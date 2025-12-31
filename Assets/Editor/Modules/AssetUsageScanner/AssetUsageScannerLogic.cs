using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// 资产使用情况扫描逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class AssetUsageScannerLogic
{
    /// <summary>
    /// 资产引用状态
    /// </summary>
    public enum ReferenceStatus
    {
        Unreferenced,      // 未引用
        EditorOnly,        // 仅编辑器引用（Editor文件夹中的脚本、Editor资源等）
        TestOnly,         // 仅测试引用（Tests文件夹中的脚本、测试资源等）
        Runtime           // 运行时引用（场景、Prefab等）
    }

    /// <summary>
    /// 资产使用情况信息
    /// </summary>
    public class AssetUsageInfo
    {
        public string AssetPath;
        public string AssetName;
        public Object AssetObject;
        public ReferenceStatus Status;
        public List<string> ReferencedBy = new List<string>(); // 引用此资产的资产路径
        public long FileSize; // 文件大小（字节）

        public AssetUsageInfo(string path)
        {
            AssetPath = path;
            AssetName = Path.GetFileName(path);
            AssetObject = AssetDatabase.LoadAssetAtPath<Object>(path);
            
            // 获取文件大小
            if (File.Exists(path))
            {
                FileInfo fileInfo = new FileInfo(path);
                FileSize = fileInfo.Length;
            }
        }

        /// <summary>
        /// 获取文件大小格式化字符串
        /// </summary>
        public string GetFormattedSize()
        {
            if (FileSize < 1024)
                return $"{FileSize} B";
            else if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F2} KB";
            else
                return $"{FileSize / (1024.0 * 1024.0):F2} MB";
        }
    }

    /// <summary>
    /// 扫描结果
    /// </summary>
    public class ScanResult
    {
        public List<AssetUsageInfo> UnreferencedAssets = new List<AssetUsageInfo>();
        public List<AssetUsageInfo> EditorOnlyAssets = new List<AssetUsageInfo>();
        public List<AssetUsageInfo> TestOnlyAssets = new List<AssetUsageInfo>();
        public List<AssetUsageInfo> RuntimeAssets = new List<AssetUsageInfo>();
        public int TotalScanned;
        public float ScanProgress;

        public int TotalUnused => UnreferencedAssets.Count + EditorOnlyAssets.Count + TestOnlyAssets.Count;
        public long TotalUnusedSize => UnreferencedAssets.Sum(a => a.FileSize) + 
                                       EditorOnlyAssets.Sum(a => a.FileSize) + 
                                       TestOnlyAssets.Sum(a => a.FileSize);
    }

    /// <summary>
    /// 扫描所有资产的使用情况
    /// </summary>
    public static ScanResult ScanAllAssets(HashSet<string> whitelist = null, System.Action<float> progressCallback = null)
    {
        ScanResult result = new ScanResult();
        whitelist = whitelist ?? new HashSet<string>();

        // 获取所有资产路径 - 仅扫描 Assets 文件夹内的资产
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/")) // 仅扫描 Assets 文件夹
            .Where(path => !AssetDatabase.IsValidFolder(path))
            .Where(path => !path.StartsWith("Assets/Editor/") && !path.StartsWith("Assets/Tests/"))
            .Where(path => !whitelist.Contains(path))
            .Where(path => !ShouldSkipAsset(path))
            .ToArray();

        result.TotalScanned = allAssetPaths.Length;

        // 获取所有场景和Prefab路径（用于检查引用）- 仅扫描 Assets 文件夹
        HashSet<string> scenePaths = new HashSet<string>(
            AssetDatabase.FindAssets("t:Scene")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => path.StartsWith("Assets/")) // 仅扫描 Assets 文件夹
        );

        HashSet<string> prefabPaths = new HashSet<string>(
            AssetDatabase.FindAssets("t:Prefab")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => path.StartsWith("Assets/")) // 仅扫描 Assets 文件夹
                .Where(path => !path.StartsWith("Assets/Editor/") && !path.StartsWith("Assets/Tests/"))
        );

        // 获取所有Editor文件夹中的资产（用于识别仅编辑器引用）- 仅扫描 Assets 文件夹
        HashSet<string> editorAssetPaths = new HashSet<string>(
            AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/")) // 仅扫描 Assets 文件夹
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(path => path.StartsWith("Assets/Editor/") || path.Contains("/Editor/"))
        );

        // 获取所有Tests文件夹中的资产（用于识别仅测试引用）- 仅扫描 Assets 文件夹
        HashSet<string> testAssetPaths = new HashSet<string>(
            AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/")) // 仅扫描 Assets 文件夹
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(path => path.StartsWith("Assets/Tests/") || path.Contains("/Tests/"))
        );

        // 构建反向引用映射（资产路径 -> 引用它的资产路径列表）
        Dictionary<string, List<string>> reverseDependencies = new Dictionary<string, List<string>>();
        
        // 先构建所有可能引用其他资产的资产的依赖关系
        HashSet<string> allReferencerPaths = new HashSet<string>();
        allReferencerPaths.UnionWith(scenePaths);
        allReferencerPaths.UnionWith(prefabPaths);
        allReferencerPaths.UnionWith(editorAssetPaths);
        allReferencerPaths.UnionWith(testAssetPaths);
        
        // 也包含所有非脚本资产（它们可能引用其他资产）- 仅扫描 Assets 文件夹
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (path.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(path) && !ShouldSkipAsset(path))
            {
                allReferencerPaths.Add(path);
            }
        }

        // 构建反向依赖映射
        int totalReferencers = allReferencerPaths.Count;
        int processedReferencers = 0;
        foreach (string referencerPath in allReferencerPaths)
        {
            string[] dependencies = AssetDatabase.GetDependencies(referencerPath, false);
            foreach (string depPath in dependencies)
            {
                if (depPath == referencerPath)
                    continue;
                
                if (!reverseDependencies.ContainsKey(depPath))
                    reverseDependencies[depPath] = new List<string>();
                
                reverseDependencies[depPath].Add(referencerPath);
            }
            
            processedReferencers++;
            if (processedReferencers % 100 == 0)
            {
                progressCallback?.Invoke(0.3f * processedReferencers / totalReferencers);
            }
        }

        // 扫描每个资产
        for (int i = 0; i < allAssetPaths.Length; i++)
        {
            string assetPath = allAssetPaths[i];
            
            AssetUsageInfo info = new AssetUsageInfo(assetPath);
            
            // 检查引用情况（使用预构建的反向依赖映射）
            CheckAssetReferencesOptimized(assetPath, info, scenePaths, prefabPaths, 
                editorAssetPaths, testAssetPaths, reverseDependencies);
            
            // 分类资产
            switch (info.Status)
            {
                case ReferenceStatus.Unreferenced:
                    result.UnreferencedAssets.Add(info);
                    break;
                case ReferenceStatus.EditorOnly:
                    result.EditorOnlyAssets.Add(info);
                    break;
                case ReferenceStatus.TestOnly:
                    result.TestOnlyAssets.Add(info);
                    break;
                case ReferenceStatus.Runtime:
                    result.RuntimeAssets.Add(info);
                    break;
            }

            // 更新进度（30%用于构建映射，70%用于扫描）
            result.ScanProgress = 0.3f + 0.7f * (float)(i + 1) / allAssetPaths.Length;
            progressCallback?.Invoke(result.ScanProgress);
        }

        return result;
    }

    /// <summary>
    /// 检查资产是否被引用（优化版本，使用预构建的反向依赖映射）
    /// </summary>
    private static void CheckAssetReferencesOptimized(string assetPath, AssetUsageInfo info, 
        HashSet<string> scenePaths, HashSet<string> prefabPaths,
        HashSet<string> editorAssetPaths, HashSet<string> testAssetPaths,
        Dictionary<string, List<string>> reverseDependencies)
    {
        // 获取引用此资产的资产列表
        if (!reverseDependencies.ContainsKey(assetPath))
        {
            info.Status = ReferenceStatus.Unreferenced;
            return;
        }

        List<string> referencers = reverseDependencies[assetPath];
        info.ReferencedBy.AddRange(referencers);

        bool hasRuntimeRef = false;
        bool hasEditorRef = false;
        bool hasTestRef = false;

        // 检查直接引用
        foreach (string referencerPath in referencers)
        {
            if (scenePaths.Contains(referencerPath) || prefabPaths.Contains(referencerPath))
            {
                hasRuntimeRef = true;
            }
            else if (editorAssetPaths.Contains(referencerPath))
            {
                hasEditorRef = true;
            }
            else if (testAssetPaths.Contains(referencerPath))
            {
                hasTestRef = true;
            }
        }

        // 如果已经有运行时引用，直接返回
        if (hasRuntimeRef)
        {
            info.Status = ReferenceStatus.Runtime;
            return;
        }

        // 检查间接引用（递归检查引用者是否被运行时资产引用）
        HashSet<string> visited = new HashSet<string>();
        foreach (string referencerPath in referencers)
        {
            if (IsReferencedByRuntimeRecursive(referencerPath, scenePaths, prefabPaths, 
                reverseDependencies, visited))
            {
                hasRuntimeRef = true;
                break;
            }
        }

        // 确定最终状态
        if (hasRuntimeRef)
        {
            info.Status = ReferenceStatus.Runtime;
        }
        else if (hasEditorRef && !hasTestRef)
        {
            info.Status = ReferenceStatus.EditorOnly;
        }
        else if (hasTestRef && !hasEditorRef)
        {
            info.Status = ReferenceStatus.TestOnly;
        }
        else if (hasEditorRef && hasTestRef)
        {
            // 既有编辑器引用又有测试引用，优先标记为编辑器引用
            info.Status = ReferenceStatus.EditorOnly;
        }
        else
        {
            info.Status = ReferenceStatus.Unreferenced;
        }
    }

    /// <summary>
    /// 递归检查资产是否被运行时资产引用（使用反向依赖映射）
    /// </summary>
    private static bool IsReferencedByRuntimeRecursive(string assetPath, HashSet<string> scenePaths, 
        HashSet<string> prefabPaths, Dictionary<string, List<string>> reverseDependencies, 
        HashSet<string> visited)
    {
        if (visited.Contains(assetPath))
            return false;
        
        visited.Add(assetPath);

        if (!reverseDependencies.ContainsKey(assetPath))
            return false;

        foreach (string referencerPath in reverseDependencies[assetPath])
        {
            if (scenePaths.Contains(referencerPath) || prefabPaths.Contains(referencerPath))
                return true;
            
            if (IsReferencedByRuntimeRecursive(referencerPath, scenePaths, prefabPaths, 
                reverseDependencies, visited))
                return true;
        }

        return false;
    }


    /// <summary>
    /// 判断是否应该跳过某个资产
    /// </summary>
    private static bool ShouldSkipAsset(string assetPath)
    {
        // 跳过脚本文件
        string ext = Path.GetExtension(assetPath).ToLower();
        if (ext == ".cs" || ext == ".js")
            return true;

        // 跳过.meta文件
        if (ext == ".meta")
            return true;

        // 跳过某些Unity系统文件
        string fileName = Path.GetFileName(assetPath).ToLower();
        if (fileName.Contains("unity") && ext == ".asset")
            return true;

        return false;
    }

    /// <summary>
    /// 删除资产
    /// </summary>
    public static bool DeleteAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        try
        {
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"删除资产失败: {assetPath}\n{e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 批量删除资产
    /// </summary>
    public static int DeleteAssets(List<string> assetPaths)
    {
        int deletedCount = 0;
        
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in assetPaths)
            {
                if (DeleteAsset(path))
                    deletedCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        return deletedCount;
    }

    /// <summary>
    /// 加载白名单
    /// </summary>
    public static HashSet<string> LoadWhitelist()
    {
        HashSet<string> whitelist = new HashSet<string>();
        string whitelistJson = EditorPrefs.GetString("AssetUsageScanner_Whitelist", "");
        
        if (!string.IsNullOrEmpty(whitelistJson))
        {
            try
            {
                string[] paths = whitelistJson.Split('|');
                foreach (string path in paths)
                {
                    if (!string.IsNullOrEmpty(path))
                        whitelist.Add(path);
                }
            }
            catch
            {
                Debug.LogWarning("加载白名单失败，使用空白名单");
            }
        }

        return whitelist;
    }

    /// <summary>
    /// 保存白名单
    /// </summary>
    public static void SaveWhitelist(HashSet<string> whitelist)
    {
        string whitelistJson = string.Join("|", whitelist.Where(p => !string.IsNullOrEmpty(p)));
        EditorPrefs.SetString("AssetUsageScanner_Whitelist", whitelistJson);
    }

    /// <summary>
    /// 添加到白名单
    /// </summary>
    public static void AddToWhitelist(string assetPath)
    {
        HashSet<string> whitelist = LoadWhitelist();
        whitelist.Add(assetPath);
        SaveWhitelist(whitelist);
    }

    /// <summary>
    /// 从白名单移除
    /// </summary>
    public static void RemoveFromWhitelist(string assetPath)
    {
        HashSet<string> whitelist = LoadWhitelist();
        whitelist.Remove(assetPath);
        SaveWhitelist(whitelist);
    }
}

