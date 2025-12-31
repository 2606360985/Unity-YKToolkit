using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Project窗口自定义绘制器 - 用于显示文件夹颜色标记
/// </summary>
public static class ProjectWindowDrawer
{
    private static List<VisualHierarchyLogic.FolderColorConfig> _folderConfigs;
    private static bool _enabled = true;

    /// <summary>
    /// 初始化Project窗口绘制回调
    /// </summary>
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        LoadConfigs();
    }

    /// <summary>
    /// Project窗口项目绘制回调
    /// </summary>
    private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
    {
        if (!_enabled || _folderConfigs == null || _folderConfigs.Count == 0)
            return;

        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
            return;

        var config = VisualHierarchyLogic.GetFolderConfig(assetPath, _folderConfigs);
        if (config == null)
            return;

        // 绘制颜色背景（在图标区域）
        Rect iconRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.height, selectionRect.height);
        
        // 绘制半透明背景色
        Color bgColor = config.color;
        bgColor.a = 0.3f; // 半透明
        EditorGUI.DrawRect(iconRect, bgColor);

        // 绘制边框
        Color borderColor = config.color;
        borderColor.a = 0.8f;
        EditorGUI.DrawRect(new Rect(iconRect.x, iconRect.y, iconRect.width, 1), borderColor);
        EditorGUI.DrawRect(new Rect(iconRect.x, iconRect.y + iconRect.height - 1, iconRect.width, 1), borderColor);
        EditorGUI.DrawRect(new Rect(iconRect.x, iconRect.y, 1, iconRect.height), borderColor);
        EditorGUI.DrawRect(new Rect(iconRect.x + iconRect.width - 1, iconRect.y, 1, iconRect.height), borderColor);
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    private static void LoadConfigs()
    {
        string json = ToolboxSettings.GetString("VisualHierarchy_FolderColors", "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<SerializableList<VisualHierarchyLogic.FolderColorConfig>>(json);
                _folderConfigs = wrapper.list;
            }
            catch
            {
                _folderConfigs = VisualHierarchyLogic.GetDefaultFolderColors();
            }
        }
        else
        {
            _folderConfigs = VisualHierarchyLogic.GetDefaultFolderColors();
        }

        _enabled = ToolboxSettings.GetBool("VisualHierarchy_FolderColorEnabled", true);
    }

    /// <summary>
    /// 刷新配置（由模块调用）
    /// </summary>
    public static void RefreshConfigs()
    {
        LoadConfigs();
    }

    // 辅助类：用于序列化List
    [System.Serializable]
    private class SerializableList<T>
    {
        public List<T> list;
    }
}

