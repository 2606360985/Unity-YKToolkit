using UnityEngine;
using UnityEditor;

/// <summary>
/// 系统清理模块 - UI部分
/// </summary>
public class SystemCleanupModule : ToolModule
{
    public override string Name => "系统清理";
    public override string Category => "Tools";
    public override int Order => 7;
    public override string IconName => "d_Settings";
    public override Color HeaderColor => new Color(0.5f, 0.5f, 0.5f);
    public override Color BackgroundColor => Color.gray;

    private GameObject[] lastFoundMissingScripts = null;

    public override void OnGUI(ToolContext context)
    {
        if (DrawIconButton("⚠️ 查找场景 Missing Scripts", "d_console.warnicon", new Color(0.9f, 0.7f, 0.2f), 25))
        {
            lastFoundMissingScripts = SystemCleanupLogic.FindMissingScripts();
            int count = lastFoundMissingScripts.Length;
            if (count > 0)
            {
                EditorUtility.DisplayDialog("完成", 
                    $"发现 {count} 个缺失脚本物体。\n\n" +
                    "可以使用下方的\"定位\"按钮选中这些物体，\n" +
                    "或使用\"一键移除\"按钮删除所有缺失脚本组件。", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("完成", "未发现缺失脚本物体。", "OK");
            }
        }

        // 定位功能：选中所有找到的缺失脚本物体
        if (lastFoundMissingScripts != null && lastFoundMissingScripts.Length > 0)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (DrawIconButton("📍 定位缺失脚本物体", "d_ViewToolZoom", new Color(0.4f, 0.8f, 1f), 25))
            {
                EditorSelectionUtil.SetSelection(lastFoundMissingScripts);
                // 聚焦到第一个物体
                if (lastFoundMissingScripts.Length > 0)
                {
                    Selection.activeGameObject = lastFoundMissingScripts[0];
                    EditorGUIUtility.PingObject(lastFoundMissingScripts[0]);
                }
                Debug.Log($"<color=cyan>已选中 {lastFoundMissingScripts.Length} 个缺失脚本物体</color>");
            }

            if (DrawIconButton("🗑️ 一键移除所有缺失脚本", "d_TreeEditor.Trash", new Color(1f, 0.4f, 0.4f), 25))
            {
                int totalCount = lastFoundMissingScripts.Length;
                if (EditorUtility.DisplayDialog("警告", 
                    $"确定要移除所有 {totalCount} 个物体上的缺失脚本组件吗？\n\n" +
                    "此操作不可撤销！", 
                    "确定移除", "取消"))
                {
                    int removedCount = SystemCleanupLogic.RemoveAllMissingScripts();
                    EditorUtility.DisplayDialog("完成", 
                        $"已移除 {removedCount} 个缺失脚本组件。\n\n" +
                        "请重新查找以确认清理结果。", 
                        "OK");
                    lastFoundMissingScripts = null; // 清空缓存，需要重新查找
                }
            }

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox($"当前找到 {lastFoundMissingScripts.Length} 个缺失脚本物体", MessageType.Info);
        }

        if (DrawIconButton("🗑️ 清空所有本地缓存", "d_Refresh", new Color(0.7f, 0.7f, 0.7f), 25))
        {
            if (EditorUtility.DisplayDialog("警告", "清空PlayerPrefs？", "是", "否"))
            {
                SystemCleanupLogic.ClearCache();
            }
        }

        if (context.ActiveGameObject != null)
        {
            if (DrawIconButton("🏷️ 一键选中同 Tag 物体", "d_FilterByLabel", new Color(0.6f, 0.8f, 1f), 25))
            {
                string tag = context.ActiveGameObject.tag;
                GameObject[] objects = SystemCleanupLogic.SelectByTag(tag);
                EditorSelectionUtil.SetSelection(objects);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("请先选择一个物体以使用\"标签选择\"功能。", MessageType.Info);
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

