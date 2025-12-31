using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 资产依赖可视化模块 - UI部分
/// </summary>
public class AssetDependencyModule : ToolModule
{
    public override string Name => "资产依赖可视化图谱";
    public override string Category => "Assets";
    public override int Order => 1;
    public override string IconName => "d_UnityEditor.Graphs";
    public override Color HeaderColor => new Color(0.4f, 0.6f, 1f);
    public override Color BackgroundColor => new Color(0.9f, 0.95f, 1f);

    // UI状态
    private AssetDependencyLogic.DependencyGraph _currentGraph;
    private Vector2 _scrollPos;
    private bool _showDependencies = true;
    private bool _showReferencers = true;
    private bool _showDetails = false;
    private string _selectedNodePath = "";

    public override bool IsAvailable(ToolContext context)
    {
        return context.HasSelectedObjectsAll && 
               context.SelectedObjectsAll.Any(obj => AssetUtil.IsAsset(obj));
    }

    public override void OnGUI(ToolContext context)
    {
        if (!context.HasSelectedObjectsAll)
        {
            EditorGUILayout.HelpBox("请在 Project 窗口中选择要分析的资产（Prefab / Material / Script 等）。", MessageType.Info);
            return;
        }

        // 检查选中的是否是资产
        var assets = context.SelectedObjectsAll.Where(obj => AssetUtil.IsAsset(obj)).ToArray();
        if (assets.Length == 0)
        {
            EditorGUILayout.HelpBox("请选择资产文件（Prefab、Material、Script等），而不是场景中的GameObject。", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox($"已选中 {assets.Length} 个资产，点击下方按钮分析依赖关系。", MessageType.None);

        EditorGUILayout.Space(5);

        // 分析按钮
        if (DrawIconButton("🔍 分析依赖关系", IconName, HeaderColor, 30))
        {
            AnalyzeDependencies(assets);
        }

        EditorGUILayout.Space(10);

        // 显示依赖图
        if (_currentGraph != null && _currentGraph.RootNode != null)
        {
            DrawDependencyGraph();
        }
    }

    private void AnalyzeDependencies(Object[] assets)
    {
        EditorUtility.DisplayProgressBar("分析依赖", "正在分析资产依赖关系...", 0f);
        try
        {
            _currentGraph = AssetDependencyLogic.AnalyzeDependencies(assets);
            if (_currentGraph != null)
            {
                Debug.Log($"<color=green>✓ 分析完成：发现 {_currentGraph.AllNodes.Count} 个相关资产</color>");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void DrawDependencyGraph()
    {
        EditorGUILayout.BeginVertical("box");

        // 统计信息
        var rootNode = _currentGraph.RootNode;
        int depCount = rootNode.Dependencies.Count;
        int refCount = rootNode.ReferencedBy.Count;

        EditorGUILayout.LabelField("📊 依赖关系统计", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"依赖的资产: {depCount}", GUILayout.Width(150));
        EditorGUILayout.LabelField($"引用此资产的: {refCount}", GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 显示选项
        EditorGUILayout.BeginHorizontal();
        _showDependencies = EditorGUILayout.Toggle("显示依赖", _showDependencies, GUILayout.Width(100));
        _showReferencers = EditorGUILayout.Toggle("显示引用者", _showReferencers, GUILayout.Width(100));
        _showDetails = EditorGUILayout.Toggle("显示详情", _showDetails, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 滚动视图
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(400));

        // 绘制根节点
        DrawNode(rootNode, true);

        EditorGUILayout.Space(10);

        // 绘制依赖节点
        if (_showDependencies && depCount > 0)
        {
            EditorGUILayout.LabelField("📥 依赖的资产:", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            foreach (string depPath in rootNode.Dependencies)
            {
                if (_currentGraph.NodeMap.TryGetValue(depPath, out var depNode))
                {
                    DrawNode(depNode, false);
                }
            }
        }

        EditorGUILayout.Space(10);

        // 绘制引用者节点
        if (_showReferencers && refCount > 0)
        {
            EditorGUILayout.LabelField("📤 引用此资产的资产:", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            foreach (string refPath in rootNode.ReferencedBy)
            {
                if (_currentGraph.NodeMap.TryGetValue(refPath, out var refNode))
                {
                    DrawNode(refNode, false);
                }
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("在Project中定位", GUILayout.Height(25)))
        {
            if (rootNode.AssetObject != null)
            {
                EditorGUIUtility.PingObject(rootNode.AssetObject);
                Selection.activeObject = rootNode.AssetObject;
            }
        }
        if (GUILayout.Button("导出为文本", GUILayout.Height(25)))
        {
            ExportToText();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawNode(AssetDependencyLogic.DependencyNode node, bool isRoot)
    {
        EditorGUILayout.BeginVertical("box");
        
        Color nodeColor = AssetDependencyLogic.GetNodeTypeColor(node.Type);
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = isRoot ? nodeColor * 1.2f : nodeColor * 0.7f;

        EditorGUILayout.BeginHorizontal();

        // 图标
        string iconName = AssetDependencyLogic.GetNodeTypeIcon(node.Type);
        Texture2D icon = IconHelper.GetIconSafely(iconName);
        if (icon != null)
        {
            GUILayout.Box(icon, GUILayout.Width(20), GUILayout.Height(20));
        }

        // 节点名称
        string displayName = isRoot ? $"⭐ {node.AssetName}" : node.AssetName;
        EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

        // 统计标签
        if (node.Dependencies.Count > 0 || node.ReferencedBy.Count > 0)
        {
            string stats = "";
            if (node.Dependencies.Count > 0)
                stats += $"↓{node.Dependencies.Count} ";
            if (node.ReferencedBy.Count > 0)
                stats += $"↑{node.ReferencedBy.Count}";
            EditorGUILayout.LabelField(stats, EditorStyles.miniLabel, GUILayout.Width(60));
        }

        // 定位按钮
        if (GUILayout.Button("定位", GUILayout.Width(50), GUILayout.Height(20)))
        {
            if (node.AssetObject != null)
            {
                EditorGUIUtility.PingObject(node.AssetObject);
                Selection.activeObject = node.AssetObject;
            }
        }

        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = originalColor;

        // 显示详情
        if (_showDetails)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField($"路径: {node.AssetPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"类型: {node.Type}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void ExportToText()
    {
        if (_currentGraph == null || _currentGraph.RootNode == null)
            return;

        string text = "=== 资产依赖关系报告 ===\n\n";
        text += $"根资产: {_currentGraph.RootNode.AssetPath}\n";
        text += $"分析时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";

        text += "依赖的资产:\n";
        foreach (string dep in _currentGraph.RootNode.Dependencies)
        {
            text += $"  - {dep}\n";
        }

        text += "\n引用此资产的资产:\n";
        foreach (string refPath in _currentGraph.RootNode.ReferencedBy)
        {
            text += $"  - {refPath}\n";
        }

        // 复制到剪贴板
        EditorGUIUtility.systemCopyBuffer = text;
        Debug.Log($"<color=green>✓ 依赖关系已复制到剪贴板</color>");
        EditorUtility.DisplayDialog("导出完成", "依赖关系已复制到剪贴板，可以粘贴到文本编辑器。", "确定");
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
}

