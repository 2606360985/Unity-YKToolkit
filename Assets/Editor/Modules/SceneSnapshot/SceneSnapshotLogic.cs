using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 场景状态快照/回滚系统 - 核心逻辑
/// </summary>
public static class SceneSnapshotLogic
{
    private const string DATA_KEY = "SceneSnapshot_Data";
    private static SceneSnapshotData _data;

    #region 数据结构

    [Serializable]
    public class SceneSnapshotData
    {
        public List<SceneSnapshot> snapshots = new List<SceneSnapshot>();
    }

    [Serializable]
    public class SceneSnapshot
    {
        public string id;
        public string name;
        public string scenePath;
        public string timestamp;
        public List<GameObjectSnapshot> gameObjects = new List<GameObjectSnapshot>();
        public LightmapSettingsSnapshot lightmapSettings = new LightmapSettingsSnapshot();
    }

    [Serializable]
    public class GameObjectSnapshot
    {
        public string instanceId; // Unity实例ID，用于匹配
        public string name;
        public string guid; // 唯一标识
        public int parentGuidIndex = -1; // 父对象的索引
        public TransformSnapshot transform = new TransformSnapshot();
        public List<ComponentSnapshot> components = new List<ComponentSnapshot>();
        public bool activeSelf;
        public int layer;
        public string tag;
    }

    [Serializable]
    public class TransformSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    [Serializable]
    public class ComponentSnapshot
    {
        public string typeName;
        public string dataJson; // 组件数据的JSON序列化
    }

    [Serializable]
    public class LightmapSettingsSnapshot
    {
        public int lightmapCount;
        public List<string> lightmapPaths = new List<string>();
        public List<string> lightmapDirPaths = new List<string>();
        public List<string> shadowMaskPaths = new List<string>();
    }

    [Serializable]
    public class SnapshotComparison
    {
        public string snapshotId1;
        public string snapshotId2;
        public List<string> addedObjects = new List<string>();
        public List<string> removedObjects = new List<string>();
        public List<string> modifiedObjects = new List<string>();
    }

    #endregion

    #region 初始化

    static SceneSnapshotLogic()
    {
        LoadData();
    }

    private static void LoadData()
    {
        string json = ToolboxSettings.GetString(DATA_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            _data = new SceneSnapshotData();
        }
        else
        {
            try
            {
                _data = JsonUtility.FromJson<SceneSnapshotData>(json);
                if (_data == null)
                {
                    _data = new SceneSnapshotData();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SceneSnapshot: Failed to load data: {e.Message}");
                _data = new SceneSnapshotData();
            }
        }

        if (_data.snapshots == null)
        {
            _data.snapshots = new List<SceneSnapshot>();
        }
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
            Debug.LogError($"SceneSnapshot: Failed to save data: {e.Message}");
        }
    }

    #endregion

    #region 快照管理

    /// <summary>
    /// 创建当前场景的快照
    /// </summary>
    public static SceneSnapshot CreateSnapshot(string name = null)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == null)
        {
            Debug.LogWarning("SceneSnapshot: 当前场景未保存，无法创建快照");
            return null;
        }

        SceneSnapshot snapshot = new SceneSnapshot
        {
            id = Guid.NewGuid().ToString(),
            name = name ?? $"快照_{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            gameObjects = new List<GameObjectSnapshot>(),
            lightmapSettings = CaptureLightmapSettings()
        };

        // 捕获所有GameObject
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        Dictionary<GameObject, int> objectToIndex = new Dictionary<GameObject, int>();

        // 第一遍：创建所有GameObject的快照
        foreach (GameObject obj in allObjects)
        {
            if (obj.hideFlags != HideFlags.None && obj.hideFlags != HideFlags.HideInHierarchy)
                continue; // 跳过隐藏的对象

            GameObjectSnapshot goSnapshot = CaptureGameObject(obj);
            snapshot.gameObjects.Add(goSnapshot);
            objectToIndex[obj] = snapshot.gameObjects.Count - 1;
        }

        // 第二遍：建立父子关系
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject obj = allObjects[i];
            if (!objectToIndex.ContainsKey(obj))
                continue;

            // 使用 objectToIndex 获取正确的索引，而不是使用 allObjects 的索引 i
            int snapshotIndex = objectToIndex[obj];
            
            if (obj.transform.parent != null && objectToIndex.ContainsKey(obj.transform.parent.gameObject))
            {
                snapshot.gameObjects[snapshotIndex].parentGuidIndex = objectToIndex[obj.transform.parent.gameObject];
            }
        }

        _data.snapshots.Add(snapshot);
        SaveData();

        Debug.Log($"<color=green>✓ 场景快照已创建: {snapshot.name}</color>");
        return snapshot;
    }

    /// <summary>
    /// 捕获GameObject的状态
    /// </summary>
    private static GameObjectSnapshot CaptureGameObject(GameObject obj)
    {
        GameObjectSnapshot snapshot = new GameObjectSnapshot
        {
            instanceId = obj.GetInstanceID().ToString(),
            name = obj.name,
            guid = Guid.NewGuid().ToString(),
            transform = new TransformSnapshot
            {
                position = obj.transform.localPosition,
                rotation = obj.transform.localRotation,
                scale = obj.transform.localScale
            },
            activeSelf = obj.activeSelf,
            layer = obj.layer,
            tag = obj.tag,
            components = new List<ComponentSnapshot>()
        };

        // 捕获重要组件
        Component[] components = obj.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp == null) continue;

            ComponentSnapshot compSnapshot = CaptureComponent(comp);
            if (compSnapshot != null)
            {
                snapshot.components.Add(compSnapshot);
            }
        }

        return snapshot;
    }

    /// <summary>
    /// 捕获组件数据
    /// </summary>
    private static ComponentSnapshot CaptureComponent(Component comp)
    {
        ComponentSnapshot snapshot = new ComponentSnapshot
        {
            typeName = comp.GetType().FullName
        };

        // 捕获特定组件的数据
        if (comp is Renderer renderer)
        {
            var rendererData = new RendererComponentData
            {
                sharedMaterials = renderer.sharedMaterials.Select(m => m != null ? AssetDatabase.GetAssetPath(m) : "").ToArray(),
                lightmapIndex = renderer.lightmapIndex,
                lightmapScaleOffset = renderer.lightmapScaleOffset,
                realtimeLightmapIndex = renderer.realtimeLightmapIndex,
                realtimeLightmapScaleOffset = renderer.realtimeLightmapScaleOffset
            };
            snapshot.dataJson = JsonUtility.ToJson(rendererData);
        }
        else if (comp is MeshFilter meshFilter)
        {
            var meshData = new MeshComponentData
            {
                sharedMeshPath = meshFilter.sharedMesh != null ? AssetDatabase.GetAssetPath(meshFilter.sharedMesh) : ""
            };
            snapshot.dataJson = JsonUtility.ToJson(meshData);
        }
        else if (comp is Light light)
        {
            var lightData = new LightComponentData
            {
                type = light.type.ToString(),
                color = light.color,
                intensity = light.intensity,
                range = light.range,
                spotAngle = light.spotAngle,
                shadows = light.shadows.ToString(),
                shadowStrength = light.shadowStrength
            };
            snapshot.dataJson = JsonUtility.ToJson(lightData);
        }
        else
        {
            // 对于其他组件，只保存类型信息
            snapshot.dataJson = "{}";
        }

        return snapshot;
    }

    [Serializable]
    private class RendererComponentData
    {
        public string[] sharedMaterials;
        public int lightmapIndex;
        public Vector4 lightmapScaleOffset;
        public int realtimeLightmapIndex;
        public Vector4 realtimeLightmapScaleOffset;
    }

    [Serializable]
    private class MeshComponentData
    {
        public string sharedMeshPath;
    }

    [Serializable]
    private class LightComponentData
    {
        public string type;
        public Color color;
        public float intensity;
        public float range;
        public float spotAngle;
        public string shadows;
        public float shadowStrength;
    }

    /// <summary>
    /// 捕获Lightmap设置
    /// </summary>
    private static LightmapSettingsSnapshot CaptureLightmapSettings()
    {
        LightmapSettingsSnapshot snapshot = new LightmapSettingsSnapshot();
        
        LightmapData[] lightmaps = LightmapSettings.lightmaps;
        snapshot.lightmapCount = lightmaps != null ? lightmaps.Length : 0;

        if (lightmaps != null)
        {
            foreach (var lightmap in lightmaps)
            {
                snapshot.lightmapPaths.Add(lightmap.lightmapColor != null ? AssetDatabase.GetAssetPath(lightmap.lightmapColor) : "");
                snapshot.lightmapDirPaths.Add(lightmap.lightmapDir != null ? AssetDatabase.GetAssetPath(lightmap.lightmapDir) : "");
                snapshot.shadowMaskPaths.Add(lightmap.shadowMask != null ? AssetDatabase.GetAssetPath(lightmap.shadowMask) : "");
            }
        }

        return snapshot;
    }

    /// <summary>
    /// 获取所有快照
    /// </summary>
    public static List<SceneSnapshot> GetAllSnapshots()
    {
        LoadData();
        return _data.snapshots ?? new List<SceneSnapshot>();
    }

    /// <summary>
    /// 根据ID获取快照
    /// </summary>
    public static SceneSnapshot GetSnapshot(string id)
    {
        LoadData();
        return _data.snapshots?.FirstOrDefault(s => s.id == id);
    }

    /// <summary>
    /// 删除快照
    /// </summary>
    public static bool DeleteSnapshot(string id)
    {
        LoadData();
        var snapshot = _data.snapshots?.FirstOrDefault(s => s.id == id);
        if (snapshot != null)
        {
            _data.snapshots.Remove(snapshot);
            SaveData();
            Debug.Log($"<color=yellow>✓ 快照已删除: {snapshot.name}</color>");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 清空所有快照
    /// </summary>
    public static void ClearAllSnapshots()
    {
        _data.snapshots.Clear();
        SaveData();
        Debug.Log("<color=yellow>✓ 所有快照已清空</color>");
    }

    #endregion

    #region 回滚功能

    // 回滚进度信息（用于协程）
    private static float _restoreProgress = 0f;
    private static bool _isRestoring = false;
    private static string _restoreStatus = "";

    /// <summary>
    /// 获取回滚进度
    /// </summary>
    public static float GetRestoreProgress() => _restoreProgress;

    /// <summary>
    /// 获取回滚状态
    /// </summary>
    public static string GetRestoreStatus() => _restoreStatus;

    /// <summary>
    /// 是否正在回滚
    /// </summary>
    public static bool IsRestoring() => _isRestoring;

    /// <summary>
    /// 回滚到指定快照（同步版本，用于小场景）
    /// </summary>
    public static bool RestoreSnapshot(string snapshotId)
    {
        var snapshot = GetSnapshot(snapshotId);
        if (snapshot == null)
        {
            Debug.LogError($"SceneSnapshot: 快照不存在: {snapshotId}");
            return false;
        }

        if (snapshot.scenePath != UnityEngine.SceneManagement.SceneManager.GetActiveScene().path)
        {
            Debug.LogWarning($"SceneSnapshot: 快照来自不同场景，可能无法完全恢复");
        }

        // 如果对象数量较多，使用协程版本
        if (snapshot.gameObjects.Count > 100)
        {
            Debug.LogWarning($"SceneSnapshot: 场景对象较多 ({snapshot.gameObjects.Count} 个)，建议使用协程版本以避免卡顿");
        }

        // 记录Undo
        Undo.SetCurrentGroupName($"Restore Snapshot: {snapshot.name}");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            // 1. 恢复Lightmap设置
            RestoreLightmapSettings(snapshot.lightmapSettings);

            // 2. 清除当前场景的所有对象（但保留场景本身）
            ClearSceneObjects();

            // 3. 恢复GameObject结构
            bool success = RestoreGameObjectsOptimized(snapshot);

            if (success)
            {
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log($"<color=green>✓ 场景已回滚到快照: {snapshot.name}</color>");
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SceneSnapshot: 回滚失败: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 回滚到指定快照（协程版本，用于大场景）
    /// </summary>
    public static System.Collections.IEnumerator RestoreSnapshotCoroutine(string snapshotId, System.Action<bool> onComplete = null)
    {
        _isRestoring = true;
        _restoreProgress = 0f;
        _restoreStatus = "初始化...";

        var snapshot = GetSnapshot(snapshotId);
        if (snapshot == null)
        {
            Debug.LogError($"SceneSnapshot: 快照不存在: {snapshotId}");
            _isRestoring = false;
            onComplete?.Invoke(false);
            yield break;
        }

        if (snapshot.scenePath != UnityEngine.SceneManagement.SceneManager.GetActiveScene().path)
        {
            Debug.LogWarning($"SceneSnapshot: 快照来自不同场景，可能无法完全恢复");
        }

        // 记录Undo
        Undo.SetCurrentGroupName($"Restore Snapshot: {snapshot.name}");
        int undoGroup = Undo.GetCurrentGroup();

        bool success = false;
        
        // 1. 恢复Lightmap设置
        _restoreStatus = "恢复Lightmap设置...";
        _restoreProgress = 0.05f;
        yield return null;
        
        try
        {
            RestoreLightmapSettings(snapshot.lightmapSettings);
        }
        catch (Exception e)
        {
            Debug.LogError($"SceneSnapshot: 恢复Lightmap设置失败: {e.Message}");
            _isRestoring = false;
            onComplete?.Invoke(false);
            yield break;
        }

        // 2. 清除当前场景的所有对象
        _restoreStatus = "清除场景对象...";
        _restoreProgress = 0.1f;
        yield return null;
        
        try
        {
            ClearSceneObjects();
        }
        catch (Exception e)
        {
            Debug.LogError($"SceneSnapshot: 清除场景对象失败: {e.Message}");
            _isRestoring = false;
            onComplete?.Invoke(false);
            yield break;
        }

        // 3. 恢复GameObject结构（分批处理）
        _restoreStatus = "恢复GameObject结构...";
        _restoreProgress = 0.15f;
        yield return null;

        // 使用协程恢复对象
        // RestoreGameObjectsCoroutine 内部已经处理了单个对象的错误
        // 这里只需要迭代协程，不需要额外的try-catch（因为不能在try-catch中使用yield）
        var restoreCoroutine = RestoreGameObjectsCoroutine(snapshot, (progress) => {
            _restoreProgress = 0.15f + 0.8f * progress;
            _restoreStatus = $"恢复对象中... ({Mathf.RoundToInt(progress * 100)}%)";
        });
        
        while (restoreCoroutine.MoveNext())
        {
            yield return restoreCoroutine.Current;
        }

        try
        {
            Undo.CollapseUndoOperations(undoGroup);
            _restoreProgress = 1f;
            _restoreStatus = "完成";
            Debug.Log($"<color=green>✓ 场景已回滚到快照: {snapshot.name}</color>");
            success = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"SceneSnapshot: 完成回滚操作失败: {e.Message}");
            success = false;
        }
        finally
        {
            _isRestoring = false;
            onComplete?.Invoke(success);
        }
    }

    /// <summary>
    /// 优化版本的恢复GameObject（使用字典优化索引查找）
    /// </summary>
    private static bool RestoreGameObjectsOptimized(SceneSnapshot snapshot)
    {
        Dictionary<int, GameObject> guidToObject = new Dictionary<int, GameObject>();
        
        // 创建快照对象到索引的映射（避免使用IndexOf）
        Dictionary<GameObjectSnapshot, int> snapshotToIndex = new Dictionary<GameObjectSnapshot, int>();
        for (int i = 0; i < snapshot.gameObjects.Count; i++)
        {
            snapshotToIndex[snapshot.gameObjects[i]] = i;
        }

        List<GameObjectSnapshot> sortedObjects = new List<GameObjectSnapshot>(snapshot.gameObjects);

        // 按层级排序：先创建父对象（parentGuidIndex为-1的是根对象）
        sortedObjects.Sort((a, b) => 
        {
            if (a.parentGuidIndex == -1 && b.parentGuidIndex != -1) return -1;
            if (a.parentGuidIndex != -1 && b.parentGuidIndex == -1) return 1;
            return a.parentGuidIndex.CompareTo(b.parentGuidIndex);
        });

        foreach (var goSnapshot in sortedObjects)
        {
            try
            {
                GameObject obj = RestoreGameObject(goSnapshot, guidToObject);
                if (obj != null)
                {
                    // 使用字典查找索引，而不是IndexOf
                    if (snapshotToIndex.TryGetValue(goSnapshot, out int index))
                    {
                        guidToObject[index] = obj;
                    }
                    else
                    {
                        Debug.LogWarning($"SceneSnapshot: 无法找到GameObject在快照中的索引: {goSnapshot.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SceneSnapshot: 恢复GameObject失败: {goSnapshot.name}, 错误: {e.Message}");
            }
        }

        return true;
    }

    /// <summary>
    /// 协程版本的恢复GameObject（分批处理）
    /// </summary>
    private static System.Collections.IEnumerator RestoreGameObjectsCoroutine(SceneSnapshot snapshot, System.Action<float> progressCallback)
    {
        Dictionary<int, GameObject> guidToObject = new Dictionary<int, GameObject>();
        
        // 创建快照对象到索引的映射（避免使用IndexOf）
        Dictionary<GameObjectSnapshot, int> snapshotToIndex = new Dictionary<GameObjectSnapshot, int>();
        for (int i = 0; i < snapshot.gameObjects.Count; i++)
        {
            snapshotToIndex[snapshot.gameObjects[i]] = i;
        }

        List<GameObjectSnapshot> sortedObjects = new List<GameObjectSnapshot>(snapshot.gameObjects);

        // 按层级排序：先创建父对象（parentGuidIndex为-1的是根对象）
        sortedObjects.Sort((a, b) => 
        {
            if (a.parentGuidIndex == -1 && b.parentGuidIndex != -1) return -1;
            if (a.parentGuidIndex != -1 && b.parentGuidIndex == -1) return 1;
            return a.parentGuidIndex.CompareTo(b.parentGuidIndex);
        });

        const int batchSize = 50; // 每批处理50个对象
        int processed = 0;
        int total = sortedObjects.Count;

        for (int i = 0; i < sortedObjects.Count; i++)
        {
            var goSnapshot = sortedObjects[i];
            try
            {
                GameObject obj = RestoreGameObject(goSnapshot, guidToObject);
                if (obj != null)
                {
                    // 使用字典查找索引，而不是IndexOf
                    if (snapshotToIndex.TryGetValue(goSnapshot, out int index))
                    {
                        guidToObject[index] = obj;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SceneSnapshot: 恢复GameObject失败: {goSnapshot.name}, 错误: {e.Message}");
            }

            processed++;

            // 每处理一批对象后，等待一帧
            if (processed % batchSize == 0 || i == sortedObjects.Count - 1)
            {
                progressCallback?.Invoke((float)processed / total);
                yield return null; // 等待一帧，避免卡顿
            }
        }
    }

    /// <summary>
    /// 清除场景对象（保留场景本身）
    /// </summary>
    private static void ClearSceneObjects()
    {
        // 获取所有根对象
        List<GameObject> rootObjects = new List<GameObject>();
        foreach (GameObject obj in UnityEngine.Object.FindObjectsOfType<GameObject>())
        {
            if (obj.transform.parent == null)
            {
                rootObjects.Add(obj);
            }
        }

        // 删除所有根对象（子对象会自动被删除）
        foreach (GameObject obj in rootObjects)
        {
            Undo.DestroyObjectImmediate(obj);
        }
    }

    /// <summary>
    /// 恢复GameObject
    /// </summary>
    private static GameObject RestoreGameObject(GameObjectSnapshot snapshot, Dictionary<int, GameObject> guidToObject)
    {
        GameObject obj = new GameObject(snapshot.name);
        Undo.RegisterCreatedObjectUndo(obj, "Restore GameObject");

        // 设置Transform
        obj.transform.localPosition = snapshot.transform.position;
        obj.transform.localRotation = snapshot.transform.rotation;
        obj.transform.localScale = snapshot.transform.scale;

        // 设置父对象
        if (snapshot.parentGuidIndex >= 0)
        {
            if (guidToObject.ContainsKey(snapshot.parentGuidIndex))
            {
                obj.transform.SetParent(guidToObject[snapshot.parentGuidIndex].transform);
            }
            else
            {
                Debug.LogWarning($"SceneSnapshot: 无法找到父对象 (索引: {snapshot.parentGuidIndex})，GameObject '{snapshot.name}' 将作为根对象创建");
            }
        }

        // 设置基本属性
        obj.SetActive(snapshot.activeSelf);
        obj.layer = snapshot.layer;
        obj.tag = snapshot.tag;

        // 恢复组件
        foreach (var compSnapshot in snapshot.components)
        {
            RestoreComponent(obj, compSnapshot);
        }

        return obj;
    }

    /// <summary>
    /// 恢复组件
    /// </summary>
    private static void RestoreComponent(GameObject obj, ComponentSnapshot snapshot)
    {
        Type componentType = Type.GetType(snapshot.typeName);
        if (componentType == null)
        {
            // 尝试从已加载的程序集中查找
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                componentType = assembly.GetType(snapshot.typeName);
                if (componentType != null) break;
            }
        }

        if (componentType == null)
        {
            Debug.LogWarning($"SceneSnapshot: 无法找到组件类型: {snapshot.typeName}");
            return;
        }

        Component comp = obj.GetComponent(componentType);
        if (comp == null)
        {
            try
            {
                comp = obj.AddComponent(componentType);
                if (comp != null)
                {
                    Undo.RegisterCreatedObjectUndo(comp, "Restore Component");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SceneSnapshot: 无法添加组件 {snapshot.typeName} 到 {obj.name}: {e.Message}");
                return;
            }
        }

        // 恢复组件数据
        if (comp is Renderer renderer && !string.IsNullOrEmpty(snapshot.dataJson))
        {
            try
            {
                var rendererData = JsonUtility.FromJson<RendererComponentData>(snapshot.dataJson);
                if (rendererData != null && rendererData.sharedMaterials != null)
                {
                    Material[] materials = new Material[rendererData.sharedMaterials.Length];
                    for (int i = 0; i < rendererData.sharedMaterials.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(rendererData.sharedMaterials[i]))
                        {
                            materials[i] = AssetDatabase.LoadAssetAtPath<Material>(rendererData.sharedMaterials[i]);
                        }
                    }
                    renderer.sharedMaterials = materials;
                }
                renderer.lightmapIndex = rendererData.lightmapIndex;
                renderer.lightmapScaleOffset = rendererData.lightmapScaleOffset;
                renderer.realtimeLightmapIndex = rendererData.realtimeLightmapIndex;
                renderer.realtimeLightmapScaleOffset = rendererData.realtimeLightmapScaleOffset;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SceneSnapshot: 恢复Renderer组件失败: {e.Message}");
            }
        }
        else if (comp is MeshFilter meshFilter && !string.IsNullOrEmpty(snapshot.dataJson))
        {
            try
            {
                var meshData = JsonUtility.FromJson<MeshComponentData>(snapshot.dataJson);
                if (meshData != null && !string.IsNullOrEmpty(meshData.sharedMeshPath))
                {
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshData.sharedMeshPath);
                    if (mesh != null)
                    {
                        meshFilter.sharedMesh = mesh;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SceneSnapshot: 恢复MeshFilter组件失败: {e.Message}");
            }
        }
        else if (comp is Light light && !string.IsNullOrEmpty(snapshot.dataJson))
        {
            try
            {
                var lightData = JsonUtility.FromJson<LightComponentData>(snapshot.dataJson);
                if (lightData != null)
                {
                    if (Enum.TryParse<LightType>(lightData.type, out LightType lightType))
                        light.type = lightType;
                    light.color = lightData.color;
                    light.intensity = lightData.intensity;
                    light.range = lightData.range;
                    light.spotAngle = lightData.spotAngle;
                    if (Enum.TryParse<LightShadows>(lightData.shadows, out LightShadows shadows))
                        light.shadows = shadows;
                    light.shadowStrength = lightData.shadowStrength;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SceneSnapshot: 恢复Light组件失败: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 恢复Lightmap设置
    /// </summary>
    private static void RestoreLightmapSettings(LightmapSettingsSnapshot snapshot)
    {
        if (snapshot.lightmapCount == 0)
        {
            LightmapSettings.lightmaps = new LightmapData[0];
            return;
        }

        // 确保列表大小一致，防止索引越界
        int maxCount = Math.Max(snapshot.lightmapCount, 
            Math.Max(snapshot.lightmapPaths?.Count ?? 0, 
            Math.Max(snapshot.lightmapDirPaths?.Count ?? 0, 
                     snapshot.shadowMaskPaths?.Count ?? 0)));
        
        LightmapData[] lightmaps = new LightmapData[snapshot.lightmapCount];
        for (int i = 0; i < snapshot.lightmapCount; i++)
        {
            lightmaps[i] = new LightmapData();
            
            // 安全地访问列表，防止索引越界
            if (snapshot.lightmapPaths != null && i < snapshot.lightmapPaths.Count && !string.IsNullOrEmpty(snapshot.lightmapPaths[i]))
            {
                lightmaps[i].lightmapColor = AssetDatabase.LoadAssetAtPath<Texture2D>(snapshot.lightmapPaths[i]);
            }
            if (snapshot.lightmapDirPaths != null && i < snapshot.lightmapDirPaths.Count && !string.IsNullOrEmpty(snapshot.lightmapDirPaths[i]))
            {
                lightmaps[i].lightmapDir = AssetDatabase.LoadAssetAtPath<Texture2D>(snapshot.lightmapDirPaths[i]);
            }
            if (snapshot.shadowMaskPaths != null && i < snapshot.shadowMaskPaths.Count && !string.IsNullOrEmpty(snapshot.shadowMaskPaths[i]))
            {
                lightmaps[i].shadowMask = AssetDatabase.LoadAssetAtPath<Texture2D>(snapshot.shadowMaskPaths[i]);
            }
        }

        LightmapSettings.lightmaps = lightmaps;
    }

    #endregion

    #region 对比功能

    /// <summary>
    /// 对比两个快照
    /// </summary>
    public static SnapshotComparison CompareSnapshots(string snapshotId1, string snapshotId2)
    {
        var snapshot1 = GetSnapshot(snapshotId1);
        var snapshot2 = GetSnapshot(snapshotId2);

        if (snapshot1 == null || snapshot2 == null)
        {
            Debug.LogError("SceneSnapshot: 无法对比，快照不存在");
            return null;
        }

        SnapshotComparison comparison = new SnapshotComparison
        {
            snapshotId1 = snapshotId1,
            snapshotId2 = snapshotId2
        };

        // 使用名称和位置来匹配对象
        Dictionary<string, GameObjectSnapshot> snapshot1Objects = new Dictionary<string, GameObjectSnapshot>();
        foreach (var obj in snapshot1.gameObjects)
        {
            string key = $"{obj.name}_{obj.transform.position}";
            snapshot1Objects[key] = obj;
        }

        Dictionary<string, GameObjectSnapshot> snapshot2Objects = new Dictionary<string, GameObjectSnapshot>();
        foreach (var obj in snapshot2.gameObjects)
        {
            string key = $"{obj.name}_{obj.transform.position}";
            snapshot2Objects[key] = obj;
        }

        // 找出新增的对象
        foreach (var kvp in snapshot2Objects)
        {
            if (!snapshot1Objects.ContainsKey(kvp.Key))
            {
                comparison.addedObjects.Add(kvp.Value.name);
            }
        }

        // 找出删除的对象
        foreach (var kvp in snapshot1Objects)
        {
            if (!snapshot2Objects.ContainsKey(kvp.Key))
            {
                comparison.removedObjects.Add(kvp.Key.Split('_')[0]);
            }
        }

        // 找出修改的对象（简化版：只检查Transform变化）
        foreach (var kvp in snapshot1Objects)
        {
            if (snapshot2Objects.ContainsKey(kvp.Key))
            {
                var obj1 = kvp.Value;
                var obj2 = snapshot2Objects[kvp.Key];
                if (!TransformEquals(obj1.transform, obj2.transform))
                {
                    comparison.modifiedObjects.Add(obj1.name);
                }
            }
        }

        return comparison;
    }

    private static bool TransformEquals(TransformSnapshot t1, TransformSnapshot t2)
    {
        return Vector3.Distance(t1.position, t2.position) < 0.001f &&
               Quaternion.Angle(t1.rotation, t2.rotation) < 0.1f &&
               Vector3.Distance(t1.scale, t2.scale) < 0.001f;
    }

    #endregion
}

