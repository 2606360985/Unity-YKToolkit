using UnityEngine;
using UnityEditor;

/// <summary>
/// 批量静态设置模块 - UI部分
/// </summary>
public class BatchStaticModule : ToolModule
{
    public override string Name => "批量静态设置";
    public override string Category => "Scene";
    public override int Order => 9;
    public override string IconName => "d_Static";
    public override Color HeaderColor => new Color(0.9f, 0.6f, 0.3f);
    public override Color BackgroundColor => new Color(0.9f, 0.7f, 0.5f);

    private BatchStaticLogic.Settings _settings = new BatchStaticLogic.Settings();
    private const string SETTINGS_KEY_PREFIX = "BatchStatic_";

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
            EditorGUILayout.HelpBox("请至少选中 1 个物体！", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox("批量设置选中物体的静态标志位，用于场景优化和光照烘焙。", MessageType.Info);

        _settings.ContributeGI = EditorGUILayout.Toggle("Contribute GI", _settings.ContributeGI);
        _settings.ReflectionProbeStatic = EditorGUILayout.Toggle("Reflection Probe Static", _settings.ReflectionProbeStatic);
        _settings.OccluderStatic = EditorGUILayout.Toggle("Occluder Static", _settings.OccluderStatic);
        _settings.OccludeeStatic = EditorGUILayout.Toggle("Occludee Static", _settings.OccludeeStatic);
        _settings.BatchingStatic = EditorGUILayout.Toggle("Batching Static", _settings.BatchingStatic);
        _settings.NavigationStatic = EditorGUILayout.Toggle("Navigation Static", _settings.NavigationStatic);
        _settings.OffMeshLinkGeneration = EditorGUILayout.Toggle("Off Mesh Link Generation", _settings.OffMeshLinkGeneration);

        EditorGUILayout.Space(5);

        if (DrawIconButton("⚙️ 应用静态设置到选中物体", IconName, HeaderColor, 30))
        {
            BatchStaticLogic.ApplyStaticFlags(context.SelectedObjects, _settings);
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
        _settings.ContributeGI = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "ContributeGI", true);
        _settings.ReflectionProbeStatic = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "ReflectionProbeStatic", true);
        _settings.OccluderStatic = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "OccluderStatic", false);
        _settings.OccludeeStatic = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "OccludeeStatic", false);
        _settings.BatchingStatic = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "BatchingStatic", false);
        _settings.NavigationStatic = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "NavigationStatic", false);
        _settings.OffMeshLinkGeneration = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "OffMeshLinkGeneration", false);
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "ContributeGI", _settings.ContributeGI);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "ReflectionProbeStatic", _settings.ReflectionProbeStatic);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "OccluderStatic", _settings.OccluderStatic);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "OccludeeStatic", _settings.OccludeeStatic);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "BatchingStatic", _settings.BatchingStatic);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "NavigationStatic", _settings.NavigationStatic);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "OffMeshLinkGeneration", _settings.OffMeshLinkGeneration);
    }
}

