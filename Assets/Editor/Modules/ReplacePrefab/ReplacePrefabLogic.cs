using UnityEngine;
using UnityEditor;

/// <summary>
/// 资产替换逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class ReplacePrefabLogic
{
    /// <summary>
    /// 用预制体替换选中的GameObject
    /// </summary>
    public static void ReplaceWithPrefab(GameObject[] targets, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("ReplacePrefab: Prefab is null");
            return;
        }

        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning("ReplacePrefab: No targets selected");
            return;
        }

        foreach (var target in targets)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            UndoUtil.RegisterCreatedObjectUndo(instance, "Replace");
            
            instance.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            instance.transform.localScale = target.transform.localScale;
            instance.transform.parent = target.transform.parent;
            
            UndoUtil.DestroyObjectImmediate(target);
        }
    }
}

