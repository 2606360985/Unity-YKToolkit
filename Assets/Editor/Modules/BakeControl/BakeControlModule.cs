using UnityEngine;
using UnityEditor;

/// <summary>
/// 烘焙精度双档切换模块 - UI部分
/// </summary>
public class BakeControlModule : ToolModule
{
    public override string Name => "烘焙精度双档切换";
    public override string Category => "Lighting";
    public override int Order => 11;
    public override string IconName => "d_Lighting";
    public override Color HeaderColor => new Color(0.7f, 0.9f, 0.4f);
    public override Color BackgroundColor => new Color(0.8f, 0.9f, 0.5f);

    private BakeControlLogic.Settings _settings = new BakeControlLogic.Settings();
    private bool _showAdvancedSettings = false;
    private bool _autoEstimateTime = true;
    private bool _showBakeProgress = false;
    private float _bakeProgress = 0f;
    private string _bakeStatus = "";
    private const string SETTINGS_KEY_PREFIX = "BakeControl_";

    public override void OnInitialize()
    {
        LoadSettings();
    }

    public override void OnGUI(ToolContext context)
    {
        // 更新进度（每帧调用）
        UpdateBakeProgress();

        EditorGUILayout.HelpBox("提示：现代 Unity 必须在 Lighting 窗口先创建 'Lighting Settings' 资产才能生效。", MessageType.Info);

        // 打开 Lighting 窗口按钮
        if (DrawIconButton("🔧 打开 Lighting 窗口", "d_Lighting", new Color(0.4f, 0.7f, 1f), 30))
        {
            OpenLightingWindow();
        }

        // 模式切换和预设选择
        EditorGUILayout.BeginHorizontal();
        Color originalColor = GUI.color;
        GUI.color = _settings.IsPreviewMode ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.5f, 0.5f);
        Texture2D lightingIcon = IconHelper.GetIconSafely("d_Lighting");
        GUIContent modeContent = new GUIContent(_settings.IsPreviewMode ? "预览模式" : "生产模式", lightingIcon);
        if (GUILayout.Button(modeContent, GUILayout.Height(30)))
        {
            _settings.IsPreviewMode = !_settings.IsPreviewMode;
            BakeControlLogic.ApplyPreset(_settings);
            SaveSettings();
        }
        GUI.color = originalColor;

        _settings.SelectedPreset = EditorGUILayout.Popup(_settings.SelectedPreset, BakeControlLogic.PresetNames, 
            GUILayout.Width(120), GUILayout.Height(30));
        
        if (DrawIconButton("应用预设", "d_Refresh", new Color(0.7f, 0.9f, 0.4f), 30))
        {
            BakeControlLogic.ApplyPreset(_settings);
            SaveSettings();
        }
        EditorGUILayout.EndHorizontal();

        // 高级设置折叠
        _showAdvancedSettings = EditorGUILayout.Foldout(_showAdvancedSettings, "参数微调 (当前模式)");
        if (_showAdvancedSettings)
        {
            EditorGUILayout.BeginVertical("box");
            _settings.DirectSamples = EditorGUILayout.IntSlider("Direct Samples", _settings.DirectSamples, 1, 1024);
            _settings.IndirectSamples = EditorGUILayout.IntSlider("Indirect Samples", _settings.IndirectSamples, 1, 4096);
            _settings.EnvSamples = EditorGUILayout.IntSlider("Env Samples", _settings.EnvSamples, 1, 1024);
            _settings.Bounces = EditorGUILayout.IntSlider("Bounces", _settings.Bounces, 0, 4);
            EditorGUILayout.EndVertical();
        }

        // 时间预估
        if (_autoEstimateTime)
        {
            string estimate = BakeControlLogic.EstimateBakeTime(_settings);
            EditorGUILayout.HelpBox($"⏱ 预计时长: {estimate}", MessageType.None);
        }

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        if (DrawIconButton("💾 写入设置到资产", "d_SaveAs", new Color(0.2f, 0.7f, 0.9f), 35))
        {
            if (BakeControlLogic.ApplySettingsToAsset(_settings))
            {
                if (_settings.StartBakeAfterSwitch)
                {
                    StartBake();
                }
                SaveSettings();
            }
        }
        if (DrawIconButton("🔥 立即开始烘焙", "d_Lighting", new Color(0.7f, 0.9f, 0.4f), 35))
        {
            StartBake();
        }
        EditorGUILayout.EndHorizontal();

        _settings.StartBakeAfterSwitch = EditorGUILayout.Toggle("写入后立即烘焙", _settings.StartBakeAfterSwitch);

        // 进度条
        if (_showBakeProgress)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(r, _bakeProgress, _bakeStatus);
        }
    }

    private void StartBake()
    {
        if (BakeControlLogic.StartBake(_settings))
        {
            _showBakeProgress = true;
        }
    }

    private void UpdateBakeProgress()
    {
        if (BakeControlLogic.IsBaking())
        {
            _bakeProgress = BakeControlLogic.GetBakeProgress();
            _bakeProgress = Mathf.Clamp01(_bakeProgress);
            _bakeStatus = $"正在烘焙: {Mathf.RoundToInt(_bakeProgress * 100)}%";
            if (_bakeProgress < 0) _bakeStatus = "正在准备...";
            _showBakeProgress = true;
        }
        else if (_showBakeProgress)
        {
            _bakeProgress = 1f;
            _bakeStatus = "烘焙完成!";
            
            // 延迟隐藏进度条（使用协程或延迟任务）
            if (!_isHidingProgress)
            {
                _isHidingProgress = true;
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(t => 
                {
                    _showBakeProgress = false;
                    _isHidingProgress = false;
                });
            }
        }
    }

    private bool _isHidingProgress = false;

    /// <summary>
    /// 打开 Unity Lighting 窗口
    /// </summary>
    private void OpenLightingWindow()
    {
        // 尝试多种方式打开 Lighting 窗口（兼容不同 Unity 版本）
        // Unity 2019+ 使用 "Window/Rendering/Lighting"
        // Unity 2018 及更早版本可能使用 "Window/Lighting"
        if (!EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting"))
        {
            // 如果失败，尝试旧版本的菜单路径
            EditorApplication.ExecuteMenuItem("Window/Lighting");
        }
    }

    private bool DrawIconButton(string text, string iconName, Color buttonColor, float height)
    {
        Color originalBgColor = GUI.backgroundColor;
        GUI.backgroundColor = buttonColor * 0.8f;
        GUI.contentColor = Color.white;

        GUIContent buttonContent = IconHelper.GetIconContent(iconName, text);
        GUIStyle buttonStyle = ToolboxStyles.ButtonStyle(buttonColor);

        bool clicked = GUILayout.Button(buttonContent, buttonStyle, GUILayout.Height(height));

        GUI.backgroundColor = originalBgColor;
        GUI.contentColor = Color.white;
        return clicked;
    }

    private void LoadSettings()
    {
        _settings.IsPreviewMode = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "IsPreviewMode", true);
        _settings.SelectedPreset = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "SelectedPreset", 0);
        _settings.DirectSamples = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "DirectSamples", 16);
        _settings.IndirectSamples = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "IndirectSamples", 64);
        _settings.EnvSamples = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "EnvSamples", 64);
        _settings.Bounces = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "Bounces", 2);
        _settings.StartBakeAfterSwitch = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "StartBakeAfterSwitch", false);
        _showAdvancedSettings = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "ShowAdvancedSettings", false);
        _autoEstimateTime = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "AutoEstimateTime", true);
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "IsPreviewMode", _settings.IsPreviewMode);
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "SelectedPreset", _settings.SelectedPreset);
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "DirectSamples", _settings.DirectSamples);
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "IndirectSamples", _settings.IndirectSamples);
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "EnvSamples", _settings.EnvSamples);
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "Bounces", _settings.Bounces);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "StartBakeAfterSwitch", _settings.StartBakeAfterSwitch);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "ShowAdvancedSettings", _showAdvancedSettings);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "AutoEstimateTime", _autoEstimateTime);
    }
}

