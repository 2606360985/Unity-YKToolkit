using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 查找重复物体逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class FindDuplicatesLogic
{
    /// <summary>
    /// 查找结果
    /// </summary>
    public class FindResult
    {
        public List<GameObject> Duplicates = new List<GameObject>();
        public Dictionary<string, List<GameObject>> GroupedDuplicates = new Dictionary<string, List<GameObject>>();
        public int GroupCount = 0;
    }

    /// <summary>
    /// 查找重复物体
    /// </summary>
    public static FindResult FindDuplicateObjects()
    {
        FindResult result = new FindResult();

        // 获取场景中所有物体（包括子物体和隐藏物体）
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);
        HashSet<GameObject> processed = new HashSet<GameObject>();

        const float positionThreshold = 0.001f;
        const float rotationThreshold = 0.1f;

        Debug.Log($"<color=cyan>开始扫描场景，共 {allObjects.Length} 个物体（包含所有层级）...</color>");

        for (int i = 0; i < allObjects.Length; i++)
        {
            if (processed.Contains(allObjects[i])) continue;

            GameObject obj1 = allObjects[i];

            // 检查是否有MeshFilter组件
            MeshFilter mf1 = obj1.GetComponent<MeshFilter>();
            if (mf1 == null || mf1.sharedMesh == null) continue;

            List<GameObject> group = new List<GameObject> { obj1 };

            for (int j = i + 1; j < allObjects.Length; j++)
            {
                GameObject obj2 = allObjects[j];
                if (processed.Contains(obj2)) continue;

                MeshFilter mf2 = obj2.GetComponent<MeshFilter>();
                if (mf2 == null || mf2.sharedMesh == null) continue;

                // 检查模型是否相同
                if (mf1.sharedMesh != mf2.sharedMesh) continue;

                // 检查位置是否相同（使用世界坐标）
                Vector3 posDiff = obj1.transform.position - obj2.transform.position;
                if (posDiff.magnitude > positionThreshold) continue;

                // 检查旋转是否相同（使用世界旋转）
                Quaternion rotDiff = obj1.transform.rotation * Quaternion.Inverse(obj2.transform.rotation);
                float angle = Mathf.Abs(Quaternion.Angle(Quaternion.identity, rotDiff));
                if (angle > rotationThreshold) continue;

                // 检查缩放是否相同（使用本地缩放）
                Vector3 scaleDiff = obj1.transform.localScale - obj2.transform.localScale;
                if (scaleDiff.magnitude > positionThreshold) continue;

                group.Add(obj2);
                processed.Add(obj2);
            }

            if (group.Count > 1)
            {
                result.Duplicates.AddRange(group);
                processed.Add(obj1);
            }
        }

        // 按层级分组
        if (result.Duplicates.Count > 0)
        {
            var grouped = result.Duplicates.GroupBy(obj => GetHierarchyPath(obj.transform));
            result.GroupCount = grouped.Count();

            foreach (var group in grouped)
            {
                result.GroupedDuplicates[group.Key] = group.ToList();
            }
        }

        return result;
    }

    /// <summary>
    /// 获取物体在层级中的完整路径
    /// </summary>
    private static string GetHierarchyPath(Transform transform)
    {
        List<string> path = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }
        return string.Join("/", path);
    }

    /// <summary>
    /// 显示查找结果
    /// </summary>
    public static void DisplayResult(FindResult result)
    {
        if (result.Duplicates.Count == 0)
        {
            EditorUtility.DisplayDialog("完成", "未发现重复物体。\n\n已扫描所有层级的物体（包括子物体）。", "确定");
            Debug.Log("<color=green>扫描完成：未发现重复物体</color>");
            return;
        }

        // 高亮显示重复物体
        EditorSelectionUtil.SetSelection(result.Duplicates.ToArray());

        // 按层级分组显示重复物体
        int groupCount = 0;
        foreach (var kvp in result.GroupedDuplicates)
        {
            groupCount++;
            var group = kvp.Value;
            Debug.LogWarning($"<color=yellow>重复组 #{groupCount} ({group.Count} 个物体):</color>");
            foreach (var dup in group)
            {
                string path = kvp.Key;
                Debug.LogWarning($"  • {path} | 位置: {dup.transform.position}", dup);
            }
        }

        EditorUtility.DisplayDialog("完成",
            $"发现 {result.Duplicates.Count} 个重复物体（共 {result.GroupCount} 组），已在 Hierarchy 中选中并高亮显示。\n\n" +
            "已扫描所有层级的物体（包括子物体）。",
            "确定");
    }
}

