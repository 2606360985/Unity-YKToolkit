using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 快速对齐与等距分布逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class AlignAndDistributeLogic
{
    /// <summary>
    /// 对齐设置
    /// </summary>
    public class Settings
    {
        public int Axis = 0; // 0=X, 1=Y, 2=Z
        public bool IsDistributeMode = false; // false=对齐, true=等距分布
    }

    /// <summary>
    /// 对齐或等距分布物体
    /// </summary>
    public static void AlignAndDistribute(Transform[] transforms, Settings settings)
    {
        if (transforms == null || transforms.Length < 2)
        {
            Debug.LogWarning("AlignAndDistribute: Need at least 2 transforms");
            return;
        }

        UndoUtil.RecordObjects(transforms.Select(t => t.gameObject).ToArray(), 
            settings.IsDistributeMode ? "Distribute Objects" : "Align Objects");

        if (settings.IsDistributeMode)
        {
            DistributeObjects(transforms, settings.Axis);
        }
        else
        {
            AlignObjects(transforms, settings.Axis);
        }

        Debug.Log($"<color=green>成功{(settings.IsDistributeMode ? "等距分布" : "对齐")} {transforms.Length} 个物体！</color>");
    }

    /// <summary>
    /// 对齐物体
    /// </summary>
    private static void AlignObjects(Transform[] transforms, int axis)
    {
        // 计算所有物体的平均位置作为对齐目标
        Vector3 sumPos = Vector3.zero;
        foreach (var t in transforms) sumPos += t.position;
        Vector3 targetPos = sumPos / transforms.Length;

        foreach (var t in transforms)
        {
            Vector3 pos = t.position;
            if (axis == 0) pos.x = targetPos.x;
            else if (axis == 1) pos.y = targetPos.y;
            else if (axis == 2) pos.z = targetPos.z;
            t.position = pos;
        }
    }

    /// <summary>
    /// 等距分布物体
    /// </summary>
    private static void DistributeObjects(Transform[] transforms, int axis)
    {
        // 按指定轴排序
        List<Transform> sorted = new List<Transform>(transforms);
        sorted.Sort((a, b) =>
        {
            float valA = axis == 0 ? a.position.x : (axis == 1 ? a.position.y : a.position.z);
            float valB = axis == 0 ? b.position.x : (axis == 1 ? b.position.y : b.position.z);
            return valA.CompareTo(valB);
        });

        // 计算起始和结束位置
        float startVal = axis == 0 ? sorted[0].position.x : (axis == 1 ? sorted[0].position.y : sorted[0].position.z);
        float endVal = axis == 0 ? sorted[sorted.Count - 1].position.x : (axis == 1 ? sorted[sorted.Count - 1].position.y : sorted[sorted.Count - 1].position.z);
        float totalDistance = endVal - startVal;

        // 等距分布
        for (int i = 0; i < sorted.Count; i++)
        {
            float ratio = sorted.Count > 1 ? (float)i / (sorted.Count - 1) : 0f;
            float newVal = startVal + totalDistance * ratio;

            Vector3 pos = sorted[i].position;
            if (axis == 0) pos.x = newVal;
            else if (axis == 1) pos.y = newVal;
            else if (axis == 2) pos.z = newVal;
            sorted[i].position = pos;
        }
    }
}

