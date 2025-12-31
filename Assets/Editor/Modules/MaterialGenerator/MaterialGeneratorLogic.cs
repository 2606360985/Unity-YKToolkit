using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Rendering;

/// <summary>
/// 智能批量材质生成逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class MaterialGeneratorLogic
{
    // --- [PBR 匹配关键词定义] - 支持大小写不敏感识别 ---
    // Albedo/Diffuse 关键词（支持多种命名格式，按长度从长到短排序，优先匹配长关键词）
    private static readonly string[] albedoKeys = {
        // 复合关键词（优先匹配）
        "_albedotransparency", "_albedo_transparency", "albedotransparency", "albedo_transparency",
        "_basecolortransparency", "_base_color_transparency", "basecolortransparency", "base_color_transparency",
        "_basemap", "_base_map", "_basecolor", "_base_color", "_albedo", "_diffuse", "_d", "_c", "_color", "_maintex", "_main_tex",
        // 支持无下划线前缀的格式
        "basemap", "base_map", "basecolor", "base_color", "albedo", "diffuse", "color", "maintex", "main_tex"
    };
    // Normal/Bump 关键词
    private static readonly string[] normalKeys = {
        "_normal", "_normalmap", "_normal_map", "_n", "_norm", "_bump", "_bumpmap", "_bump_map",
        "normal", "normalmap", "normal_map", "norm", "bump", "bumpmap", "bump_map"
    };
    // Mask/Metallic/Roughness 关键词（按长度从长到短排序）
    private static readonly string[] maskKeys = {
        // 复合关键词（优先匹配）
        "_metallicsmoothness", "_metallic_smoothness", "metallicsmoothness", "metallic_smoothness",
        "_metallicroughness", "_metallic_roughness", "metallicroughness", "metallic_roughness",
        "_roughnessmetallic", "_roughness_metallic", "roughnessmetallic", "roughness_metallic",
        "_maskmap", "_mask_map", "_mask", "_ms", "_metallic", "_m", "_roughness", "_r", "_rough", "_specular", "_s", "_ao", "_metallicglossmap", "_metallic_gloss_map",
        "maskmap", "mask_map", "mask", "metallic", "roughness", "rough", "specular", "ao", "metallicglossmap", "metallic_gloss_map"
    };
    // Height/Displacement 关键词
    private static readonly string[] heightKeys = {
        "_height", "_heightmap", "_height_map", "_h", "_disp", "_displacement", "_parallaxmap", "_parallax_map",
        "height", "heightmap", "height_map", "disp", "displacement", "parallaxmap", "parallax_map"
    };

    /// <summary>
    /// 从选中的对象创建材质
    /// </summary>
    public static void CreateMaterialsFromSelection(Object[] selection)
    {
        // 1. 获取所有选中的贴图
        HashSet<string> texturePaths = new HashSet<string>();
        foreach (var obj in selection)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
            {
                // 如果是文件夹，搜寻文件夹内所有贴图
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                foreach (var guid in guids) texturePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else if (obj is Texture2D)
            {
                texturePaths.Add(path);
            }
        }

        if (texturePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "未选中任何贴图或包含贴图的文件夹！", "确定");
            return;
        }

        // 2. 按前缀进行组队 (例如 Stone_Albedo 和 Stone_Normal 都会归入 "Stone" 组)
        Dictionary<string, List<string>> materialGroups = new Dictionary<string, List<string>>();
        foreach (string path in texturePaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string baseName = GetBaseName(fileName);
            if (!materialGroups.ContainsKey(baseName)) materialGroups[baseName] = new List<string>();
            materialGroups[baseName].Add(path);
        }

        // 3. 开始批量创建材质
        int count = 0;
        AssetDatabase.StartAssetEditing(); // 提升批量处理速度
        try
        {
            foreach (var group in materialGroups)
            {
                CreatePBRMaterialFromGroup(group.Key, group.Value);
                count++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"<color=green>成功生成 {count} 个材质球！</color>");
        EditorUtility.DisplayDialog("完成", $"已成功基于名称匹配生成了 {count} 个材质球。", "确定");
    }

    /// <summary>
    /// 从文件名提取基础名称（移除贴图类型后缀）
    /// </summary>
    private static string GetBaseName(string fileName)
    {
        // 统一转换为小写进行匹配（大小写不敏感）
        string lowerName = fileName.ToLower();
        string processedFileName = fileName;
        string numberSuffix = null;

        // 先提取数字后缀（如 ".1001"）
        int lastDotIndex = fileName.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            string beforeDot = fileName.Substring(0, lastDotIndex);
            string afterDot = fileName.Substring(lastDotIndex + 1);
            // 如果点号后是纯数字，保存这个数字后缀
            if (Regex.IsMatch(afterDot, @"^\d+$"))
            {
                numberSuffix = afterDot;
                processedFileName = beforeDot;
                lowerName = processedFileName.ToLower();
            }
        }

        // 尝试从文件名中移除已知的贴图类型后缀关键词（大小写不敏感）
        // 按长度排序，优先匹配长关键词（如先匹配 "albedotransparency"，再匹配 "albedo"）
        string[] allKeys = albedoKeys.Concat(normalKeys).Concat(maskKeys).Concat(heightKeys)
            .OrderByDescending(k => k.Length)
            .ToArray();

        foreach (var key in allKeys)
        {
            string lowerKey = key.ToLower();

            // 支持多种匹配方式：
            // 1. 以关键词结尾（如 "Stone_Albedo"）
            // 2. 关键词前有下划线或连字符（如 "Stone_AlbedoTransparency"）
            // 3. 关键词后跟其他词（如 "AlbedoTransparency"）
            bool matched = false;
            int removeLength = 0;

            // 情况1：完全以关键词结尾
            if (lowerName.EndsWith(lowerKey))
            {
                matched = true;
                removeLength = key.Length;

                // 检查前面是否有分隔符
                int keyStartIndex = processedFileName.Length - key.Length;
                if (keyStartIndex > 0)
                {
                    char beforeKey = processedFileName[keyStartIndex - 1];
                    if (beforeKey == '_' || beforeKey == '-')
                    {
                        removeLength = key.Length + 1;
                    }
                }
            }
            // 情况2：关键词前有分隔符，且后面可能还有其他内容（复合词）
            else if (lowerName.Contains("_" + lowerKey) || lowerName.Contains("-" + lowerKey))
            {
                int keyIndex = lowerName.LastIndexOf("_" + lowerKey);
                if (keyIndex == -1) keyIndex = lowerName.LastIndexOf("-" + lowerKey);

                if (keyIndex >= 0)
                {
                    int keyEndIndex = keyIndex + lowerKey.Length + 1;
                    if (keyEndIndex >= lowerName.Length - 20) // 允许后面最多20个字符
                    {
                        matched = true;
                        removeLength = processedFileName.Length - keyIndex;
                    }
                }
            }
            // 情况3：关键词后跟其他词（如 "AlbedoTransparency"）
            else if (lowerName.Contains(lowerKey))
            {
                int keyIndex = lowerName.LastIndexOf(lowerKey);
                if (keyIndex >= 0)
                {
                    if (keyIndex > 0)
                    {
                        char beforeKey = lowerName[keyIndex - 1];
                        if (beforeKey == '_' || beforeKey == '-')
                        {
                            int keyEndIndex = keyIndex + lowerKey.Length;
                            if (keyEndIndex >= lowerName.Length - 20)
                            {
                                matched = true;
                                removeLength = processedFileName.Length - keyIndex + 1;
                            }
                        }
                    }
                    else if (keyIndex == 0 && lowerName.Length <= lowerKey.Length + 20)
                    {
                        matched = true;
                        removeLength = processedFileName.Length;
                        return numberSuffix ?? "Material";
                    }
                }
            }

            if (matched)
            {
                if (processedFileName.Length >= removeLength)
                {
                    string baseName = processedFileName.Substring(0, processedFileName.Length - removeLength);
                    if (string.IsNullOrEmpty(baseName) || baseName == "_" || baseName == "-")
                    {
                        return numberSuffix ?? processedFileName;
                    }
                    baseName = baseName.TrimEnd('_', '-');
                    if (baseName.StartsWith("_") || baseName.StartsWith("-"))
                        baseName = baseName.Substring(1);
                    return string.IsNullOrEmpty(baseName) ? (numberSuffix ?? processedFileName) : baseName;
                }
            }
        }

        return numberSuffix ?? processedFileName;
    }

    /// <summary>
    /// 从贴图组创建PBR材质
    /// </summary>
    private static void CreatePBRMaterialFromGroup(string baseName, List<string> paths)
    {
        // 自动识别当前管线
        string shaderName = "Standard";
        bool isHDRP = false, isURP = false;
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            string pipe = GraphicsSettings.currentRenderPipeline.GetType().ToString();
            if (pipe.Contains("HDRenderPipeline")) { shaderName = "HDRP/Lit"; isHDRP = true; }
            else if (pipe.Contains("UniversalRenderPipeline")) { shaderName = "Universal Render Pipeline/Lit"; isURP = true; }
        }

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"<color=red>无法找到着色器: {shaderName}</color>");
            return;
        }

        Material mat = new Material(shader);
        string folder = Path.GetDirectoryName(paths[0]);
        string matPath = $"{folder}/{baseName}_Mat.mat";

        // 用于记录已分配的贴图类型，避免重复分配
        bool hasAlbedo = false, hasNormal = false, hasMask = false, hasHeight = false;

        foreach (string path in paths)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"<color=yellow>无法加载贴图: {path}</color>");
                continue;
            }

            // 统一转换为小写进行匹配（大小写不敏感）
            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();
            string fileNameOriginal = Path.GetFileNameWithoutExtension(path);
            bool textureAssigned = false;

            // Albedo/BaseMap
            if (!hasAlbedo && !textureAssigned)
            {
                foreach (var key in albedoKeys)
                {
                    string lowerKey = key.ToLower();
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        string propName = isHDRP ? "_BaseColorMap" : (isURP ? "_BaseMap" : "_MainTex");
                        if (TrySetTexture(mat, propName, tex, fileNameOriginal, ref textureAssigned))
                        {
                            hasAlbedo = true;
                            break;
                        }
                    }
                }
            }

            // Normal
            if (!hasNormal && !textureAssigned)
            {
                foreach (var key in normalKeys)
                {
                    string lowerKey = key.ToLower();
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        SetNormalMapTexture(path);
                        string propName = isHDRP ? "_NormalMap" : (isURP ? "_BumpMap" : "_BumpMap");
                        if (TrySetTexture(mat, propName, tex, fileNameOriginal, ref textureAssigned, enableNormalMap: true))
                        {
                            hasNormal = true;
                            break;
                        }
                    }
                }
            }

            // Mask
            if (!hasMask && !textureAssigned)
            {
                foreach (var key in maskKeys)
                {
                    string lowerKey = key.ToLower();
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        string propName = isHDRP ? "_MaskMap" : "_MetallicGlossMap";
                        bool enableKeyword = !isHDRP;
                        if (TrySetTexture(mat, propName, tex, fileNameOriginal, ref textureAssigned, enableMetallicKeyword: enableKeyword))
                        {
                            hasMask = true;
                            break;
                        }
                    }
                }
            }

            // Height
            if (!hasHeight && !textureAssigned)
            {
                foreach (var key in heightKeys)
                {
                    string lowerKey = key.ToLower();
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        string propName = isHDRP ? "_HeightMap" : "_ParallaxMap";
                        if (TrySetTexture(mat, propName, tex, fileNameOriginal, ref textureAssigned))
                        {
                            hasHeight = true;
                            break;
                        }
                    }
                }
            }

            if (!textureAssigned)
            {
                Debug.LogWarning($"<color=yellow>未识别贴图类型: {fileNameOriginal}</color>");
            }
        }

        // 创建材质资产
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan>材质已创建: {matPath}</color>");
        Debug.Log($"<color=cyan>管线类型: {(isHDRP ? "HDRP" : (isURP ? "URP" : "Built-in"))}</color>");
    }

    /// <summary>
    /// 尝试设置材质贴图
    /// </summary>
    private static bool TrySetTexture(Material mat, string propName, Texture2D tex, string fileNameOriginal, 
        ref bool textureAssigned, bool enableNormalMap = false, bool enableMetallicKeyword = false)
    {
        try
        {
            if (mat.HasProperty(propName))
            {
                mat.SetTexture(propName, tex);
                if (enableNormalMap) mat.EnableKeyword("_NORMALMAP");
                if (enableMetallicKeyword) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                Debug.Log($"<color=green>✓ 分配: {fileNameOriginal} → {propName}</color>");
                textureAssigned = true;
                return true;
            }
            else
            {
                string altPropName = propName.TrimStart('_');
                if (mat.HasProperty(altPropName))
                {
                    mat.SetTexture(altPropName, tex);
                    if (enableNormalMap) mat.EnableKeyword("_NORMALMAP");
                    if (enableMetallicKeyword) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    Debug.Log($"<color=green>✓ 分配: {fileNameOriginal} → {altPropName}</color>");
                    textureAssigned = true;
                    return true;
                }
                else
                {
                    Debug.LogWarning($"<color=yellow>材质缺少属性: {propName} 或 {altPropName}</color>");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red>设置贴图失败: {propName} - {e.Message}</color>");
        }
        return false;
    }

    /// <summary>
    /// 设置法线贴图的TextureImporter
    /// </summary>
    private static void SetNormalMapTexture(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }
}

