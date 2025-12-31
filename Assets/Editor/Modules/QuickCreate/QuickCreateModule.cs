using UnityEngine;
using UnityEditor;

/// <summary>
/// 快速创建助手模块 - UI部分
/// </summary>
public class QuickCreateModule : ToolModule
{
    public override string Name => "快速创建助手";
    public override string Category => "Scene";
    public override int Order => 12;
    public override string IconName => "d_ToolHandleLocal";
    public override Color HeaderColor => new Color(0.9f, 0.5f, 0.2f);
    public override Color BackgroundColor => new Color(0.9f, 0.6f, 0.4f);

    private QuickCreateLogic.Settings _settings = new QuickCreateLogic.Settings();
    private const string SETTINGS_KEY_PREFIX = "QuickCreate_";

    public override void OnInitialize()
    {
        LoadSettings();
    }

    public override void OnGUI(ToolContext context)
    {
        _settings.CreateAtSelection = EditorGUILayout.Toggle("在选中位置创建", _settings.CreateAtSelection);
        SaveSettings();

        Vector3 createPos = QuickCreateLogic.GetCreatePosition(_settings.CreateAtSelection, context.ActiveTransform);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("📦 基础模型", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawCreateButton("立方体", "d_Cube", new Color(0.4f, 0.7f, 1f), 25, () => 
            QuickCreateLogic.CreatePrimitive(PrimitiveType.Cube, createPos));
        DrawCreateButton("球体", "d_Sphere", new Color(0.4f, 0.7f, 1f), 25, () => 
            QuickCreateLogic.CreatePrimitive(PrimitiveType.Sphere, createPos));
        DrawCreateButton("平面", "d_Plane", new Color(0.4f, 0.7f, 1f), 25, () => 
            QuickCreateLogic.CreatePrimitive(PrimitiveType.Plane, createPos));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        DrawCreateButton("圆柱体", "d_Cylinder", new Color(0.4f, 0.7f, 1f), 25, () => 
            QuickCreateLogic.CreatePrimitive(PrimitiveType.Cylinder, createPos));
        DrawCreateButton("胶囊体", "d_Capsule", new Color(0.4f, 0.7f, 1f), 25, () => 
            QuickCreateLogic.CreatePrimitive(PrimitiveType.Capsule, createPos));
        DrawCreateButton("四边形", "d_Quad", new Color(0.4f, 0.7f, 1f), 25, () => 
            QuickCreateLogic.CreatePrimitive(PrimitiveType.Quad, createPos));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("💡 灯光组件", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawCreateButton("平行光", "d_DirectionalLight", new Color(1f, 0.9f, 0.3f), 25, () => 
            QuickCreateLogic.CreateLight(LightType.Directional, createPos));
        DrawCreateButton("点光源", "d_Light", new Color(1f, 0.9f, 0.3f), 25, () => 
            QuickCreateLogic.CreateLight(LightType.Point, createPos));
        DrawCreateButton("聚光灯", "d_Spotlight", new Color(1f, 0.9f, 0.3f), 25, () => 
            QuickCreateLogic.CreateLight(LightType.Spot, createPos));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🔍 探针与环境", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawCreateButton("反射探针", "d_ReflectionProbe", new Color(0.4f, 0.8f, 1f), 25, () => 
            QuickCreateLogic.CreateReflectionProbe(createPos));
        DrawCreateButton("光照探针组", "d_LightProbeGroup", new Color(0.4f, 0.8f, 1f), 25, () => 
            QuickCreateLogic.CreateLightProbeGroup(createPos));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🌐 环境配置 (URP/HDRP)", EditorStyles.miniLabel);
        DrawCreateButton("全局 Volume", "d_SceneViewFx", new Color(0.6f, 0.4f, 0.9f), 25, () => 
            QuickCreateLogic.CreateVolume(createPos));
    }

    private void DrawCreateButton(string text, string iconName, Color buttonColor, float height, System.Action action)
    {
        Color originalBgColor = GUI.backgroundColor;
        GUI.backgroundColor = buttonColor * 0.8f;
        GUI.contentColor = Color.white;

        GUIContent buttonContent = IconHelper.GetIconContent(iconName, text);
        GUIStyle buttonStyle = ToolboxStyles.ButtonStyle(buttonColor);

        if (GUILayout.Button(buttonContent, buttonStyle, GUILayout.Height(height)))
        {
            // 记录工具使用
            ToolUsageTracker.Track($"快速创建-{text}");
            action?.Invoke();
        }

        GUI.backgroundColor = originalBgColor;
        GUI.contentColor = Color.white;
    }

    private void LoadSettings()
    {
        _settings.CreateAtSelection = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "CreateAtSelection", true);
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "CreateAtSelection", _settings.CreateAtSelection);
    }
}

