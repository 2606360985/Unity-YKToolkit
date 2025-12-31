using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 编辑器选择工具 - 封装Selection相关操作
/// </summary>
public static class EditorSelectionUtil
{
    /// <summary>
    /// 获取选中的GameObject数组
    /// </summary>
    public static GameObject[] GetSelectedGameObjects()
    {
        return Selection.gameObjects;
    }

    /// <summary>
    /// 获取选中的Transform数组
    /// </summary>
    public static Transform[] GetSelectedTransforms()
    {
        return Selection.transforms;
    }

    /// <summary>
    /// 获取选中的活动GameObject
    /// </summary>
    public static GameObject GetActiveGameObject()
    {
        return Selection.activeGameObject;
    }

    /// <summary>
    /// 设置选中的对象
    /// </summary>
    public static void SetSelection(Object[] objects)
    {
        Selection.objects = objects;
    }

    /// <summary>
    /// 设置选中的GameObject
    /// </summary>
    public static void SetActiveGameObject(GameObject go)
    {
        Selection.activeGameObject = go;
    }

    /// <summary>
    /// 是否有选中的对象
    /// </summary>
    public static bool HasSelection()
    {
        return Selection.objects != null && Selection.objects.Length > 0;
    }

    /// <summary>
    /// 是否有选中的GameObject
    /// </summary>
    public static bool HasSelectedGameObjects()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}

