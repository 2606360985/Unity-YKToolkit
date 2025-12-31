using UnityEngine;
using UnityEditor;

/// <summary>
/// 智能批量材质生成模块 - UI部分
/// </summary>
public class MaterialGeneratorModule : ToolModule
{
    public override string Name => "智能批量材质生成";
    public override string Category => "Assets";
    public override int Order => 0;
    public override string IconName => "d_Material";
    public override Color HeaderColor => new Color(0.2f, 0.6f, 1f);
    public override Color BackgroundColor => new Color(0.4f, 0.7f, 1f);

    public override bool IsAvailable(ToolContext context)
    {
        // 只要有选中对象就可以（可能是文件夹或贴图）
        return context.HasSelectedObjectsAll;
    }

    public override void OnGUI(ToolContext context)
    {
        EditorGUILayout.HelpBox(
            "操作：在 Project 窗口选中【贴图文件夹】或【多张贴图】，点击下方按钮。\n" +
            "系统会自动根据文件名关键词匹配 Albedo/Normal/Mask/Height 并生成材质。\n\n" +
            "📋 标准命名规范（支持大小写不敏感）：\n" +
            "• Albedo: _BaseMap, _Albedo, _Diffuse, _Color, _BaseColor, _MainTex 等\n" +
            "• Normal: _Normal, _NormalMap, _Bump, _BumpMap 等\n" +
            "• Mask: _MaskMap, _Metallic, _Roughness, _AO, _MetallicGlossMap 等\n" +
            "• Height: _Height, _HeightMap, _Displacement, _ParallaxMap 等\n\n" +
            "💡 示例：Stone_Albedo.png + Stone_Normal.png → Stone_Mat.mat\n" +
            "   支持格式：前缀_类型、前缀类型、类型（无前缀）等",
            MessageType.Info);

        if (DrawIconButton("✨ 一键识别并生成材质", IconName, HeaderColor, 40))
        {
            if (context.HasSelectedObjectsAll)
            {
                EditorUtility.DisplayProgressBar("生成材质", "正在处理贴图...", 0f);
                try
                {
                    MaterialGeneratorLogic.CreateMaterialsFromSelection(context.SelectedObjectsAll);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请在 Project 窗口中选择贴图或包含贴图的文件夹！", "确定");
            }
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
}

