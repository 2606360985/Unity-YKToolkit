using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 资产使用情况扫描模块 - UI部分
/// </summary>
public class AssetUsageScannerModule : ToolModule
{
    public override string Name => "资产使用情况扫描";
    public override string Category => "Assets";
    public override int Order => 2;
    public override string IconName => "d_Search";
    public override Color HeaderColor => new Color(0.8f, 0.4f, 0.2f);
    public override Color BackgroundColor => new Color(1f, 0.95f, 0.9f);

    // UI状态
    private AssetUsageScannerLogic.ScanResult _scanResult;
    private bool _isScanning = false;
    private Vector2 _scrollPos;
    private Vector2 _whitelistScrollPos;
    private bool _showUnreferenced = true;
    private bool _showEditorOnly = true;
    private bool _showTestOnly = true;
    private bool _showRuntime = false;
    private bool _showWhitelist = false;
    private string _searchFilter = "";
    private HashSet<string> _whitelist = new HashSet<string>();
    private Dictionary<string, bool> _selectedAssets = new Dictionary<string, bool>();
    private System.Collections.IEnumerator _scanCoroutine;

    public override void OnInitialize()
    {
        _whitelist = AssetUsageScannerLogic.LoadWhitelist();
    }

    public override void OnGUI(ToolContext context)
    {
        EditorGUILayout.HelpBox(
            "扫描项目中未被场景/Prefab引用的资产。\n" +
            "区分：未引用 / 仅编辑器引用 / 仅测试引用\n" +
            "清理前预览 + 白名单机制，更安全！",
            MessageType.Info);

        EditorGUILayout.Space(5);

        // 扫描按钮
        EditorGUI.BeginDisabledGroup(_isScanning);
        if (DrawIconButton("🔍 开始扫描", "d_Search", new Color(0.2f, 0.6f, 1f), 35))
        {
            StartScan();
        }
        EditorGUI.EndDisabledGroup();

        // 显示扫描进度
        if (_isScanning)
        {
            if (_scanResult != null && _scanResult.TotalScanned > 0)
            {
                Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, _scanResult.ScanProgress, 
                    $"扫描中... {_scanResult.TotalScanned} 个资产 ({_scanResult.ScanProgress * 100:F1}%)");
            }
            else
            {
                EditorGUILayout.HelpBox("正在扫描资产，请稍候...", MessageType.Info);
            }
        }

        // 显示扫描结果
        if (_scanResult != null && !_isScanning)
        {
            DrawScanResults();
        }

        // 白名单管理
        DrawWhitelistSection();
    }

    /// <summary>
    /// 开始扫描
    /// </summary>
    private void StartScan()
    {
        _isScanning = true;
        _scanResult = new AssetUsageScannerLogic.ScanResult();
        _selectedAssets.Clear();

        // 使用协程进行扫描（虽然AssetDatabase API是同步的，但使用协程可以显示进度）
        _scanCoroutine = ScanCoroutine();
        EditorCoroutineHelper.StartCoroutine(_scanCoroutine);
    }

    /// <summary>
    /// 扫描协程
    /// </summary>
    private System.Collections.IEnumerator ScanCoroutine()
    {
        // 初始化结果对象
        _scanResult = new AssetUsageScannerLogic.ScanResult();
        
        // 执行扫描（同步执行，但进度回调会更新进度）
        _scanResult = AssetUsageScannerLogic.ScanAllAssets(
            _whitelist,
            progress => 
            {
                if (_scanResult != null)
                {
                    _scanResult.ScanProgress = progress;
                }
                // 强制重绘窗口以显示进度
                EditorApplication.delayCall += () => {
                    if (EditorWindow.focusedWindow != null)
                        EditorWindow.focusedWindow.Repaint();
                };
            }
        );
        
        _isScanning = false;
        yield return null;
    }

    /// <summary>
    /// 绘制扫描结果
    /// </summary>
    private void DrawScanResults()
    {
        EditorGUILayout.Space(10);

        // 统计信息
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📊 扫描统计", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"总扫描数: {_scanResult.TotalScanned}");
        EditorGUILayout.LabelField($"未引用: {_scanResult.UnreferencedAssets.Count} ({FormatSize(_scanResult.UnreferencedAssets.Sum(a => a.FileSize))})");
        EditorGUILayout.LabelField($"仅编辑器引用: {_scanResult.EditorOnlyAssets.Count} ({FormatSize(_scanResult.EditorOnlyAssets.Sum(a => a.FileSize))})");
        EditorGUILayout.LabelField($"仅测试引用: {_scanResult.TestOnlyAssets.Count} ({FormatSize(_scanResult.TestOnlyAssets.Sum(a => a.FileSize))})");
        EditorGUILayout.LabelField($"运行时引用: {_scanResult.RuntimeAssets.Count}");
        EditorGUILayout.LabelField($"总计未使用: {_scanResult.TotalUnused} ({FormatSize(_scanResult.TotalUnusedSize)})", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // 搜索过滤
        _searchFilter = EditorGUILayout.TextField("🔍 搜索过滤", _searchFilter);

        EditorGUILayout.Space(5);

        // 分类显示选项
        EditorGUILayout.BeginHorizontal();
        _showUnreferenced = EditorGUILayout.Toggle("未引用", _showUnreferenced, GUILayout.Width(100));
        _showEditorOnly = EditorGUILayout.Toggle("仅编辑器引用", _showEditorOnly, GUILayout.Width(120));
        _showTestOnly = EditorGUILayout.Toggle("仅测试引用", _showTestOnly, GUILayout.Width(120));
        _showRuntime = EditorGUILayout.Toggle("运行时引用", _showRuntime, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 批量操作
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选未使用"))
        {
            SelectAllUnused();
        }
        if (GUILayout.Button("取消全选"))
        {
            _selectedAssets.Clear();
        }
        if (GUILayout.Button("删除选中"))
        {
            DeleteSelected();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 资产列表 - 设置更大的显示高度，提供更多展示空间
        EditorGUILayout.LabelField("📋 资产管理清单", EditorStyles.boldLabel);
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(400));

        // 未引用资产
        if (_showUnreferenced)
        {
            DrawAssetList("❌ 未引用资产", _scanResult.UnreferencedAssets, 
                AssetUsageScannerLogic.ReferenceStatus.Unreferenced);
        }

        // 仅编辑器引用
        if (_showEditorOnly)
        {
            DrawAssetList("⚠️ 仅编辑器引用", _scanResult.EditorOnlyAssets, 
                AssetUsageScannerLogic.ReferenceStatus.EditorOnly);
        }

        // 仅测试引用
        if (_showTestOnly)
        {
            DrawAssetList("🧪 仅测试引用", _scanResult.TestOnlyAssets, 
                AssetUsageScannerLogic.ReferenceStatus.TestOnly);
        }

        // 运行时引用（可选显示）
        if (_showRuntime)
        {
            DrawAssetList("✅ 运行时引用", _scanResult.RuntimeAssets, 
                AssetUsageScannerLogic.ReferenceStatus.Runtime);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 绘制资产列表
    /// </summary>
    private void DrawAssetList(string title, List<AssetUsageScannerLogic.AssetUsageInfo> assets, 
        AssetUsageScannerLogic.ReferenceStatus status)
    {
        if (assets.Count == 0)
            return;

        // 应用搜索过滤
        var filteredAssets = assets.Where(a => 
            string.IsNullOrEmpty(_searchFilter) || 
            a.AssetName.ToLower().Contains(_searchFilter.ToLower()) ||
            a.AssetPath.ToLower().Contains(_searchFilter.ToLower())
        ).ToList();

        if (filteredAssets.Count == 0)
            return;

        EditorGUILayout.LabelField($"{title} ({filteredAssets.Count})", EditorStyles.boldLabel);

        foreach (var asset in filteredAssets)
        {
            EditorGUILayout.BeginHorizontal("box");

            // 选择框
            bool selected = _selectedAssets.ContainsKey(asset.AssetPath) && _selectedAssets[asset.AssetPath];
            bool newSelected = EditorGUILayout.Toggle(selected, GUILayout.Width(20));
            if (newSelected != selected)
            {
                _selectedAssets[asset.AssetPath] = newSelected;
            }

            // 资产信息
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(asset.AssetName, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(asset.AssetPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"大小: {asset.GetFormattedSize()}", EditorStyles.miniLabel);
            
            // 显示引用者（如果有）
            if (asset.ReferencedBy.Count > 0)
            {
                EditorGUILayout.LabelField($"被 {asset.ReferencedBy.Count} 个资产引用", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            // 操作按钮
            EditorGUILayout.BeginVertical();
            if (GUILayout.Button("定位", GUILayout.Width(50), GUILayout.Height(20)))
            {
                Selection.activeObject = asset.AssetObject;
                EditorGUIUtility.PingObject(asset.AssetObject);
            }
            if (GUILayout.Button("加入白名单", GUILayout.Width(80), GUILayout.Height(20)))
            {
                AssetUsageScannerLogic.AddToWhitelist(asset.AssetPath);
                _whitelist.Add(asset.AssetPath);
                EditorUtility.DisplayDialog("提示", "已添加到白名单，重新扫描后将从结果中排除。", "确定");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);
    }

    /// <summary>
    /// 绘制白名单部分
    /// </summary>
    private void DrawWhitelistSection()
    {
        _showWhitelist = EditorGUILayout.Foldout(_showWhitelist, $"📋 白名单管理 ({_whitelist.Count})");
        if (_showWhitelist)
        {
            EditorGUILayout.BeginVertical("box");
            
            if (_whitelist.Count == 0)
            {
                EditorGUILayout.HelpBox("白名单为空。可以将资产添加到白名单以避免被扫描和删除。", MessageType.Info);
            }
            else
            {
                _whitelistScrollPos = EditorGUILayout.BeginScrollView(_whitelistScrollPos, GUILayout.Height(150));
                var whitelistArray = _whitelist.ToArray();
                foreach (string path in whitelistArray)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                    if (GUILayout.Button("移除", GUILayout.Width(50)))
                    {
                        AssetUsageScannerLogic.RemoveFromWhitelist(path);
                        _whitelist.Remove(path);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 全选未使用的资产
    /// </summary>
    private void SelectAllUnused()
    {
        foreach (var asset in _scanResult.UnreferencedAssets)
        {
            _selectedAssets[asset.AssetPath] = true;
        }
        foreach (var asset in _scanResult.EditorOnlyAssets)
        {
            _selectedAssets[asset.AssetPath] = true;
        }
        foreach (var asset in _scanResult.TestOnlyAssets)
        {
            _selectedAssets[asset.AssetPath] = true;
        }
    }

    /// <summary>
    /// 删除选中的资产
    /// </summary>
    private void DeleteSelected()
    {
        var selectedPaths = _selectedAssets.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        if (selectedPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先选择要删除的资产。", "确定");
            return;
        }

        string message = $"确定要删除 {selectedPaths.Count} 个资产吗？\n\n此操作不可撤销！";
        if (!EditorUtility.DisplayDialog("确认删除", message, "删除", "取消"))
            return;

        int deletedCount = AssetUsageScannerLogic.DeleteAssets(selectedPaths);
        EditorUtility.DisplayDialog("完成", $"已删除 {deletedCount} 个资产。", "确定");

        // 清除选择并重新扫描
        _selectedAssets.Clear();
        _scanResult = null;
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F2} KB";
        else
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    /// <summary>
    /// 绘制带图标的按钮
    /// </summary>
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

