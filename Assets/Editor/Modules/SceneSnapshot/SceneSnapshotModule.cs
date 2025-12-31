using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 场景状态快照/回滚系统 - UI模块
/// </summary>
public class SceneSnapshotModule : ToolModule
{
    public override string Name => "场景状态快照";
    public override string Category => "Scene";
    public override int Order => 5;
    public override string IconName => "d_SaveAs";
    public override Color HeaderColor => new Color(0.2f, 0.7f, 0.9f);
    public override Color BackgroundColor => new Color(0.3f, 0.8f, 1f);

    private Vector2 _snapshotScrollPos;
    private Vector2 _comparisonScrollPos;
    private string _newSnapshotName = "";
    private string _selectedSnapshotId1 = "";
    private string _selectedSnapshotId2 = "";
    private bool _showComparison = false;
    private SceneSnapshotLogic.SnapshotComparison _currentComparison = null;
    private System.Collections.IEnumerator _restoreCoroutine = null;

    public override void OnInitialize()
    {
        // 初始化
    }

    public override void OnGUI(ToolContext context)
    {
        // 更新回滚协程
        if (_restoreCoroutine != null)
        {
            if (!_restoreCoroutine.MoveNext())
            {
                _restoreCoroutine = null;
            }
        }

        // 显示回滚进度
        if (SceneSnapshotLogic.IsRestoring())
        {
            float progress = SceneSnapshotLogic.GetRestoreProgress();
            string status = SceneSnapshotLogic.GetRestoreStatus();
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(progressRect, progress, $"{status} ({progress * 100:F1}%)");
            EditorGUILayout.Space(5);
        }

        // 检查当前场景是否已保存
        bool isSceneSaved = !string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        
        if (!isSceneSaved)
        {
            EditorGUILayout.HelpBox("⚠ 当前场景未保存，请先保存场景后再创建快照", MessageType.Warning);
            EditorGUILayout.Space(5);
        }

        // 创建快照区域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📸 创建快照", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        _newSnapshotName = EditorGUILayout.TextField("快照名称", _newSnapshotName);
        
        EditorGUI.BeginDisabledGroup(!isSceneSaved);
        if (GUILayout.Button("创建快照", GUILayout.Width(100)))
        {
            string name = string.IsNullOrEmpty(_newSnapshotName) ? null : _newSnapshotName;
            var snapshot = SceneSnapshotLogic.CreateSnapshot(name);
            if (snapshot != null)
            {
                _newSnapshotName = "";
                GUI.changed = true;
            }
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // 快照列表
        var snapshots = SceneSnapshotLogic.GetAllSnapshots();
        
        if (snapshots.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无快照，点击上方按钮创建第一个快照", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"📋 快照列表 ({snapshots.Count})", EditorStyles.boldLabel);
        
        _snapshotScrollPos = EditorGUILayout.BeginScrollView(_snapshotScrollPos, GUILayout.Height(200));
        
        foreach (var snapshot in snapshots.OrderByDescending(s => s.timestamp))
        {
            DrawSnapshotItem(snapshot, snapshots);
        }
        
        EditorGUILayout.EndScrollView();
        
        // 清空所有快照按钮
        EditorGUILayout.Space(5);
        EditorGUI.BeginDisabledGroup(snapshots.Count == 0);
        if (GUILayout.Button("🗑️ 清空所有快照", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除所有快照吗？此操作不可恢复！", "确定", "取消"))
            {
                SceneSnapshotLogic.ClearAllSnapshots();
                GUI.changed = true;
            }
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // 快照对比区域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🔍 快照对比", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        // 选择快照1
        EditorGUILayout.LabelField("快照1:", GUILayout.Width(50));
        int index1 = snapshots.FindIndex(s => s.id == _selectedSnapshotId1);
        if (index1 < 0) index1 = 0;
        index1 = EditorGUILayout.Popup(index1, snapshots.Select(s => $"{s.name} ({s.timestamp})").ToArray(), GUILayout.ExpandWidth(true));
        if (snapshots.Count > 0)
        {
            _selectedSnapshotId1 = snapshots[index1].id;
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        // 选择快照2
        EditorGUILayout.LabelField("快照2:", GUILayout.Width(50));
        int index2 = snapshots.FindIndex(s => s.id == _selectedSnapshotId2);
        if (index2 < 0) index2 = snapshots.Count > 1 ? 1 : 0;
        index2 = EditorGUILayout.Popup(index2, snapshots.Select(s => $"{s.name} ({s.timestamp})").ToArray(), GUILayout.ExpandWidth(true));
        if (snapshots.Count > 0)
        {
            _selectedSnapshotId2 = snapshots[index2].id;
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 对比按钮
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_selectedSnapshotId1) || string.IsNullOrEmpty(_selectedSnapshotId2) || _selectedSnapshotId1 == _selectedSnapshotId2);
        if (GUILayout.Button("开始对比", GUILayout.Height(25)))
        {
            _currentComparison = SceneSnapshotLogic.CompareSnapshots(_selectedSnapshotId1, _selectedSnapshotId2);
            _showComparison = true;
        }
        EditorGUI.EndDisabledGroup();
        
        // 显示对比结果
        if (_showComparison && _currentComparison != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("对比结果:", EditorStyles.boldLabel);
            
            _comparisonScrollPos = EditorGUILayout.BeginScrollView(_comparisonScrollPos, GUILayout.Height(150));
            
            if (_currentComparison.addedObjects.Count > 0)
            {
                EditorGUILayout.LabelField($"➕ 新增对象 ({_currentComparison.addedObjects.Count}):", EditorStyles.miniLabel);
                foreach (var objName in _currentComparison.addedObjects)
                {
                    EditorGUILayout.LabelField($"  • {objName}", EditorStyles.miniLabel);
                }
                EditorGUILayout.Space(3);
            }
            
            if (_currentComparison.removedObjects.Count > 0)
            {
                EditorGUILayout.LabelField($"➖ 删除对象 ({_currentComparison.removedObjects.Count}):", EditorStyles.miniLabel);
                foreach (var objName in _currentComparison.removedObjects)
                {
                    EditorGUILayout.LabelField($"  • {objName}", EditorStyles.miniLabel);
                }
                EditorGUILayout.Space(3);
            }
            
            if (_currentComparison.modifiedObjects.Count > 0)
            {
                EditorGUILayout.LabelField($"✏️ 修改对象 ({_currentComparison.modifiedObjects.Count}):", EditorStyles.miniLabel);
                foreach (var objName in _currentComparison.modifiedObjects)
                {
                    EditorGUILayout.LabelField($"  • {objName}", EditorStyles.miniLabel);
                }
                EditorGUILayout.Space(3);
            }
            
            if (_currentComparison.addedObjects.Count == 0 && 
                _currentComparison.removedObjects.Count == 0 && 
                _currentComparison.modifiedObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("两个快照完全相同", MessageType.Info);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawSnapshotItem(SceneSnapshotLogic.SceneSnapshot snapshot, List<SceneSnapshotLogic.SceneSnapshot> allSnapshots)
    {
        EditorGUILayout.BeginVertical("box");
        
        // 快照信息
        EditorGUILayout.BeginHorizontal();
        
        // 选中指示器
        bool isSelected = _selectedSnapshotId1 == snapshot.id || _selectedSnapshotId2 == snapshot.id;
        if (isSelected)
        {
            GUI.color = new Color(0.3f, 0.7f, 1f);
        }
        
        EditorGUILayout.LabelField("📸", GUILayout.Width(20));
        EditorGUILayout.LabelField(snapshot.name, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField(snapshot.timestamp, EditorStyles.miniLabel, GUILayout.Width(150));
        
        GUI.color = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        // 快照详情
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"  对象数: {snapshot.gameObjects.Count}", EditorStyles.miniLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField($"Lightmap: {snapshot.lightmapSettings.lightmapCount}", EditorStyles.miniLabel, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        
        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        
        // 回滚按钮
        Color originalColor = GUI.backgroundColor;
        bool isRestoring = SceneSnapshotLogic.IsRestoring();
        GUI.backgroundColor = isRestoring ? Color.gray : new Color(0.2f, 0.8f, 0.3f);
        EditorGUI.BeginDisabledGroup(isRestoring);
        if (GUILayout.Button(isRestoring ? "⏳ 回滚中..." : "↩️ 回滚", GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog("确认回滚", 
                $"确定要回滚到快照「{snapshot.name}」吗？\n\n当前场景的所有更改将被覆盖！", 
                "确定", "取消"))
            {
                StartRestore(snapshot.id, snapshot.name);
            }
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = originalColor;
        
        // 删除按钮
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("🗑️ 删除", GUILayout.Height(22), GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", 
                $"确定要删除快照「{snapshot.name}」吗？", 
                "确定", "取消"))
            {
                SceneSnapshotLogic.DeleteSnapshot(snapshot.id);
                // 如果删除的是选中的快照，清除选择
                if (_selectedSnapshotId1 == snapshot.id)
                    _selectedSnapshotId1 = "";
                if (_selectedSnapshotId2 == snapshot.id)
                    _selectedSnapshotId2 = "";
                GUI.changed = true;
            }
        }
        GUI.backgroundColor = originalColor;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    /// <summary>
    /// 开始回滚
    /// </summary>
    private void StartRestore(string snapshotId, string snapshotName)
    {
        var snapshot = SceneSnapshotLogic.GetSnapshot(snapshotId);
        if (snapshot == null)
        {
            EditorUtility.DisplayDialog("错误", "快照不存在", "确定");
            return;
        }

        // 如果对象数量较多，使用协程版本
        if (snapshot.gameObjects.Count > 100)
        {
            _restoreCoroutine = SceneSnapshotLogic.RestoreSnapshotCoroutine(snapshotId, (success) =>
            {
                if (success)
                {
                    EditorUtility.DisplayDialog("回滚成功", $"场景已回滚到快照「{snapshotName}」", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("回滚失败", "回滚过程中出现错误，请查看Console获取详细信息", "确定");
                }
                _restoreCoroutine = null;
            });
        }
        else
        {
            // 对象数量较少，使用同步版本
            bool success = SceneSnapshotLogic.RestoreSnapshot(snapshotId);
            if (success)
            {
                EditorUtility.DisplayDialog("回滚成功", $"场景已回滚到快照「{snapshotName}」", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("回滚失败", "回滚过程中出现错误，请查看Console获取详细信息", "确定");
            }
        }
    }

    public override bool IsAvailable(ToolContext context)
    {
        // 始终可用
        return true;
    }
}

