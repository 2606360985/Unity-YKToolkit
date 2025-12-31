using UnityEngine;
using UnityEditor;

/// <summary>
/// 批量重命名模块 - UI部分
/// </summary>
public class BatchRenameModule : ToolModule
{
    public override string Name => "批量重命名";
    public override string Category => "Assets";
    public override int Order => 3;
    public override string IconName => "d_TextAsset";
    public override Color HeaderColor => new Color(0.7f, 0.7f, 0.7f);
    public override Color BackgroundColor => new Color(0.85f, 0.85f, 0.85f);

    // 模块状态（UI相关）
    private BatchRenameLogic.BatchRenameSettings _settings = new BatchRenameLogic.BatchRenameSettings();
    private const string SETTINGS_KEY_PREFIX = "BatchRename_";

    public override void OnInitialize()
    {
        LoadSettings();
    }

    public override bool IsAvailable(ToolContext context)
    {
        return context.HasSelectedObjectsAll;
    }

    public override void OnGUI(ToolContext context)
    {
        if (!context.HasSelectedObjectsAll)
        {
            EditorGUILayout.HelpBox("请在 Hierarchy 或 Project 窗口中选择要重命名的对象。", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox($"已选中 {context.SelectedObjectsAll.Length} 个对象", MessageType.None);

        // UI控件
        _settings.ReplaceAll = EditorGUILayout.Toggle("完全替换名", _settings.ReplaceAll);
        
        if (_settings.ReplaceAll)
        {
            _settings.Base = EditorGUILayout.TextField("基础名", _settings.Base);
        }

        EditorGUILayout.BeginHorizontal();
        _settings.Prefix = EditorGUILayout.TextField("前缀", _settings.Prefix);
        _settings.Suffix = EditorGUILayout.TextField("后缀", _settings.Suffix);
        EditorGUILayout.EndHorizontal();

        _settings.StartIndex = EditorGUILayout.IntField("起始编号", _settings.StartIndex);
        _settings.Digits = EditorGUILayout.IntSlider("编号位数", _settings.Digits, 1, 5);

        EditorGUILayout.Space(5);

        // 执行按钮
        if (DrawIconButton("📝 执行批量重命名", IconName, HeaderColor, 25))
        {
            if (BatchRenameLogic.ValidateSettings(_settings))
            {
                // 记录工具使用
                ToolUsageTracker.Track("批量重命名");
                
                // 记录慢操作（如果耗时超过阈值）
                ToolUsageTracker.TrackSlowOperation("批量重命名", () =>
                {
                    BatchRenameLogic.ExecuteBatchRename(context.SelectedObjectsAll, _settings);
                }, $"重命名 {context.SelectedObjectsAll.Length} 个对象");
                
                SaveSettings();
            }
        }
    }

    private bool DrawIconButton(string text, string iconName, Color buttonColor, float height)
    {
        Color originalBgColor = GUI.backgroundColor;
        Color originalContentColor = GUI.contentColor;

        GUI.backgroundColor = buttonColor * 0.8f;
        GUI.contentColor = Color.white;

        GUIContent buttonContent = IconHelper.GetIconContent(iconName, text);

        GUIStyle buttonStyle = ToolboxStyles.ButtonStyle(buttonColor);

        bool clicked = GUILayout.Button(buttonContent, buttonStyle, GUILayout.Height(height));

        GUI.backgroundColor = originalBgColor;
        GUI.contentColor = originalContentColor;

        return clicked;
    }

    private void LoadSettings()
    {
        _settings.Prefix = ToolboxSettings.GetString(SETTINGS_KEY_PREFIX + "Prefix", "");
        _settings.Base = ToolboxSettings.GetString(SETTINGS_KEY_PREFIX + "Base", "Object");
        _settings.Suffix = ToolboxSettings.GetString(SETTINGS_KEY_PREFIX + "Suffix", "");
        _settings.StartIndex = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "StartIndex", 0);
        _settings.Digits = ToolboxSettings.GetInt(SETTINGS_KEY_PREFIX + "Digits", 2);
        _settings.ReplaceAll = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "ReplaceAll", true);
    }

    private void SaveSettings()
    {
        ToolboxSettings.SetString(SETTINGS_KEY_PREFIX + "Prefix", _settings.Prefix);
        ToolboxSettings.SetString(SETTINGS_KEY_PREFIX + "Base", _settings.Base);
        ToolboxSettings.SetString(SETTINGS_KEY_PREFIX + "Suffix", _settings.Suffix);
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "StartIndex", _settings.StartIndex);
        ToolboxSettings.SetInt(SETTINGS_KEY_PREFIX + "Digits", _settings.Digits);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "ReplaceAll", _settings.ReplaceAll);
    }
}

