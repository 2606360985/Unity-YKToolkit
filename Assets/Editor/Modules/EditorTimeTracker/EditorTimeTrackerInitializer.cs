using UnityEditor;

/// <summary>
/// 编辑器时间统计初始化器 - 确保统计系统在Unity启动时自动初始化
/// </summary>
[InitializeOnLoad]
public static class EditorTimeTrackerInitializer
{
    static EditorTimeTrackerInitializer()
    {
        // Unity启动时自动初始化
        EditorTimeTrackerLogic.Initialize();

        // 注册Unity退出事件
        EditorApplication.quitting += OnQuitting;
    }

    private static void OnQuitting()
    {
        // Unity退出时保存数据
        EditorTimeTrackerLogic.Cleanup();
    }
}

