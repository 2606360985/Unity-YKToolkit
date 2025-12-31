using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 编辑器时间统计模块 - UI部分
/// </summary>
public class EditorTimeTrackerModule : ToolModule
{
    public override string Name => "⏱️ 时间统计 & 行为分析";
    public override string Category => "Analytics";
    public override int Order => 0;
    public override string IconName => "d_Profiler.Statistics";
    public override Color HeaderColor => new Color(0.3f, 0.6f, 0.9f);
    public override Color BackgroundColor => new Color(0.95f, 0.95f, 0.95f);

    private enum TabType
    {
        场景编辑时间,
        烘焙耗时历史,
        常用工具排行,
        最慢操作记录
    }

    private TabType _currentTab = TabType.场景编辑时间;
    private Vector2 _scrollPos;
    private bool _showDetails = false;

    public override void OnInitialize()
    {
        EditorTimeTrackerLogic.Initialize();
    }

    public override void OnCleanup()
    {
        EditorTimeTrackerLogic.Cleanup();
    }

    public override void OnGUI(ToolContext context)
    {
        // 标签页切换
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_currentTab == TabType.场景编辑时间, "场景时间", EditorStyles.toolbarButton))
            _currentTab = TabType.场景编辑时间;
        if (GUILayout.Toggle(_currentTab == TabType.烘焙耗时历史, "烘焙历史", EditorStyles.toolbarButton))
            _currentTab = TabType.烘焙耗时历史;
        if (GUILayout.Toggle(_currentTab == TabType.常用工具排行, "工具排行", EditorStyles.toolbarButton))
            _currentTab = TabType.常用工具排行;
        if (GUILayout.Toggle(_currentTab == TabType.最慢操作记录, "慢操作", EditorStyles.toolbarButton))
            _currentTab = TabType.最慢操作记录;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case TabType.场景编辑时间:
                DrawSceneTimeTab();
                break;
            case TabType.烘焙耗时历史:
                DrawBakeHistoryTab();
                break;
            case TabType.常用工具排行:
                DrawToolRankingTab();
                break;
            case TabType.最慢操作记录:
                DrawSlowOperationsTab();
                break;
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);
        DrawActions();
    }

    #region 场景编辑时间

    private void DrawSceneTimeTab()
    {
        EditorGUILayout.LabelField("场景编辑时间统计", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("统计你在每个场景上花费的时间，包括活跃时间和空转时间。", MessageType.Info);

        var records = EditorTimeTrackerLogic.GetSceneRecords();
        if (records.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无场景记录", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(5);

        // 总计统计
        double totalTime = records.Sum(r => r.totalTime);
        double totalActiveTime = records.Sum(r => r.activeTime);
        double totalIdleTime = totalTime - totalActiveTime;
        int totalOpenCount = records.Sum(r => r.openCount);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("总计", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"总编辑时间: {EditorTimeTrackerLogic.FormatTime(totalTime)}");
        EditorGUILayout.LabelField($"活跃时间: {EditorTimeTrackerLogic.FormatTime(totalActiveTime)} ({totalActiveTime / totalTime * 100:F1}%)");
        EditorGUILayout.LabelField($"空转时间: {EditorTimeTrackerLogic.FormatTime(totalIdleTime)} ({totalIdleTime / totalTime * 100:F1}%)");
        EditorGUILayout.LabelField($"场景切换次数: {totalOpenCount}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // 场景列表
        EditorGUILayout.LabelField("各场景详情", EditorStyles.boldLabel);
        foreach (var record in records)
        {
            DrawSceneRecord(record);
        }
    }

    private void DrawSceneRecord(EditorTimeTrackerLogic.SceneTimeRecord record)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string sceneName = EditorTimeTrackerLogic.GetSceneName(record.scenePath);
        EditorGUILayout.LabelField(sceneName, EditorStyles.boldLabel);

        if (!string.IsNullOrEmpty(record.scenePath))
        {
            EditorGUILayout.LabelField($"路径: {record.scenePath}", EditorStyles.miniLabel);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"总时间: {EditorTimeTrackerLogic.FormatTime(record.totalTime)}", GUILayout.Width(150));
        EditorGUILayout.LabelField($"打开次数: {record.openCount}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"活跃时间: {EditorTimeTrackerLogic.FormatTime(record.activeTime)}", GUILayout.Width(150));
        double activePercent = record.totalTime > 0 ? record.activeTime / record.totalTime * 100 : 0;
        EditorGUILayout.LabelField($"({activePercent:F1}%)", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"空转时间: {EditorTimeTrackerLogic.FormatTime(record.idleTime)}", GUILayout.Width(150));
        double idlePercent = record.totalTime > 0 ? record.idleTime / record.totalTime * 100 : 0;
        EditorGUILayout.LabelField($"({idlePercent:F1}%)", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        // 进度条显示活跃度
        Rect rect = GUILayoutUtility.GetRect(0, 5, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(rect, (float)(activePercent / 100.0), "");

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    #endregion

    #region 烘焙耗时历史

    private void DrawBakeHistoryTab()
    {
        EditorGUILayout.LabelField("烘焙耗时历史", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("记录所有烘焙操作的耗时，帮你了解时间都花在哪了。", MessageType.Info);

        var records = EditorTimeTrackerLogic.GetBakeRecords();
        if (records.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无烘焙记录", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(5);

        // 统计摘要
        var totalByType = EditorTimeTrackerLogic.GetTotalBakeTimeByType();
        double todayBakeTime = EditorTimeTrackerLogic.GetTodayBakeTime();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("统计摘要", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"今日烘焙总时间: {EditorTimeTrackerLogic.FormatTime(todayBakeTime)}");
        EditorGUILayout.Space(3);

        foreach (var kvp in totalByType.OrderByDescending(x => x.Value))
        {
            EditorGUILayout.LabelField($"{kvp.Key}: {EditorTimeTrackerLogic.FormatTime(kvp.Value)}");
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // 按类型分组显示
        var groupedByType = records.GroupBy(r => r.type);
        foreach (var group in groupedByType.OrderByDescending(g => g.Sum(r => r.duration)))
        {
            EditorGUILayout.LabelField($"{group.Key} ({group.Count()}次)", EditorStyles.boldLabel);

            var recentRecords = group.OrderByDescending(r => r.time).Take(10);
            foreach (var record in recentRecords)
            {
                DrawBakeRecord(record);
            }

            if (group.Count() > 10)
            {
                EditorGUILayout.LabelField($"... 还有 {group.Count() - 10} 条记录", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(5);
        }
    }

    private void DrawBakeRecord(EditorTimeTrackerLogic.BakeRecord record)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(EditorTimeTrackerLogic.GetSceneName(record.scene), GUILayout.Width(150));
        EditorGUILayout.LabelField($"{EditorTimeTrackerLogic.FormatTime(record.duration)}", GUILayout.Width(100));
        EditorGUILayout.LabelField(record.time.ToString("MM-dd HH:mm"), EditorStyles.miniLabel, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region 常用工具排行

    private void DrawToolRankingTab()
    {
        EditorGUILayout.LabelField("常用工具排行", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("统计工具箱内各功能的使用频率，帮你了解自己的使用习惯。", MessageType.Info);

        var rankings = EditorTimeTrackerLogic.GetToolUsageRanking(20);
        if (rankings.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无工具使用记录", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(5);

        // 计算总使用次数
        int totalUsage = rankings.Sum(r => r.usageCount);

        // 显示排行榜
        for (int i = 0; i < rankings.Count; i++)
        {
            var record = rankings[i];
            double percentage = totalUsage > 0 ? (double)record.usageCount / totalUsage * 100 : 0;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 排名
            EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(30));

            // 工具名称
            EditorGUILayout.LabelField(record.toolName, GUILayout.Width(150));

            // 使用次数
            EditorGUILayout.LabelField($"{record.usageCount}次", GUILayout.Width(80));

            // 百分比
            EditorGUILayout.LabelField($"{percentage:F1}%", GUILayout.Width(60));

            // 进度条
            Rect rect = GUILayoutUtility.GetRect(0, 15, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, (float)(percentage / 100.0), "");

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"总计使用: {totalUsage}次", EditorStyles.miniLabel);
    }

    #endregion

    #region 最慢操作记录

    private void DrawSlowOperationsTab()
    {
        EditorGUILayout.LabelField("最慢操作记录", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("记录耗时超过1秒的操作，帮你发现性能瓶颈。", MessageType.Info);

        var slowOps = EditorTimeTrackerLogic.GetSlowOperations(20);
        if (slowOps.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无慢操作记录", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField($"最慢的 {slowOps.Count} 次操作:", EditorStyles.boldLabel);

        foreach (var record in slowOps)
        {
            DrawSlowOperationRecord(record);
        }
    }

    private void DrawSlowOperationRecord(EditorTimeTrackerLogic.SlowOperationRecord record)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(record.actionName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{EditorTimeTrackerLogic.FormatTime(record.duration)}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"时间: {record.time.ToString("yyyy-MM-dd HH:mm:ss")}", EditorStyles.miniLabel);

        if (!string.IsNullOrEmpty(record.details))
        {
            EditorGUILayout.LabelField($"详情: {record.details}", EditorStyles.miniLabel);
        }

        // 进度条显示相对耗时（相对于最慢的）
        var allOps = EditorTimeTrackerLogic.GetSlowOperations(1);
        if (allOps.Count > 0 && allOps[0].duration > 0)
        {
            float relativeDuration = (float)(record.duration / allOps[0].duration);
            Rect rect = GUILayoutUtility.GetRect(0, 5, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, relativeDuration, "");
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    #endregion

    #region 操作按钮

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("导出数据"))
        {
            string json = EditorTimeTrackerLogic.ExportData();
            string path = EditorUtility.SaveFilePanel("导出统计数据", "", "EditorTimeTrackerData", "json");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                EditorUtility.DisplayDialog("导出成功", "数据已导出到:\n" + path, "确定");
            }
        }

        if (GUILayout.Button("导入数据"))
        {
            string path = EditorUtility.OpenFilePanel("导入统计数据", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(path);
                    if (EditorTimeTrackerLogic.ImportData(json))
                    {
                        EditorUtility.DisplayDialog("导入成功", "数据已导入", "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("导入失败", "数据格式错误", "确定");
                    }
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("导入失败", e.Message, "确定");
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("清空数据", GUIStyle.none))
        {
            if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有统计数据吗？此操作不可恢复！", "确定", "取消"))
            {
                EditorTimeTrackerLogic.ClearAllData();
                EditorUtility.DisplayDialog("清空完成", "所有数据已清空", "确定");
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    #endregion
}

