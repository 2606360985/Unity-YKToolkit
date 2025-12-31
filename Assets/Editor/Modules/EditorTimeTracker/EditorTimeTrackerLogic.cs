using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;

/// <summary>
/// 编辑器时间统计逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class EditorTimeTrackerLogic
{
    #region 数据结构

    /// <summary>
    /// 场景时间记录
    /// </summary>
    [Serializable]
    public class SceneTimeRecord
    {
        public string scenePath;
        public double totalTime;          // 总时间（秒）
        public double activeTime;        // 活跃时间（秒）
        public int openCount;             // 打开次数
        public DateTime lastOpenTime;     // 最后打开时间
        public DateTime lastCloseTime;    // 最后关闭时间

        public double idleTime => totalTime - activeTime;
    }

    /// <summary>
    /// 烘焙记录
    /// </summary>
    [Serializable]
    public class BakeRecord
    {
        public string type;               // Lightmap / NavMesh / Occlusion / Reflection
        public string scene;
        public double duration;           // 耗时（秒）
        public DateTime time;
    }

    /// <summary>
    /// 工具使用记录
    /// </summary>
    [Serializable]
    public class ToolUsageRecord
    {
        public string toolName;
        public int usageCount;
        public DateTime firstUsedTime;
        public DateTime lastUsedTime;
    }

    /// <summary>
    /// 慢操作记录
    /// </summary>
    [Serializable]
    public class SlowOperationRecord
    {
        public string actionName;
        public double duration;           // 耗时（秒）
        public DateTime time;
        public string details;
    }

    /// <summary>
    /// 统计数据容器
    /// </summary>
    [Serializable]
    public class TimeTrackerData
    {
        public List<SceneTimeRecord> sceneRecords = new List<SceneTimeRecord>();
        public List<BakeRecord> bakeRecords = new List<BakeRecord>();
        public List<ToolUsageRecord> toolUsageRecords = new List<ToolUsageRecord>();
        public List<SlowOperationRecord> slowOperations = new List<SlowOperationRecord>();
    }

    #endregion

    #region 单例和初始化

    private static TimeTrackerData _data;
    private static bool _initialized = false;
    private const string DATA_KEY = "EditorTimeTracker_Data";

    // 当前场景状态
    private static SceneTimeRecord _currentSceneRecord;
    private static DateTime _sceneOpenTime;
    private static DateTime _lastActiveTime;
    private static bool _isSceneActive;
    private const double ACTIVE_THRESHOLD = 5.0; // 5秒内无操作视为空转

    // 烘焙状态
    private static Dictionary<string, DateTime> _bakeStartTimes = new Dictionary<string, DateTime>();
    private static bool _isLightmapBaking = false;
    private static bool _isNavMeshBaking = false;

    // 慢操作阈值（毫秒）
    private const double SLOW_OPERATION_THRESHOLD_MS = 1000; // 1秒

    /// <summary>
    /// 初始化统计系统
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        LoadData();
        RegisterEvents();
        _initialized = true;
    }

    /// <summary>
    /// 清理统计系统
    /// </summary>
    public static void Cleanup()
    {
        if (!_initialized) return;

        SaveCurrentSceneRecord();
        UnregisterEvents();
        SaveData();
        _initialized = false;
    }

    #endregion

    #region 事件注册

    private static void RegisterEvents()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosed += OnSceneClosed;
        EditorApplication.update += OnUpdate;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        // 烘焙事件（通过反射或轮询检测）
        EditorApplication.update += CheckBakeStatus;
    }

    private static void UnregisterEvents()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneClosed -= OnSceneClosed;
        EditorApplication.update -= OnUpdate;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= CheckBakeStatus;
    }

    #endregion

    #region 场景时间统计

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        SaveCurrentSceneRecord();

        string scenePath = scene.path;
        _currentSceneRecord = _data.sceneRecords.FirstOrDefault(r => r.scenePath == scenePath);

        if (_currentSceneRecord == null)
        {
            _currentSceneRecord = new SceneTimeRecord
            {
                scenePath = scenePath,
                totalTime = 0,
                activeTime = 0,
                openCount = 0
            };
            _data.sceneRecords.Add(_currentSceneRecord);
        }

        _currentSceneRecord.openCount++;
        _currentSceneRecord.lastOpenTime = DateTime.Now;
        _sceneOpenTime = DateTime.Now;
        _lastActiveTime = DateTime.Now;
        _isSceneActive = true;

        SaveData();
    }

    private static void OnSceneClosed(Scene scene)
    {
        SaveCurrentSceneRecord();
    }

    private static void SaveCurrentSceneRecord()
    {
        if (_currentSceneRecord == null) return;

        DateTime now = DateTime.Now;
        double elapsed = (now - _sceneOpenTime).TotalSeconds;
        _currentSceneRecord.totalTime += elapsed;

        // 计算活跃时间：从上次活跃时间到现在的间隔
        double inactiveDuration = (now - _lastActiveTime).TotalSeconds;
        if (inactiveDuration <= ACTIVE_THRESHOLD)
        {
            // 如果距离上次操作在阈值内，累积为活跃时间
            _currentSceneRecord.activeTime += inactiveDuration;
        }
        // 如果超过阈值，说明已经空转，不累积活跃时间

        _currentSceneRecord.lastCloseTime = now;
        SaveData();
    }

    private static void OnUpdate()
    {
        if (_currentSceneRecord == null) return;

        // 定期检查并更新活跃时间
        // 如果距离上次操作超过阈值，标记为空转
        DateTime now = DateTime.Now;
        double inactiveDuration = (now - _lastActiveTime).TotalSeconds;
        
        if (inactiveDuration > ACTIVE_THRESHOLD && _isSceneActive)
        {
            // 累积活跃时间到阈值为止
            double activeElapsed = ACTIVE_THRESHOLD;
            _currentSceneRecord.activeTime += activeElapsed;
            
            // 更新场景打开时间，减去已经计算的部分
            _sceneOpenTime = _lastActiveTime.AddSeconds(ACTIVE_THRESHOLD);
            _isSceneActive = false;
        }
    }

    private static void OnUndoRedoPerformed()
    {
        MarkSceneActive();
    }

    private static void OnSelectionChanged()
    {
        MarkSceneActive();
    }

    private static void MarkSceneActive()
    {
        if (_currentSceneRecord == null) return;

        DateTime now = DateTime.Now;
        
        // 如果之前是活跃状态，累积活跃时间
        if (_isSceneActive)
        {
            double inactiveDuration = (now - _lastActiveTime).TotalSeconds;
            if (inactiveDuration <= ACTIVE_THRESHOLD)
            {
                _currentSceneRecord.activeTime += inactiveDuration;
            }
            else
            {
                // 超过阈值，只累积到阈值
                _currentSceneRecord.activeTime += ACTIVE_THRESHOLD;
            }
        }
        else
        {
            // 从空转恢复活跃，重新开始计时
            _sceneOpenTime = now;
        }

        _lastActiveTime = now;
        _isSceneActive = true;
    }

    private static DateTime _playModeEnterTime;
    
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // 记录进入 Play Mode 的开始时间
            _playModeEnterTime = DateTime.Now;
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // 计算进入 Play Mode 的耗时
            double duration = (DateTime.Now - _playModeEnterTime).TotalSeconds;
            if (duration > 0)
            {
                RecordSlowOperation("Enter Play Mode", duration, "场景切换到播放模式");
            }
        }
    }

    #endregion

    #region 烘焙耗时统计

    private static void CheckBakeStatus()
    {
        // 检测Lightmap烘焙
        bool isLightmapRunning = Lightmapping.isRunning;
        if (isLightmapRunning && !_isLightmapBaking)
        {
            StartBake("Lightmap");
            _isLightmapBaking = true;
        }
        else if (!isLightmapRunning && _isLightmapBaking)
        {
            EndBake("Lightmap");
            _isLightmapBaking = false;
        }

        // NavMesh烘焙需要通过其他方式检测（Unity没有直接的事件）
        // 这里可以通过轮询NavMeshBuilder状态来检测
    }

    private static void StartBake(string bakeType)
    {
        string key = $"{bakeType}_{SceneManager.GetActiveScene().path}";
        _bakeStartTimes[key] = DateTime.Now;
    }

    private static void EndBake(string bakeType)
    {
        string scenePath = SceneManager.GetActiveScene().path;
        string key = $"{bakeType}_{scenePath}";

        if (_bakeStartTimes.TryGetValue(key, out DateTime startTime))
        {
            double duration = (DateTime.Now - startTime).TotalSeconds;

            var record = new BakeRecord
            {
                type = bakeType,
                scene = scenePath,
                duration = duration,
                time = DateTime.Now
            };

            _data.bakeRecords.Add(record);

            // 只保留最近1000条记录
            if (_data.bakeRecords.Count > 1000)
            {
                _data.bakeRecords.RemoveAt(0);
            }

            // 如果烘焙耗时超过1秒，也记录为慢操作
            if (duration >= SLOW_OPERATION_THRESHOLD_MS / 1000.0)
            {
                string sceneName = GetSceneName(scenePath);
                RecordSlowOperation($"{bakeType} 烘焙", duration, $"场景: {sceneName}");
            }

            _bakeStartTimes.Remove(key);
            SaveData();
        }
    }

    /// <summary>
    /// 手动记录烘焙（供外部调用）
    /// </summary>
    public static void RecordBake(string bakeType, double durationSeconds)
    {
        var record = new BakeRecord
        {
            type = bakeType,
            scene = SceneManager.GetActiveScene().path,
            duration = durationSeconds,
            time = DateTime.Now
        };

        _data.bakeRecords.Add(record);

        // 只保留最近1000条记录
        if (_data.bakeRecords.Count > 1000)
        {
            _data.bakeRecords.RemoveAt(0);
        }

        SaveData();
    }

    #endregion

    #region 工具使用统计

    /// <summary>
    /// 记录工具使用
    /// </summary>
    public static void TrackToolUsage(string toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return;

        var record = _data.toolUsageRecords.FirstOrDefault(r => r.toolName == toolName);
        if (record == null)
        {
            record = new ToolUsageRecord
            {
                toolName = toolName,
                usageCount = 0,
                firstUsedTime = DateTime.Now
            };
            _data.toolUsageRecords.Add(record);
        }

        record.usageCount++;
        record.lastUsedTime = DateTime.Now;
        SaveData();
    }

    /// <summary>
    /// 获取工具使用排行
    /// </summary>
    public static List<ToolUsageRecord> GetToolUsageRanking(int topN = 10)
    {
        return _data.toolUsageRecords
            .OrderByDescending(r => r.usageCount)
            .Take(topN)
            .ToList();
    }

    #endregion

    #region 慢操作记录

    /// <summary>
    /// 记录慢操作
    /// </summary>
    public static void RecordSlowOperation(string actionName, Action action, string details = "")
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action?.Invoke();
        }
        finally
        {
            sw.Stop();
            double durationMs = sw.ElapsedMilliseconds;

            if (durationMs >= SLOW_OPERATION_THRESHOLD_MS)
            {
                var record = new SlowOperationRecord
                {
                    actionName = actionName,
                    duration = durationMs / 1000.0, // 转换为秒
                    time = DateTime.Now,
                    details = details
                };

                _data.slowOperations.Add(record);

                // 只保留最慢的50条记录
                if (_data.slowOperations.Count > 50)
                {
                    _data.slowOperations = _data.slowOperations
                        .OrderByDescending(r => r.duration)
                        .Take(50)
                        .ToList();
                }
                else
                {
                    // 按耗时降序排序
                    _data.slowOperations = _data.slowOperations
                        .OrderByDescending(r => r.duration)
                        .ToList();
                }

                SaveData();
            }
        }
    }

    /// <summary>
    /// 记录慢操作（直接传入耗时）
    /// </summary>
    public static void RecordSlowOperation(string actionName, double durationSeconds, string details = "")
    {
        if (durationSeconds * 1000 >= SLOW_OPERATION_THRESHOLD_MS)
        {
            var record = new SlowOperationRecord
            {
                actionName = actionName,
                duration = durationSeconds,
                time = DateTime.Now,
                details = details
            };

            _data.slowOperations.Add(record);

            // 只保留最慢的50条记录
            if (_data.slowOperations.Count > 50)
            {
                _data.slowOperations = _data.slowOperations
                    .OrderByDescending(r => r.duration)
                    .Take(50)
                    .ToList();
            }
            else
            {
                _data.slowOperations = _data.slowOperations
                    .OrderByDescending(r => r.duration)
                    .ToList();
            }

            SaveData();
        }
    }

    /// <summary>
    /// 获取最慢操作记录
    /// </summary>
    public static List<SlowOperationRecord> GetSlowOperations(int topN = 10)
    {
        return _data.slowOperations
            .OrderByDescending(r => r.duration)
            .Take(topN)
            .ToList();
    }

    #endregion

    #region 数据访问

    /// <summary>
    /// 获取所有场景记录
    /// </summary>
    public static List<SceneTimeRecord> GetSceneRecords()
    {
        return _data.sceneRecords.OrderByDescending(r => r.totalTime).ToList();
    }

    /// <summary>
    /// 获取所有烘焙记录
    /// </summary>
    public static List<BakeRecord> GetBakeRecords()
    {
        return _data.bakeRecords.OrderByDescending(r => r.time).ToList();
    }

    /// <summary>
    /// 获取指定场景的烘焙记录
    /// </summary>
    public static List<BakeRecord> GetBakeRecordsByScene(string scenePath)
    {
        return _data.bakeRecords
            .Where(r => r.scene == scenePath)
            .OrderByDescending(r => r.time)
            .ToList();
    }

    /// <summary>
    /// 获取指定类型的烘焙记录
    /// </summary>
    public static List<BakeRecord> GetBakeRecordsByType(string bakeType)
    {
        return _data.bakeRecords
            .Where(r => r.type == bakeType)
            .OrderByDescending(r => r.time)
            .ToList();
    }

    /// <summary>
    /// 获取总烘焙时间（按类型）
    /// </summary>
    public static Dictionary<string, double> GetTotalBakeTimeByType()
    {
        return _data.bakeRecords
            .GroupBy(r => r.type)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.duration));
    }

    /// <summary>
    /// 获取今日烘焙总时间
    /// </summary>
    public static double GetTodayBakeTime()
    {
        DateTime today = DateTime.Today;
        return _data.bakeRecords
            .Where(r => r.time >= today)
            .Sum(r => r.duration);
    }

    #endregion

    #region 数据持久化

    private static void LoadData()
    {
        string json = ToolboxSettings.GetString(DATA_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            _data = new TimeTrackerData();
        }
        else
        {
            try
            {
                _data = JsonUtility.FromJson<TimeTrackerData>(json);
                if (_data == null)
                {
                    _data = new TimeTrackerData();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"EditorTimeTracker: Failed to load data: {e.Message}");
                _data = new TimeTrackerData();
            }
        }

        // 确保列表不为null
        if (_data.sceneRecords == null) _data.sceneRecords = new List<SceneTimeRecord>();
        if (_data.bakeRecords == null) _data.bakeRecords = new List<BakeRecord>();
        if (_data.toolUsageRecords == null) _data.toolUsageRecords = new List<ToolUsageRecord>();
        if (_data.slowOperations == null) _data.slowOperations = new List<SlowOperationRecord>();
    }

    private static void SaveData()
    {
        try
        {
            string json = JsonUtility.ToJson(_data, true);
            ToolboxSettings.SetString(DATA_KEY, json);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"EditorTimeTracker: Failed to save data: {e.Message}");
        }
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public static void ClearAllData()
    {
        _data = new TimeTrackerData();
        SaveData();
    }

    /// <summary>
    /// 导出数据为JSON
    /// </summary>
    public static string ExportData()
    {
        return JsonUtility.ToJson(_data, true);
    }

    /// <summary>
    /// 从JSON导入数据
    /// </summary>
    public static bool ImportData(string json)
    {
        try
        {
            _data = JsonUtility.FromJson<TimeTrackerData>(json);
            if (_data == null)
            {
                return false;
            }

            // 确保列表不为null
            if (_data.sceneRecords == null) _data.sceneRecords = new List<SceneTimeRecord>();
            if (_data.bakeRecords == null) _data.bakeRecords = new List<BakeRecord>();
            if (_data.toolUsageRecords == null) _data.toolUsageRecords = new List<ToolUsageRecord>();
            if (_data.slowOperations == null) _data.slowOperations = new List<SlowOperationRecord>();

            SaveData();
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"EditorTimeTracker: Failed to import data: {e.Message}");
            return false;
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 格式化时间显示
    /// </summary>
    public static string FormatTime(double seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds:F1}秒";
        }
        else if (seconds < 3600)
        {
            int minutes = (int)(seconds / 60);
            double secs = seconds % 60;
            return $"{minutes}分{secs:F1}秒";
        }
        else
        {
            int hours = (int)(seconds / 3600);
            int minutes = (int)((seconds % 3600) / 60);
            return $"{hours}小时{minutes}分钟";
        }
    }

    /// <summary>
    /// 获取场景名称（从路径）
    /// </summary>
    public static string GetSceneName(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return "未保存场景";

        return Path.GetFileNameWithoutExtension(scenePath);
    }

    #endregion
}

