using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// 项目层级与文件夹视觉管理逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class VisualHierarchyLogic
{
    /// <summary>
    /// 文件夹颜色配置
    /// </summary>
    [System.Serializable]
    public class FolderColorConfig
    {
        public string folderName;
        public Color color;
        public string iconName;
        public bool enabled = true;

        public FolderColorConfig(string name, Color col, string icon = "d_Folder")
        {
            folderName = name;
            color = col;
            iconName = icon;
        }
    }

    /// <summary>
    /// Hierarchy高亮配置
    /// </summary>
    [System.Serializable]
    public class HierarchyHighlightConfig
    {
        public string namePattern; // 名称匹配模式（支持通配符）
        public Color backgroundColor;
        public Color textColor = Color.white;
        public bool enabled = true;

        public HierarchyHighlightConfig(string pattern, Color bgColor, Color txtColor)
        {
            namePattern = pattern;
            backgroundColor = bgColor;
            textColor = txtColor;
        }
    }

    /// <summary>
    /// 项目文件夹预设配置
    /// </summary>
    [System.Serializable]
    public class ProjectFolderPreset
    {
        public string presetName;
        public List<string> folderNames;
        public bool enabled = true;

        public ProjectFolderPreset(string name, List<string> folders)
        {
            presetName = name;
            folderNames = folders;
        }
    }

    /// <summary>
    /// 默认文件夹颜色配置
    /// </summary>
    public static List<FolderColorConfig> GetDefaultFolderColors()
    {
        return new List<FolderColorConfig>
        {
            new FolderColorConfig("Scripts", new Color(0.2f, 0.6f, 1f), "d_Script"),
            new FolderColorConfig("Prefabs", new Color(0.2f, 0.8f, 0.3f), "d_Prefab"),
            new FolderColorConfig("Materials", new Color(0.7f, 0.3f, 0.9f), "d_Material"),
            new FolderColorConfig("Textures", new Color(1f, 0.7f, 0.2f), "d_Texture2D"),
            new FolderColorConfig("Models", new Color(0.3f, 0.8f, 0.9f), "d_MeshRenderer"),
            new FolderColorConfig("Scenes", new Color(1f, 0.5f, 0.5f), "d_SceneAsset"),
            new FolderColorConfig("Audio", new Color(0.5f, 0.8f, 0.3f), "d_AudioClip"),
            new FolderColorConfig("Animations", new Color(0.9f, 0.5f, 0.2f), "d_AnimationClip"),
            new FolderColorConfig("Resources", new Color(0.8f, 0.2f, 0.2f), "d_Folder"),
            new FolderColorConfig("Editor", new Color(0.6f, 0.6f, 0.6f), "d_Settings")
        };
    }

    /// <summary>
    /// 默认Hierarchy高亮配置
    /// </summary>
    public static List<HierarchyHighlightConfig> GetDefaultHierarchyHighlights()
    {
        return new List<HierarchyHighlightConfig>
        {
            new HierarchyHighlightConfig("*Manager*", new Color(0.2f, 0.6f, 1f, 0.3f), Color.white),
            new HierarchyHighlightConfig("*Player*", new Color(0.2f, 0.8f, 0.3f, 0.3f), Color.white),
            new HierarchyHighlightConfig("*Camera*", new Color(0.9f, 0.6f, 0.2f, 0.3f), Color.white),
            new HierarchyHighlightConfig("*UI*", new Color(0.7f, 0.3f, 0.9f, 0.3f), Color.white),
            new HierarchyHighlightConfig("*Light*", new Color(1f, 0.9f, 0.3f, 0.3f), Color.white)
        };
    }

    /// <summary>
    /// 默认项目文件夹预设
    /// </summary>
    public static List<ProjectFolderPreset> GetDefaultFolderPresets()
    {
        return new List<ProjectFolderPreset>
        {
            new ProjectFolderPreset("标准项目结构", new List<string>
            {
                "Scripts",
                "Prefabs",
                "Materials",
                "Textures",
                "Models",
                "Scenes",
                "Audio",
                "Animations"
            }),
            new ProjectFolderPreset("简化结构", new List<string>
            {
                "Scripts",
                "Prefabs",
                "Materials",
                "Models",
                "Textures"
            }),
            new ProjectFolderPreset("完整结构", new List<string>
            {
                "Scripts",
                "Prefabs",
                "Materials",
                "Textures",
                "Models",
                "Scenes",
                "Audio",
                "Animations",
                "Resources",
                "Editor",
                "Shaders",
                "Fonts"
            })
        };
    }

    /// <summary>
    /// 应用文件夹颜色标记
    /// </summary>
    public static void ApplyFolderColors(List<FolderColorConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return;

        int appliedCount = 0;
        string[] allFolders = AssetDatabase.GetAllAssetPaths()
            .Where(p => AssetDatabase.IsValidFolder(p))
            .ToArray();

        foreach (var config in configs)
        {
            if (!config.enabled)
                continue;

            foreach (string folderPath in allFolders)
            {
                string folderName = Path.GetFileName(folderPath);
                if (folderName.Equals(config.folderName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
                    if (folderObj != null)
                    {
                        // 设置图标（使用带颜色的图标作为视觉锚点）
                        Texture2D icon = IconHelper.GetIconSafely(config.iconName);
                        if (icon != null)
                        {
                            EditorGUIUtility.SetIconForObject(folderObj, icon);
                        }

                        // 注意：Unity的EditorGUIUtility.SetIconForObject不支持直接设置颜色
                        // 颜色标记需要通过Project窗口的自定义绘制实现
                        // 这里我们设置图标作为视觉锚点，实际颜色绘制在ProjectWindowDrawer中完成
                        appliedCount++;
                    }
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"<color=green>已应用 {appliedCount} 个文件夹颜色标记（图标已设置，颜色将在Project窗口中显示）</color>");
    }

    /// <summary>
    /// 获取文件夹的配置（用于Project窗口绘制）
    /// </summary>
    public static FolderColorConfig GetFolderConfig(string folderPath, List<FolderColorConfig> configs)
    {
        if (configs == null || string.IsNullOrEmpty(folderPath))
            return null;

        string folderName = Path.GetFileName(folderPath);
        foreach (var config in configs)
        {
            if (config.enabled && folderName.Equals(config.folderName, System.StringComparison.OrdinalIgnoreCase))
            {
                return config;
            }
        }

        return null;
    }

    /// <summary>
    /// 应用项目文件夹预设
    /// </summary>
    public static void ApplyFolderPreset(ProjectFolderPreset preset, string rootPath = "Assets")
    {
        if (preset == null || !preset.enabled || preset.folderNames == null)
            return;

        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogWarning($"无效的根路径: {rootPath}");
            return;
        }

        int createdCount = 0;
        foreach (string folderName in preset.folderNames)
        {
            string folderPath = Path.Combine(rootPath, folderName).Replace('\\', '/');
            
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parentPath = Path.GetDirectoryName(folderPath).Replace('\\', '/');
                if (AssetDatabase.IsValidFolder(parentPath))
                {
                    string guid = AssetDatabase.CreateFolder(parentPath, folderName);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        createdCount++;
                        Debug.Log($"<color=green>创建文件夹: {folderPath}</color>");
                    }
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"<color=green>已创建 {createdCount} 个文件夹（预设：{preset.presetName}）</color>");
    }

    /// <summary>
    /// 检查名称是否匹配模式（支持通配符）
    /// </summary>
    public static bool MatchPattern(string name, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        // 简单的通配符匹配：* 表示任意字符
        if (pattern.Contains("*"))
        {
            pattern = pattern.Replace("*", ".*");
            return System.Text.RegularExpressions.Regex.IsMatch(name, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return name.Equals(pattern, System.StringComparison.OrdinalIgnoreCase) ||
               name.Contains(pattern, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取Hierarchy对象的匹配配置
    /// </summary>
    public static HierarchyHighlightConfig GetMatchingConfig(GameObject obj, List<HierarchyHighlightConfig> configs)
    {
        if (obj == null || configs == null)
            return null;

        foreach (var config in configs)
        {
            if (config.enabled && MatchPattern(obj.name, config.namePattern))
            {
                return config;
            }
        }

        return null;
    }
}

