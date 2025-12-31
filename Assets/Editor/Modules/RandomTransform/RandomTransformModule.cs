using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 随机变换模块 - UI部分
/// </summary>
public class RandomTransformModule : ToolModule
{
    public override string Name => "随机变换";
    public override string Category => "Scene";
    public override int Order => 6;
    public override string IconName => "d_RotateTool";
    public override Color HeaderColor => new Color(1f, 0.4f, 0.8f);
    public override Color BackgroundColor => new Color(1f, 0.5f, 1f);

    private RandomTransformLogic.Settings _settings = new RandomTransformLogic.Settings();
    private const string SETTINGS_KEY_PREFIX = "RandomTransform_";

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
            EditorGUILayout.HelpBox("请在 Hierarchy 窗口中选择物体。", MessageType.Info);
            return;
        }

        _settings.RandYRotation = EditorGUILayout.Toggle("随机 Y 轴旋转", _settings.RandYRotation);

        EditorGUILayout.BeginHorizontal();
        _settings.MinScale = EditorGUILayout.FloatField("Min Scale", _settings.MinScale);
        _settings.MaxScale = EditorGUILayout.FloatField("Max Scale", _settings.MaxScale);
        EditorGUILayout.EndHorizontal();

        if (DrawIconButton("🎲 应用随机效果", IconName, HeaderColor, 25))
        {
            RandomTransformLogic.ApplyRandomization(context.SelectedTransforms, _settings);
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
        _settings.MinScale = ToolboxSettings.GetFloat(SETTINGS_KEY_PREFIX + "MinScale", 0.8f);
        _settings.MaxScale = ToolboxSettings.GetFloat(SETTINGS_KEY_PREFIX + "MaxScale", 1.2f);
        _settings.RandYRotation = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "RandYRotation", true);
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetFloat(SETTINGS_KEY_PREFIX + "MinScale", _settings.MinScale);
        ToolboxSettings.SetFloat(SETTINGS_KEY_PREFIX + "MaxScale", _settings.MaxScale);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "RandYRotation", _settings.RandYRotation);
    }
}

