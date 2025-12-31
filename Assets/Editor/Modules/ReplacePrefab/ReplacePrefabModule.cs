using UnityEngine;
using UnityEditor;

/// <summary>
/// 资产替换模块 - UI部分
/// </summary>
public class ReplacePrefabModule : ToolModule
{
    public override string Name => "资产替换";
    public override string Category => "Scene";
    public override int Order => 4;
    public override string IconName => "d_Prefab";
    public override Color HeaderColor => new Color(1f, 0.7f, 0.2f);
    public override Color BackgroundColor => new Color(1f, 0.8f, 0.4f);

    private GameObject _replacementPrefab;

    public override bool IsAvailable(ToolContext context)
    {
        return context.HasSelectedObjects;
    }

    public override void OnGUI(ToolContext context)
    {
        if (!context.HasSelectedObjects)
        {
            EditorGUILayout.HelpBox("请在 Hierarchy 窗口中选择要替换的物体。", MessageType.Info);
            return;
        }

        _replacementPrefab = (GameObject)EditorGUILayout.ObjectField("目标预制体", _replacementPrefab, typeof(GameObject), false);

        if (DrawIconButton("🔄 一键替换选中项", IconName, HeaderColor, 25))
        {
            if (_replacementPrefab != null)
            {
                ReplacePrefabLogic.ReplaceWithPrefab(context.SelectedObjects, _replacementPrefab);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先选择目标预制体！", "确定");
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

