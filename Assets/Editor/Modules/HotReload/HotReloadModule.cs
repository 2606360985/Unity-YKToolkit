using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;

/// <summary>
/// 代码热重载模块 - UI部分
/// </summary>
public class HotReloadModule : ToolModule
{
    public override string Name => "代码热重载";
    public override string Category => "Tools";
    public override int Order => 1;
    public override string IconName => "d_ScriptableObject Icon";
    public override Color HeaderColor => new Color(0.2f, 0.6f, 0.9f);
    public override Color BackgroundColor => new Color(0.9f, 0.95f, 1f);

    // 模块状态（UI相关）
    private HotReloadLogic.Settings _settings = new HotReloadLogic.Settings();
    private const string SETTINGS_KEY_PREFIX = "HotReload_";
    private bool _showAdvancedOptions = false;
    private double _lastUpdateTime = 0;
    private const double UPDATE_INTERVAL = 0.5; // 每0.5秒更新一次状态

    private bool _wasCompiling = false;

    public override void OnInitialize()
    {
        LoadSettings();
        ApplySettings();
        
        // 注册编译完成事件（使用兼容的方式）
        try
        {
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }
        catch
        {
            // 如果事件不存在，使用轮询方式
        }
        
        EditorApplication.update += OnUpdate;
    }

    public override void OnCleanup()
    {
        try
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }
        catch { }
        
        EditorApplication.update -= OnUpdate;
    }

    private void OnCompilationFinished(object obj)
    {
        HotReloadLogic.UpdateCompilationStats();
    }

    private void OnUpdate()
    {
        // 检测编译状态变化
        bool isCompiling = EditorApplication.isCompiling;
        if (_wasCompiling && !isCompiling)
        {
            // 编译完成
            HotReloadLogic.UpdateCompilationStats();
        }
        _wasCompiling = isCompiling;

        // 定期更新编译状态
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - _lastUpdateTime > UPDATE_INTERVAL)
        {
            _lastUpdateTime = currentTime;
            if (!isCompiling)
            {
                HotReloadLogic.UpdateCompilationStats();
            }
        }
    }

    public override bool IsAvailable(ToolContext context)
    {
        return true; // 热重载功能始终可用
    }

    public override void OnGUI(ToolContext context)
    {
        // 检查 Unity 版本支持
        if (!HotReloadLogic.SupportsEnterPlayModeOptions())
        {
            EditorGUILayout.HelpBox(
                "代码热重载功能需要 Unity 2021.2 或更高版本。\n" +
                "当前版本: " + Application.unityVersion,
                MessageType.Warning
            );
            return;
        }

        // 编译状态显示
        if (_settings.ShowCompilationStatus)
        {
            DrawCompilationStatus();
            EditorGUILayout.Space(5);
        }

        // Enter Play Mode Options 设置
        EditorGUILayout.LabelField("进入播放模式选项", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        _settings.EnableEnterPlayModeOptions = EditorGUILayout.Toggle(
            "启用快速进入播放模式", _settings.EnableEnterPlayModeOptions
        );
        
        if (_settings.EnableEnterPlayModeOptions)
        {
            EditorGUI.indentLevel++;
            _settings.DisableDomainReload = EditorGUILayout.Toggle(
                "禁用 Domain Reload（推荐）", _settings.DisableDomainReload
            );
            _settings.DisableSceneReload = EditorGUILayout.Toggle(
                "禁用 Scene Reload", _settings.DisableSceneReload
            );
            EditorGUI.indentLevel--;

            EditorGUILayout.HelpBox(
                "禁用 Domain Reload 可以大幅加快进入播放模式的速度，\n" +
                "但静态变量和静态构造函数不会重置。",
                MessageType.Info
            );
        }

        if (EditorGUI.EndChangeCheck())
        {
            ApplySettings();
            SaveSettings();
        }

        EditorGUILayout.Space(5);

        // 编译控制按钮
        EditorGUILayout.LabelField("编译控制", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling || EditorApplication.isPlaying);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔄 重新编译", GUILayout.Height(25)))
        {
            HotReloadLogic.RequestRecompile();
            if (_settings.ShowNotifications)
            {
                HotReloadLogic.ShowCompilationNotification("已请求重新编译");
            }
        }
        
        if (GUILayout.Button("⚡ 强制重新编译所有", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog(
                "强制重新编译",
                "这将清除所有编译缓存并重新编译所有脚本。\n" +
                "这可能需要一些时间，确定要继续吗？",
                "确定", "取消"))
            {
                HotReloadLogic.ForceRecompileAll();
                if (_settings.ShowNotifications)
                {
                    HotReloadLogic.ShowCompilationNotification("已强制重新编译所有脚本");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();

        if (EditorApplication.isCompiling)
        {
            EditorGUILayout.HelpBox("正在编译中，请稍候...", MessageType.Info);
        }
        else if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("播放模式中，无法编译", MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        // 高级选项
        _showAdvancedOptions = EditorGUILayout.Foldout(_showAdvancedOptions, "高级选项", true);
        if (_showAdvancedOptions)
        {
            EditorGUI.indentLevel++;
            
            EditorGUI.BeginChangeCheck();
            
            _settings.AutoRecompileOnScriptChange = EditorGUILayout.Toggle(
                "脚本变化时自动重新编译", _settings.AutoRecompileOnScriptChange
            );
            
            _settings.ShowCompilationStatus = EditorGUILayout.Toggle(
                "显示编译状态", _settings.ShowCompilationStatus
            );
            
            _settings.ShowNotifications = EditorGUILayout.Toggle(
                "显示通知", _settings.ShowNotifications
            );
            
            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
            }
        }

        EditorGUILayout.Space(5);

        // 使用说明
        EditorGUILayout.HelpBox(
            "💡 使用提示：\n" +
            "• 启用快速进入播放模式后，修改代码后无需等待 Domain Reload\n" +
            "• 在播放模式下修改代码后，点击重新编译即可生效\n" +
            "• 注意：静态变量和静态构造函数不会重置",
            MessageType.None
        );
    }

    private void DrawCompilationStatus()
    {
        var status = HotReloadLogic.GetCompilationStatus();
        var statusText = HotReloadLogic.GetCompilationStatusText();
        var statusColor = HotReloadLogic.GetCompilationStatusColor();
        var lastCompileTime = HotReloadLogic.GetLastCompileTimeText();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("编译状态:", GUILayout.Width(80));
        
        Color originalColor = GUI.color;
        GUI.color = statusColor;
        EditorGUILayout.LabelField(statusText, EditorStyles.boldLabel);
        GUI.color = originalColor;
        
        EditorGUILayout.EndHorizontal();

        if (status == HotReloadLogic.CompilationStatus.Idle)
        {
            EditorGUILayout.LabelField($"上次编译: {lastCompileTime}", EditorStyles.miniLabel);
        }

        // 显示编译进度条（如果正在编译）
        if (status == HotReloadLogic.CompilationStatus.Compiling)
        {
            Rect progressRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, 0.5f, "编译中...");
        }

        EditorGUILayout.EndVertical();
    }

    private void ApplySettings()
    {
        HotReloadLogic.ApplyEnterPlayModeOptions(_settings);
    }

    private void LoadSettings()
    {
        // 先尝试从当前 Unity 设置读取
        var currentSettings = HotReloadLogic.GetCurrentSettings();
        _settings.EnableEnterPlayModeOptions = currentSettings.EnableEnterPlayModeOptions;
        _settings.DisableDomainReload = currentSettings.DisableDomainReload;
        _settings.DisableSceneReload = currentSettings.DisableSceneReload;

        // 然后从 EditorPrefs 读取其他设置
        _settings.AutoRecompileOnScriptChange = ToolboxSettings.GetBool(
            SETTINGS_KEY_PREFIX + "AutoRecompileOnScriptChange", false
        );
        _settings.ShowCompilationStatus = ToolboxSettings.GetBool(
            SETTINGS_KEY_PREFIX + "ShowCompilationStatus", true
        );
        _settings.ShowNotifications = ToolboxSettings.GetBool(
            SETTINGS_KEY_PREFIX + "ShowNotifications", true
        );
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetBool(
            SETTINGS_KEY_PREFIX + "AutoRecompileOnScriptChange",
            _settings.AutoRecompileOnScriptChange
        );
        ToolboxSettings.SetBool(
            SETTINGS_KEY_PREFIX + "ShowCompilationStatus",
            _settings.ShowCompilationStatus
        );
        ToolboxSettings.SetBool(
            SETTINGS_KEY_PREFIX + "ShowNotifications",
            _settings.ShowNotifications
        );
    }

    private bool DrawIconButton(string text, string iconName, Color buttonColor, float height)
    {
        Color originalBgColor = GUI.backgroundColor;
        Color originalContentColor = GUI.contentColor;

        GUI.backgroundColor = buttonColor * 0.8f;
        GUI.contentColor = Color.white;

        GUIContent buttonContent = IconHelper.GetIconContent(iconName, text);

        GUIStyle buttonStyle = ToolboxStyles.ButtonStyle(buttonColor);

        bool clicked = GUILayout.Button(buttonContent, buttonStyle, GUILayout.Height(height));

        GUI.backgroundColor = originalBgColor;
        GUI.contentColor = originalContentColor;

        return clicked;
    }
}

