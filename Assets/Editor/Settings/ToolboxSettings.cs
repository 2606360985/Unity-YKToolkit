using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 工具箱设置管理 - 统一管理所有模块的设置持久化
/// </summary>
public static class ToolboxSettings
{
    private const string SETTINGS_PREFIX = "UnityProToolbox_";

    // ==================== 模块顺序和折叠状态 ====================

    private const string MODULE_ORDER_KEY = SETTINGS_PREFIX + "ModuleOrder";
    private const string MODULE_EXPANDED_KEY = SETTINGS_PREFIX + "ModuleExpanded_";
    private const string CATEGORY_EXPANDED_KEY = SETTINGS_PREFIX + "CategoryExpanded_";

    /// <summary>
    /// 保存模块顺序
    /// </summary>
    public static void SaveModuleOrder(int[] moduleIds)
    {
        string order = string.Join(",", moduleIds.Select(id => id.ToString()));
        EditorPrefs.SetString(MODULE_ORDER_KEY, order);
    }

    /// <summary>
    /// 加载模块顺序
    /// </summary>
    public static int[] LoadModuleOrder()
    {
        string savedOrder = EditorPrefs.GetString(MODULE_ORDER_KEY, "");
        if (string.IsNullOrEmpty(savedOrder))
            return null;

        string[] orderStrings = savedOrder.Split(',');
        int[] order = new int[orderStrings.Length];
        for (int i = 0; i < orderStrings.Length; i++)
        {
            if (int.TryParse(orderStrings[i], out int id))
                order[i] = id;
        }
        return order;
    }

    /// <summary>
    /// 保存模块折叠状态
    /// </summary>
    public static void SaveModuleExpanded(string moduleId, bool expanded)
    {
        EditorPrefs.SetBool(MODULE_EXPANDED_KEY + moduleId, expanded);
    }

    /// <summary>
    /// 加载模块折叠状态
    /// </summary>
    public static bool LoadModuleExpanded(string moduleId, bool defaultValue = false)
    {
        return EditorPrefs.GetBool(MODULE_EXPANDED_KEY + moduleId, defaultValue);
    }

    /// <summary>
    /// 保存分类折叠状态
    /// </summary>
    public static void SaveCategoryExpanded(string category, bool expanded)
    {
        EditorPrefs.SetBool(CATEGORY_EXPANDED_KEY + category, expanded);
    }

    /// <summary>
    /// 加载分类折叠状态
    /// </summary>
    public static bool LoadCategoryExpanded(string category, bool defaultValue = true)
    {
        return EditorPrefs.GetBool(CATEGORY_EXPANDED_KEY + category, defaultValue);
    }

    // ==================== 通用设置存取 ====================

    /// <summary>
    /// 获取字符串设置
    /// </summary>
    public static string GetString(string key, string defaultValue = "")
    {
        return EditorPrefs.GetString(SETTINGS_PREFIX + key, defaultValue);
    }

    /// <summary>
    /// 设置字符串设置
    /// </summary>
    public static void SetString(string key, string value)
    {
        EditorPrefs.SetString(SETTINGS_PREFIX + key, value);
    }

    /// <summary>
    /// 获取整数设置
    /// </summary>
    public static int GetInt(string key, int defaultValue = 0)
    {
        return EditorPrefs.GetInt(SETTINGS_PREFIX + key, defaultValue);
    }

    /// <summary>
    /// 设置整数设置
    /// </summary>
    public static void SetInt(string key, int value)
    {
        EditorPrefs.SetInt(SETTINGS_PREFIX + key, value);
    }

    /// <summary>
    /// 获取浮点数设置
    /// </summary>
    public static float GetFloat(string key, float defaultValue = 0f)
    {
        return EditorPrefs.GetFloat(SETTINGS_PREFIX + key, defaultValue);
    }

    /// <summary>
    /// 设置浮点数设置
    /// </summary>
    public static void SetFloat(string key, float value)
    {
        EditorPrefs.SetFloat(SETTINGS_PREFIX + key, value);
    }

    /// <summary>
    /// 获取布尔设置
    /// </summary>
    public static bool GetBool(string key, bool defaultValue = false)
    {
        return EditorPrefs.GetBool(SETTINGS_PREFIX + key, defaultValue);
    }

    /// <summary>
    /// 设置布尔设置
    /// </summary>
    public static void SetBool(string key, bool value)
    {
        EditorPrefs.SetBool(SETTINGS_PREFIX + key, value);
    }

    /// <summary>
    /// 删除设置
    /// </summary>
    public static void DeleteKey(string key)
    {
        EditorPrefs.DeleteKey(SETTINGS_PREFIX + key);
    }
}

