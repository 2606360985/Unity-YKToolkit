using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 随机变换逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class RandomTransformLogic
{
    /// <summary>
    /// 随机化设置
    /// </summary>
    public class Settings
    {
        public float MinScale = 0.8f;
        public float MaxScale = 1.2f;
        public bool RandYRotation = true;
    }

    /// <summary>
    /// 应用随机效果
    /// </summary>
    public static void ApplyRandomization(Transform[] transforms, Settings settings)
    {
        if (transforms == null || transforms.Length == 0) return;

        UndoUtil.RecordObjects(transforms.Select(t => t.gameObject).ToArray(), "Randomize");

        foreach (var t in transforms)
        {
            if (settings.RandYRotation)
                t.Rotate(0, Random.Range(0, 360f), 0);

            t.localScale = Vector3.one * Random.Range(settings.MinScale, settings.MaxScale);
        }
    }
}

