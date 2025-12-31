using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 物理对齐逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class AlignToGroundLogic
{
    /// <summary>
    /// 对齐设置
    /// </summary>
    public class Settings
    {
        public int GroundLayerMask = -1;
    }

    /// <summary>
    /// 对齐到地面
    /// </summary>
    public static void SnapToGround(Transform[] transforms, Settings settings)
    {
        if (transforms == null || transforms.Length == 0)
            return;

        UndoUtil.RecordObjects(transforms.Select(t => t.gameObject).ToArray(), "Snap To Ground");

        foreach (var t in transforms)
        {
            float height = 2.0f;
            if (t.TryGetComponent<Renderer>(out var r))
                height = r.bounds.size.y + 0.5f;

            if (Physics.Raycast(t.position + Vector3.up * height, Vector3.down, out RaycastHit hit, 
                2000f, settings.GroundLayerMask, QueryTriggerInteraction.Ignore))
            {
                // 避免射线打到自己或子物体
                if (hit.transform == t || hit.transform.IsChildOf(t))
                {
                    if (!Physics.Raycast(hit.point + Vector3.down * 0.1f, Vector3.down, out hit, 
                        2000f, settings.GroundLayerMask, QueryTriggerInteraction.Ignore))
                        continue;
                }

                Vector3 newPos = hit.point;
                // 如果有Renderer，保持物体底部对齐
                if (t.TryGetComponent<Renderer>(out var renderer))
                {
                    newPos.y += (t.position.y - renderer.bounds.min.y);
                }
                t.position = newPos;
            }
        }
    }
}

