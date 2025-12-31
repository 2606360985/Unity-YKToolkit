using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 项目层级与文件夹视觉管理模块 - UI部分
/// </summary>
public class VisualHierarchyModule : ToolModule
{
    public override string Name => "项目层级与文件夹视觉管理";
    public override string Category => "Assets";
    public override int Order => 20;
    public override string IconName => "d_Folder";
    public override Color HeaderColor => new Color(0.4f, 0.7f, 0.9f);
    public override Color BackgroundColor => new Color(0.6f, 0.8f, 1f);

    private List<VisualHierarchyLogic.FolderColorConfig> _folderColors;
    private List<VisualHierarchyLogic.HierarchyHighlightConfig> _hierarchyHighlights;
    private List<VisualHierarchyLogic.ProjectFolderPreset> _folderPresets;

    private Vector2 _folderScrollPos;
    private Vector2 _hierarchyScrollPos;
    private Vector2 _presetScrollPos;

    private bool _showFolderColors = true;
    private bool _showHierarchyHighlights = true;
    private bool _showFolderPresets = true;

    private bool _hierarchyHighlightEnabled = true;
    private bool _folderColorEnabled = true;

    private const string SETTINGS_KEY_PREFIX = "VisualHierarchy_";

    public override void OnInitialize()
    {
        LoadSettings();
        RegisterHierarchyCallback();
        RegisterSceneCreatedCallback();
    }

    public override void OnCleanup()
    {
        UnregisterHierarchyCallback();
        UnregisterSceneCreatedCallback();
    }

    public override void OnGUI(ToolContext context)
    {
        EditorGUILayout.HelpBox(
            "视觉锚点：给文件夹标上颜色和图标，利用视觉记忆快速定位文件。\n" +
            "层级高亮：在 Hierarchy 窗口中高亮重要对象（如 Manager、Player）。\n" +
            "文件夹预设：创建新场景时自动添加通用项目文件夹结构。",
            MessageType.Info);

        EditorGUILayout.Space(5);

        // 总开关
        EditorGUILayout.BeginHorizontal("box");
        _folderColorEnabled = EditorGUILayout.Toggle("启用文件夹颜色标记", _folderColorEnabled);
        _hierarchyHighlightEnabled = EditorGUILayout.Toggle("启用Hierarchy高亮", _hierarchyHighlightEnabled);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 文件夹颜色配置
        _showFolderColors = EditorGUILayout.Foldout(_showFolderColors, "📁 文件夹颜色配置", true);
        if (_showFolderColors)
        {
            DrawFolderColorsSection();
        }

        EditorGUILayout.Space(5);

        // Hierarchy高亮配置
        _showHierarchyHighlights = EditorGUILayout.Foldout(_showHierarchyHighlights, "🎯 Hierarchy高亮配置", true);
        if (_showHierarchyHighlights)
        {
            DrawHierarchyHighlightsSection();
        }

        EditorGUILayout.Space(5);

        // 项目文件夹预设
        _showFolderPresets = EditorGUILayout.Foldout(_showFolderPresets, "📦 项目文件夹预设", true);
        if (_showFolderPresets)
        {
            DrawFolderPresetsSection();
        }
    }

    private void DrawFolderColorsSection()
    {
        EditorGUILayout.BeginVertical("box");

        if (_folderColors == null || _folderColors.Count == 0)
        {
            if (GUILayout.Button("加载默认配置"))
            {
                _folderColors = VisualHierarchyLogic.GetDefaultFolderColors();
                SaveSettings();
            }
        }
        else
        {
            _folderScrollPos = EditorGUILayout.BeginScrollView(_folderScrollPos, GUILayout.Height(200));

            for (int i = 0; i < _folderColors.Count; i++)
            {
                EditorGUILayout.BeginHorizontal("box");

                _folderColors[i].enabled = EditorGUILayout.Toggle(_folderColors[i].enabled, GUILayout.Width(20));

                EditorGUILayout.LabelField("文件夹名:", GUILayout.Width(70));
                _folderColors[i].folderName = EditorGUILayout.TextField(_folderColors[i].folderName, GUILayout.Width(100));

                EditorGUILayout.LabelField("颜色:", GUILayout.Width(40));
                _folderColors[i].color = EditorGUILayout.ColorField(_folderColors[i].color, GUILayout.Width(60));

                EditorGUILayout.LabelField("图标:", GUILayout.Width(40));
                _folderColors[i].iconName = EditorGUILayout.TextField(_folderColors[i].iconName, GUILayout.Width(100));

                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    _folderColors.RemoveAt(i);
                    SaveSettings();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加配置"))
            {
                _folderColors.Add(new VisualHierarchyLogic.FolderColorConfig("NewFolder", Color.white));
                SaveSettings();
            }
            if (GUILayout.Button("应用配置"))
            {
                if (_folderColorEnabled)
                {
                    VisualHierarchyLogic.ApplyFolderColors(_folderColors);
                    ProjectWindowDrawer.RefreshConfigs(); // 刷新Project窗口绘制器
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请先启用文件夹颜色标记功能", "确定");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHierarchyHighlightsSection()
    {
        EditorGUILayout.BeginVertical("box");

        if (_hierarchyHighlights == null || _hierarchyHighlights.Count == 0)
        {
            if (GUILayout.Button("加载默认配置"))
            {
                _hierarchyHighlights = VisualHierarchyLogic.GetDefaultHierarchyHighlights();
                SaveSettings();
            }
        }
        else
        {
            _hierarchyScrollPos = EditorGUILayout.BeginScrollView(_hierarchyScrollPos, GUILayout.Height(200));

            for (int i = 0; i < _hierarchyHighlights.Count; i++)
            {
                EditorGUILayout.BeginHorizontal("box");

                _hierarchyHighlights[i].enabled = EditorGUILayout.Toggle(_hierarchyHighlights[i].enabled, GUILayout.Width(20));

                EditorGUILayout.LabelField("名称模式:", GUILayout.Width(70));
                _hierarchyHighlights[i].namePattern = EditorGUILayout.TextField(_hierarchyHighlights[i].namePattern, GUILayout.Width(100));
                EditorGUILayout.HelpBox("*表示通配符", MessageType.None);

                EditorGUILayout.LabelField("背景色:", GUILayout.Width(50));
                _hierarchyHighlights[i].backgroundColor = EditorGUILayout.ColorField(_hierarchyHighlights[i].backgroundColor, GUILayout.Width(60));

                EditorGUILayout.LabelField("文字色:", GUILayout.Width(50));
                _hierarchyHighlights[i].textColor = EditorGUILayout.ColorField(_hierarchyHighlights[i].textColor, GUILayout.Width(60));

                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    _hierarchyHighlights.RemoveAt(i);
                    SaveSettings();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加配置"))
            {
                _hierarchyHighlights.Add(new VisualHierarchyLogic.HierarchyHighlightConfig("*New*", Color.white, Color.black));
                SaveSettings();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawFolderPresetsSection()
    {
        EditorGUILayout.BeginVertical("box");

        if (_folderPresets == null || _folderPresets.Count == 0)
        {
            if (GUILayout.Button("加载默认预设"))
            {
                _folderPresets = VisualHierarchyLogic.GetDefaultFolderPresets();
                SaveSettings();
            }
        }
        else
        {
            _presetScrollPos = EditorGUILayout.BeginScrollView(_presetScrollPos, GUILayout.Height(150));

            foreach (var preset in _folderPresets)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                preset.enabled = EditorGUILayout.Toggle(preset.enabled, GUILayout.Width(20));
                EditorGUILayout.LabelField(preset.presetName, EditorStyles.boldLabel);

                if (GUILayout.Button("应用预设", GUILayout.Width(80)))
                {
                    VisualHierarchyLogic.ApplyFolderPreset(preset);
                }
                if (GUILayout.Button("应用到选中文件夹", GUILayout.Width(120)))
                {
                    if (Selection.activeObject != null)
                    {
                        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                        if (AssetDatabase.IsValidFolder(path))
                        {
                            VisualHierarchyLogic.ApplyFolderPreset(preset, path);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("提示", "请先选择一个文件夹", "确定");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("提示", "请先在Project窗口中选择一个文件夹", "确定");
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("包含文件夹:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (string folder in preset.folderNames)
                {
                    EditorGUILayout.LabelField($"  • {folder}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private void RegisterHierarchyCallback()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
    }

    private void UnregisterHierarchyCallback()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemGUI;
    }

    private void OnHierarchyWindowItemGUI(int instanceID, Rect selectionRect)
    {
        if (!_hierarchyHighlightEnabled || _hierarchyHighlights == null)
            return;

        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null)
            return;

        var config = VisualHierarchyLogic.GetMatchingConfig(obj, _hierarchyHighlights);
        if (config != null)
        {
            // 绘制背景高亮
            EditorGUI.DrawRect(selectionRect, config.backgroundColor);

            // 绘制文字（如果需要改变文字颜色）
            // 注意：Unity的Hierarchy窗口文字颜色修改比较复杂，这里只绘制背景
        }
    }

    private void LoadSettings()
    {
        // 加载文件夹颜色配置
        string folderColorsJson = ToolboxSettings.GetString(SETTINGS_KEY_PREFIX + "FolderColors", "");
        if (!string.IsNullOrEmpty(folderColorsJson))
        {
            _folderColors = JsonUtility.FromJson<SerializableList<VisualHierarchyLogic.FolderColorConfig>>(folderColorsJson).list;
        }
        else
        {
            _folderColors = VisualHierarchyLogic.GetDefaultFolderColors();
        }

        // 加载Hierarchy高亮配置
        string hierarchyHighlightsJson = ToolboxSettings.GetString(SETTINGS_KEY_PREFIX + "HierarchyHighlights", "");
        if (!string.IsNullOrEmpty(hierarchyHighlightsJson))
        {
            _hierarchyHighlights = JsonUtility.FromJson<SerializableList<VisualHierarchyLogic.HierarchyHighlightConfig>>(hierarchyHighlightsJson).list;
        }
        else
        {
            _hierarchyHighlights = VisualHierarchyLogic.GetDefaultHierarchyHighlights();
        }

        // 加载文件夹预设
        string folderPresetsJson = ToolboxSettings.GetString(SETTINGS_KEY_PREFIX + "FolderPresets", "");
        if (!string.IsNullOrEmpty(folderPresetsJson))
        {
            _folderPresets = JsonUtility.FromJson<SerializableList<VisualHierarchyLogic.ProjectFolderPreset>>(folderPresetsJson).list;
        }
        else
        {
            _folderPresets = VisualHierarchyLogic.GetDefaultFolderPresets();
        }

        _folderColorEnabled = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "FolderColorEnabled", true);
        _hierarchyHighlightEnabled = ToolboxSettings.GetBool(SETTINGS_KEY_PREFIX + "HierarchyHighlightEnabled", true);
    }

    private void SaveSettings()
    {
        if (_folderColors != null)
        {
            string json = JsonUtility.ToJson(new SerializableList<VisualHierarchyLogic.FolderColorConfig> { list = _folderColors });
            ToolboxSettings.SetString(SETTINGS_KEY_PREFIX + "FolderColors", json);
            ProjectWindowDrawer.RefreshConfigs(); // 刷新Project窗口绘制器
        }

        if (_hierarchyHighlights != null)
        {
            string json = JsonUtility.ToJson(new SerializableList<VisualHierarchyLogic.HierarchyHighlightConfig> { list = _hierarchyHighlights });
            ToolboxSettings.SetString(SETTINGS_KEY_PREFIX + "HierarchyHighlights", json);
        }

        if (_folderPresets != null)
        {
            string json = JsonUtility.ToJson(new SerializableList<VisualHierarchyLogic.ProjectFolderPreset> { list = _folderPresets });
            ToolboxSettings.SetString(SETTINGS_KEY_PREFIX + "FolderPresets", json);
        }

        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "FolderColorEnabled", _folderColorEnabled);
        ToolboxSettings.SetBool(SETTINGS_KEY_PREFIX + "HierarchyHighlightEnabled", _hierarchyHighlightEnabled);
    }

    private void RegisterSceneCreatedCallback()
    {
        // 场景创建功能通过菜单项手动触发，这里不需要自动监听
    }

    private void UnregisterSceneCreatedCallback()
    {
        // 清理回调（如果需要）
    }

    // 辅助类：用于序列化List
    [System.Serializable]
    private class SerializableList<T>
    {
        public List<T> list;
    }
}

