using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 物理对齐模块 - UI部分
/// </summary>
public class AlignToGroundModule : ToolModule
{
    public override string Name => "物理对齐";
    public override string Category => "Scene";
    public override int Order => 2;
    public override string IconName => "d_MoveTool";
    public override Color HeaderColor => new Color(0.2f, 0.8f, 0.3f);
    public override Color BackgroundColor => new Color(0.5f, 1f, 0.5f);

    private AlignToGroundLogic.Settings _settings = new AlignToGroundLogic.Settings();
    private const string SETTINGS_KEY_PREFIX = "AlignToGround_";

    public override void OnInitialize()
    {
        LoadSettings();
    }

    public override bool IsAvailable(ToolContext context)
    {
        return context.HasSelectedTransforms;
    }

    public override void OnGUI(ToolContext context)
    {
        if (!context.HasSelectedTransforms)
        {
            EditorGUILayout.HelpBox("请在 Hierarchy 窗口中选择要对齐的物体。", MessageType.Info);
            return;
        }

        _settings.GroundLayerMask = SceneUtil.LayerMaskField("地面层级", _settings.GroundLayerMask);

        if (DrawIconButton("⬇️ 一键对齐地面", IconName, HeaderColor, 30))
        {
            AlignToGroundLogic.SnapToGround(context.SelectedTransforms, _settings);
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
        _settings.GroundLayerMask = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "GroundLayerMask", -1);
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "GroundLayerMask", _settings.GroundLayerMask);
    }
}

