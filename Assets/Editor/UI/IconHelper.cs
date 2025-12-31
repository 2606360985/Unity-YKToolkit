using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// 图标加载助手 - 安全地加载Unity内置图标，避免警告
/// </summary>
public static class IconHelper
{
    private static System.Type logEntriesType;
    private static MethodInfo clearMethod;
    private static bool logSuppressionInitialized = false;

    /// <summary>
    /// 初始化日志抑制机制
    /// </summary>
    private static void InitializeLogSuppression()
    {
        if (logSuppressionInitialized)
            return;

        try
        {
            // 使用反射访问Unity内部的日志系统
            var assembly = Assembly.GetAssembly(typeof(Editor));
            logEntriesType = assembly.GetType("UnityEditor.LogEntries");
            if (logEntriesType != null)
            {
                clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            }
        }
        catch { }

        logSuppressionInitialized = true;
    }

    /// <summary>
    /// 安全地加载图标，避免警告
    /// </summary>
    public static Texture2D GetIconSafely(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        InitializeLogSuppression();

        // 保存当前日志状态
        var logTypeWarning = Application.GetStackTraceLogType(LogType.Warning);
        var logTypeError = Application.GetStackTraceLogType(LogType.Error);
        
        // 禁用警告和错误日志
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);

        Texture2D result = null;

        try
        {
            // 方法1: 优先使用 FindTexture（不输出警告）
            result = EditorGUIUtility.FindTexture(iconName);
            if (result != null)
            {
                return result;
            }

            // 方法2: 尝试移除 d_ 前缀（深色主题图标）
            if (iconName.StartsWith("d_"))
            {
                string lightIconName = iconName.Substring(2);
                result = EditorGUIUtility.FindTexture(lightIconName);
                if (result != null)
                {
                    return result;
                }
            }
            // 方法3: 尝试添加 d_ 前缀（浅色主题图标）
            else
            {
                string darkIconName = "d_" + iconName;
                result = EditorGUIUtility.FindTexture(darkIconName);
                if (result != null)
                {
                    return result;
                }
            }

            // 方法4: 最后尝试使用 IconContent（可能输出警告，但我们已经抑制了）
            GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                result = iconContent.image as Texture2D;
                if (result != null)
                {
                    ClearLogEntries();
                    return result;
                }
            }
        }
        catch { }
        finally
        {
            // 恢复日志输出设置
            Application.SetStackTraceLogType(LogType.Warning, logTypeWarning);
            Application.SetStackTraceLogType(LogType.Error, logTypeError);
            
            // 清除可能产生的警告日志
            ClearLogEntries();
        }

        return null;
    }

    /// <summary>
    /// 清除Unity控制台的日志条目
    /// </summary>
    private static void ClearLogEntries()
    {
        try
        {
            if (clearMethod != null)
            {
                clearMethod.Invoke(null, null);
            }
        }
        catch { }
    }

    /// <summary>
    /// 尝试加载多个备选图标
    /// </summary>
    public static Texture2D GetIconSafely(params string[] iconNames)
    {
        foreach (string iconName in iconNames)
        {
            Texture2D icon = GetIconSafely(iconName);
            if (icon != null)
                return icon;
        }
        return null;
    }

    /// <summary>
    /// 获取带图标的GUIContent
    /// </summary>
    public static GUIContent GetIconContent(string iconName, string text = "")
    {
        GUIContent content = new GUIContent(text);
        Texture2D icon = GetIconSafely(iconName);
        if (icon != null)
        {
            content.image = icon;
        }
        return content;
    }

    /// <summary>
    /// 获取带图标的GUIContent（支持多个备选图标名称）
    /// </summary>
    public static GUIContent GetIconContent(string[] iconNames, string text = "")
    {
        GUIContent content = new GUIContent(text);
        if (iconNames != null && iconNames.Length > 0)
        {
            Texture2D icon = GetIconSafely(iconNames);
            if (icon != null)
            {
                content.image = icon;
            }
        }
        return content;
    }
}

