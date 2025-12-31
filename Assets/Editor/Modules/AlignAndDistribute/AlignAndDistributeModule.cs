using UnityEngine;
using UnityEditor;

/// <summary>
/// 快速对齐与等距分布模块 - UI部分
/// </summary>
public class AlignAndDistributeModule : ToolModule
{
    public override string Name => "快速对齐与等距分布";
    public override string Category => "Scene";
    public override int Order => 8;
    public override string IconName => "d_Grid";
    public override Color HeaderColor => new Color(0.3f, 0.6f, 1f);
    public override Color BackgroundColor => new Color(0.6f, 0.8f, 1f);

    private AlignAndDistributeLogic.Settings _settings = new AlignAndDistributeLogic.Settings();
    private const string SETTINGS_KEY_PREFIX = "AlignAndDistribute_";

    public override void OnInitialize()
    {
        LoadSettings();
    }

    public override bool IsAvailable(ToolContext context)
    {
        return context.HasSelectedTransforms && context.SelectedTransforms.Length >= 2;
    }

    public override void OnGUI(ToolContext context)
    {
        if (!context.HasSelectedTransforms || context.SelectedTransforms.Length < 2)
        {
            EditorGUILayout.HelpBox("请至少选中 2 个物体！", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox("选中多个物体，按轴方向对齐或等距分布。", MessageType.Info);

        _settings.Axis = EditorGUILayout.Popup("对齐轴", _settings.Axis, new string[] { "X 轴", "Y 轴", "Z 轴" });
        _settings.IsDistributeMode = EditorGUILayout.Toggle("等距分布模式", _settings.IsDistributeMode);

        string buttonText = _settings.IsDistributeMode ? "📏 执行等距分布" : "📐 执行对齐";
        if (DrawIconButton(buttonText, IconName, HeaderColor, 30))
        {
            AlignAndDistributeLogic.AlignAndDistribute(context.SelectedTransforms, _settings);
            SaveSettings();
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
        _settings.Axis = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "Axis", 0);
        _settings.IsDistributeMode = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "IsDistributeMode", false);
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "Axis", _settings.Axis);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "IsDistributeMode", _settings.IsDistributeMode);
    }
}

