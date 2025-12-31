using UnityEngine;
using UnityEditor;

/// <summary>
/// 查找重复物体模块 - UI部分
/// </summary>
public class FindDuplicatesModule : ToolModule
{
    public override string Name => "查找重复物体";
    public override string Category => "Scene";
    public override int Order => 10;
    public override string IconName => "d_Search";
    public override Color HeaderColor => new Color(1f, 0.5f, 0.5f);
    public override Color BackgroundColor => new Color(1f, 0.6f, 0.6f);

    public override void OnGUI(ToolContext context)
    {
        EditorGUILayout.HelpBox("扫描场景中所有层级（包括子物体）的位置、旋转、模型完全一致的重复物体并高亮显示。", MessageType.Info);

        if (DrawIconButton("🔍 扫描并高亮重复物体", IconName, HeaderColor, 30))
        {
            EditorUtility.DisplayProgressBar("扫描重复物体", "正在扫描场景...", 0f);
            try
            {
                var result = FindDuplicatesLogic.FindDuplicateObjects();
                FindDuplicatesLogic.DisplayResult(result);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
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

