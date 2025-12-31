using UnityEngine;

/// <summary>
/// 工具使用追踪器 - 供其他模块调用的便捷接口
/// </summary>
public static class ToolUsageTracker
{
    /// <summary>
    /// 记录工具使用
    /// </summary>
    /// <param name="toolName">工具名称</param>
    public static void Track(string toolName)
    {
        if (string.IsNullOrEmpty(toolName))
        {
            Debug.LogWarning("ToolUsageTracker: Tool name cannot be empty");
            return;
        }

        EditorTimeTrackerLogic.TrackToolUsage(toolName);
    }

    /// <summary>
    /// 记录慢操作（带自动计时）
    /// </summary>
    /// <param name="actionName">操作名称</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="details">详细信息</param>
    public static void TrackSlowOperation(string actionName, System.Action action, string details = "")
    {
        EditorTimeTrackerLogic.RecordSlowOperation(actionName, action, details);
    }

    /// <summary>
    /// 记录慢操作（直接传入耗时）
    /// </summary>
    /// <param name="actionName">操作名称</param>
    /// <param name="durationSeconds">耗时（秒）</param>
    /// <param name="details">详细信息</param>
    public static void TrackSlowOperation(string actionName, double durationSeconds, string details = "")
    {
        EditorTimeTrackerLogic.RecordSlowOperation(actionName, durationSeconds, details);
    }
}

