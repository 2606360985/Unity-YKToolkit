using UnityEngine;
using UnityEditor;

/// <summary>
/// 批量静态设置逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class BatchStaticLogic
{
    /// <summary>
    /// 静态设置
    /// </summary>
    public class Settings
    {
        public bool ContributeGI = true;
        public bool ReflectionProbeStatic = true;
        public bool OccluderStatic = false;
        public bool OccludeeStatic = false;
        public bool BatchingStatic = false;
        public bool NavigationStatic = false;
        public bool OffMeshLinkGeneration = false;
    }

    /// <summary>
    /// 应用静态设置
    /// </summary>
    public static void ApplyStaticFlags(GameObject[] objects, Settings settings)
    {
        if (objects == null || objects.Length == 0)
        {
            Debug.LogWarning("BatchStatic: No objects selected");
            return;
        }

        UndoUtil.RecordObjects(objects, "Batch Static Toggle");

        int count = 0;
        foreach (GameObject go in objects)
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);

            flags = SetStaticFlag(flags, StaticEditorFlags.ContributeGI, settings.ContributeGI);
            flags = SetStaticFlag(flags, StaticEditorFlags.ReflectionProbeStatic, settings.ReflectionProbeStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.OccluderStatic, settings.OccluderStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.OccludeeStatic, settings.OccludeeStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.BatchingStatic, settings.BatchingStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.NavigationStatic, settings.NavigationStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.OffMeshLinkGeneration, settings.OffMeshLinkGeneration);

            GameObjectUtility.SetStaticEditorFlags(go, flags);
            count++;
        }

        Debug.Log($"<color=green>成功设置 {count} 个物体的静态标志！</color>");
    }

    private static StaticEditorFlags SetStaticFlag(StaticEditorFlags flags, StaticEditorFlags flag, bool enable)
    {
        if (enable)
            return flags | flag;
        else
            return flags & ~flag;
    }
}

