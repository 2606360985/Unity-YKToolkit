using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 系统清理逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class SystemCleanupLogic
{
    /// <summary>
    /// 查找场景中缺失脚本的物体
    /// </summary>
    /// <returns>找到的缺失脚本物体列表</returns>
    public static GameObject[] FindMissingScripts()
    {
        List<GameObject> missingScriptObjects = new List<GameObject>();
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
            {
                Debug.LogWarning($"Missing: {go.name}", go);
                missingScriptObjects.Add(go);
            }
        }
        return missingScriptObjects.ToArray();
    }

    /// <summary>
    /// 移除指定物体上的所有缺失脚本组件
    /// </summary>
    public static int RemoveMissingScripts(GameObject go)
    {
        int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        if (removedCount > 0)
        {
            Debug.Log($"<color=green>✓ 已从 {go.name} 移除 {removedCount} 个缺失脚本组件</color>", go);
        }
        return removedCount;
    }

    /// <summary>
    /// 移除场景中所有物体上的缺失脚本组件
    /// </summary>
    /// <returns>移除的组件总数</returns>
    public static int RemoveAllMissingScripts()
    {
        int totalRemoved = 0;
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);
        
        foreach (var go in allObjects)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            totalRemoved += removed;
        }

        if (totalRemoved > 0)
        {
            Debug.Log($"<color=green>✓ 已移除场景中所有缺失脚本组件，共 {totalRemoved} 个</color>");
        }
        else
        {
            Debug.Log("<color=green>✓ 场景中没有发现缺失脚本组件</color>");
        }

        return totalRemoved;
    }

    /// <summary>
    /// 清空所有本地缓存（PlayerPrefs）
    /// </summary>
    public static void ClearCache()
    {
        PlayerPrefs.DeleteAll();
        Debug.Log("<color=green>✓ 已清空所有本地缓存</color>");
    }

    /// <summary>
    /// 按标签选择物体
    /// </summary>
    public static GameObject[] SelectByTag(string tag)
    {
        try
        {
            return GameObject.FindGameObjectsWithTag(tag);
        }
        catch
        {
            return new GameObject[0];
        }
    }
}

