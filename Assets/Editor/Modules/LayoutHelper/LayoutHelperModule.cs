using UnityEngine;
using UnityEditor;

/// <summary>
/// 布局助手模块 - UI部分
/// </summary>
public class LayoutHelperModule : ToolModule
{
    public override string Name => "布局助手";
    public override string Category => "Scene";
    public override int Order => 5;
    public override string IconName => "d_Grid";
    public override Color HeaderColor => new Color(0.3f, 0.8f, 0.9f);
    public override Color BackgroundColor => new Color(0.7f, 1f, 1f);

    private LayoutHelperLogic.Settings _settings = new LayoutHelperLogic.Settings();
    private const string SETTINGS_KEY_PREFIX = "LayoutHelper_";

    public override void OnInitialize()
    {
        LoadSettings();
    }

    public override bool IsAvailable(ToolContext context)
    {
        return context.HasSelectedObjects;
    }

    public override void OnGUI(ToolContext context)
    {
        if (!context.HasSelectedObjects)
        {
            EditorGUILayout.HelpBox("请在 Hierarchy 窗口中选择物体。", MessageType.Info);
            return;
        }

        _settings.DuplicateOffset = EditorGUILayout.Vector3Field("阵列偏移量", _settings.DuplicateOffset);

        if (DrawIconButton("📋 偏移复制并移动", "d_TreeEditor.Duplicate", HeaderColor, 25))
        {
            if (context.ActiveGameObject != null)
            {
                LayoutHelperLogic.DuplicateWithOffset(context.ActiveGameObject, _settings.DuplicateOffset);
                SaveSettings();
            }
        }

        EditorGUILayout.Space(2);

        if (DrawIconButton("📁 快速打组", "d_Folder", HeaderColor, 25))
        {
            LayoutHelperLogic.QuickGroup(context.SelectedTransforms);
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
        _settings.DuplicateOffset = new Vector3(
            ToolboxSettings.GetFloat(SETTINGS_KEY_PREFIX + "OffsetX", 2f),
            ToolboxSettings.GetFloat(SETTINGS_KEY_PREFIX + "OffsetY", 0f),
            ToolboxSettings.GetFloat(SETTINGS_KEY_PREFIX + "OffsetZ", 0f)
        );
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetFloat(SETTINGS_KEY_PREFIX + "OffsetX", _settings.DuplicateOffset.x);
        ToolboxSettings.SetFloat(SETTINGS_KEY_PREFIX + "OffsetY", _settings.DuplicateOffset.y);
        ToolboxSettings.SetFloat(SETTINGS_KEY_PREFIX + "OffsetZ", _settings.DuplicateOffset.z);
    }
}

