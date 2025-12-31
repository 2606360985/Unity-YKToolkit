using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 代码热重载逻辑 - 提供运行时代码热重载功能
/// </summary>
public static class HotReloadLogic
{
    /// <summary>
    /// 热重载设置
    /// </summary>
    public class Settings
    {
        public bool EnableEnterPlayModeOptions = true;
        public bool DisableDomainReload = true;
        public bool DisableSceneReload = true;
        public bool AutoRecompileOnScriptChange = false;
        public bool ShowCompilationStatus = true;
        public bool ShowNotifications = true;
    }

    /// <summary>
    /// 编译状态
    /// </summary>
    public enum CompilationStatus
    {
        Idle,
        Compiling,
        CompilingWithErrors,
        CompilingWithWarnings,
        CompilationFailed
    }

    private static CompilationStatus _lastStatus = CompilationStatus.Idle;
    private static DateTime _lastCompileTime = DateTime.MinValue;
    private static int _lastCompileErrorCount = 0;
    private static int _lastCompileWarningCount = 0;

    /// <summary>
    /// 获取当前编译状态
    /// </summary>
    public static CompilationStatus GetCompilationStatus()
    {
        if (EditorApplication.isCompiling)
        {
            return CompilationStatus.Compiling;
        }

        // 检查编译错误和警告
        var assembly = CompilationPipeline.GetAssemblies();
        bool hasErrors = false;
        bool hasWarnings = false;

        // 通过日志检查编译错误和警告
        if (_lastCompileErrorCount > 0)
        {
            hasErrors = true;
        }
        if (_lastCompileWarningCount > 0)
        {
            hasWarnings = true;
        }

        if (hasErrors)
        {
            return CompilationStatus.CompilationFailed;
        }
        if (hasWarnings)
        {
            return CompilationStatus.CompilingWithWarnings;
        }

        return CompilationStatus.Idle;
    }

    /// <summary>
    /// 获取编译状态文本
    /// </summary>
    public static string GetCompilationStatusText()
    {
        var status = GetCompilationStatus();
        switch (status)
        {
            case CompilationStatus.Idle:
                return "就绪";
            case CompilationStatus.Compiling:
                return "编译中...";
            case CompilationStatus.CompilingWithWarnings:
                return $"编译完成（{_lastCompileWarningCount} 个警告）";
            case CompilationStatus.CompilationFailed:
                return $"编译失败（{_lastCompileErrorCount} 个错误）";
            default:
                return "未知状态";
        }
    }

    /// <summary>
    /// 获取编译状态颜色
    /// </summary>
    public static Color GetCompilationStatusColor()
    {
        var status = GetCompilationStatus();
        switch (status)
        {
            case CompilationStatus.Idle:
                return Color.green;
            case CompilationStatus.Compiling:
                return Color.yellow;
            case CompilationStatus.CompilingWithWarnings:
                return new Color(1f, 0.65f, 0f); // 橙色
            case CompilationStatus.CompilationFailed:
                return Color.red;
            default:
                return Color.gray;
        }
    }

    /// <summary>
    /// 应用 Enter Play Mode Options 设置
    /// </summary>
    public static bool ApplyEnterPlayModeOptions(Settings settings)
    {
        try
        {
            // 使用反射访问 EditorSettings 的 Enter Play Mode Options
            var editorSettingsType = typeof(EditorSettings);
            
            // 启用 Enter Play Mode Options
            var enableProperty = editorSettingsType.GetProperty("enterPlayModeOptionsEnabled", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (enableProperty != null)
            {
                enableProperty.SetValue(null, settings.EnableEnterPlayModeOptions);
            }

            // 设置 Enter Play Mode Options 标志
            if (settings.EnableEnterPlayModeOptions)
            {
                var optionsProperty = editorSettingsType.GetProperty("enterPlayModeOptions",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (optionsProperty != null)
                {
                    // EnterPlayModeOptions 是一个枚举标志
                    // 0 = None
                    // 1 = DisableDomainReload
                    // 2 = DisableSceneReload
                    // 3 = Both (1 | 2)
                    int options = 0;
                    if (settings.DisableDomainReload)
                        options |= 1;
                    if (settings.DisableSceneReload)
                        options |= 2;

                    optionsProperty.SetValue(null, options);
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"HotReload: Failed to apply Enter Play Mode Options: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取当前 Enter Play Mode Options 设置
    /// </summary>
    public static Settings GetCurrentSettings()
    {
        var settings = new Settings();
        
        try
        {
            var editorSettingsType = typeof(EditorSettings);
            
            var enableProperty = editorSettingsType.GetProperty("enterPlayModeOptionsEnabled",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (enableProperty != null)
            {
                settings.EnableEnterPlayModeOptions = (bool)enableProperty.GetValue(null);
            }

            var optionsProperty = editorSettingsType.GetProperty("enterPlayModeOptions",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (optionsProperty != null)
            {
                int options = (int)optionsProperty.GetValue(null);
                settings.DisableDomainReload = (options & 1) != 0;
                settings.DisableSceneReload = (options & 2) != 0;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"HotReload: Failed to read current settings: {e.Message}");
        }

        return settings;
    }

    /// <summary>
    /// 请求重新编译
    /// </summary>
    public static void RequestRecompile()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("HotReload: Compilation already in progress");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("HotReload: Cannot recompile while in Play Mode");
            return;
        }

        AssetDatabase.Refresh();
        CompilationPipeline.RequestScriptCompilation();
        Debug.Log("<color=cyan>HotReload: 已请求重新编译</color>");
    }

    /// <summary>
    /// 强制重新编译所有脚本
    /// </summary>
    public static void ForceRecompileAll()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogWarning("HotReload: Compilation already in progress");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("HotReload: Cannot recompile while in Play Mode");
            return;
        }

        // 清除编译缓存并重新编译
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        CompilationPipeline.RequestScriptCompilation();
        Debug.Log("<color=cyan>HotReload: 已强制重新编译所有脚本</color>");
    }

    /// <summary>
    /// 更新编译统计信息
    /// </summary>
    public static void UpdateCompilationStats()
    {
        if (EditorApplication.isCompiling)
        {
            return;
        }

        // 检查编译结果
        var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
        if (logEntries != null)
        {
            var getCountMethod = logEntries.GetMethod("GetCount", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var getCountByModeMethod = logEntries.GetMethod("GetCountByMode",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            if (getCountByModeMethod != null)
            {
                // 0 = Error, 1 = Warning, 2 = Log
                _lastCompileErrorCount = (int)getCountByModeMethod.Invoke(null, new object[] { 0 });
                _lastCompileWarningCount = (int)getCountByModeMethod.Invoke(null, new object[] { 1 });
            }
        }

        _lastCompileTime = DateTime.Now;
    }

    /// <summary>
    /// 获取上次编译时间
    /// </summary>
    public static string GetLastCompileTimeText()
    {
        if (_lastCompileTime == DateTime.MinValue)
        {
            return "从未编译";
        }

        var elapsed = DateTime.Now - _lastCompileTime;
        if (elapsed.TotalSeconds < 60)
        {
            return $"{elapsed.TotalSeconds:F0} 秒前";
        }
        else if (elapsed.TotalMinutes < 60)
        {
            return $"{elapsed.TotalMinutes:F0} 分钟前";
        }
        else
        {
            return _lastCompileTime.ToString("HH:mm:ss");
        }
    }

    /// <summary>
    /// 检查是否支持 Enter Play Mode Options
    /// </summary>
    public static bool SupportsEnterPlayModeOptions()
    {
        // Unity 2021.2+ 支持 Enter Play Mode Options
        return Application.unityVersion.CompareTo("2021.2.0") >= 0;
    }

    /// <summary>
    /// 显示编译状态通知
    /// </summary>
    public static void ShowCompilationNotification(string message, MessageType type = MessageType.Info)
    {
        if (type == MessageType.Error)
        {
            Debug.LogError($"HotReload: {message}");
        }
        else if (type == MessageType.Warning)
        {
            Debug.LogWarning($"HotReload: {message}");
        }
        else
        {
            Debug.Log($"<color=cyan>HotReload: {message}</color>");
        }
    }
}

