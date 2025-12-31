using UnityEngine;
using UnityEditor;

/// <summary>
/// 布局助手逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class LayoutHelperLogic
{
    /// <summary>
    /// 偏移复制设置
    /// </summary>
    public class Settings
    {
        public Vector3 DuplicateOffset = new Vector3(2, 0, 0);
    }

    /// <summary>
    /// 偏移复制并移动
    /// </summary>
    public static void DuplicateWithOffset(GameObject source, Vector3 offset)
    {
        if (source == null) return;

        GameObject duplicate = Object.Instantiate(source, source.transform.parent);
        duplicate.name = source.name;
        UndoUtil.RegisterCreatedObjectUndo(duplicate, "Duplicate Offset");
        duplicate.transform.position = source.transform.position + offset;
        EditorSelectionUtil.SetActiveGameObject(duplicate);
    }

    /// <summary>
    /// 快速打组
    /// </summary>
    public static void QuickGroup(Transform[] transforms)
    {
        if (transforms == null || transforms.Length == 0) return;

        GameObject group = new GameObject("Group_New");
        UndoUtil.RegisterCreatedObjectUndo(group, "Quick Group");
        group.transform.position = transforms[0].position;

        foreach (var t in transforms)
        {
            UndoUtil.SetTransformParent(t, group.transform, "Group");
        }

        EditorSelectionUtil.SetActiveGameObject(group);
    }
}

