using UnityEngine;
using UnityEditor;

/// <summary>
/// 撤销工具 - 封装Undo相关操作
/// </summary>
public static class UndoUtil
{
    /// <summary>
    /// 记录对象状态（用于撤销）
    /// </summary>
    public static void RecordObject(Object obj, string name)
    {
        Undo.RecordObject(obj, name);
    }

    /// <summary>
    /// 记录多个对象状态
    /// </summary>
    public static void RecordObjects(Object[] objects, string name)
    {
        Undo.RecordObjects(objects, name);
    }

    /// <summary>
    /// 注册创建的对象（用于撤销）
    /// </summary>
    public static void RegisterCreatedObjectUndo(Object obj, string name)
    {
        Undo.RegisterCreatedObjectUndo(obj, name);
    }

    /// <summary>
    /// 设置Transform父级（带撤销）
    /// </summary>
    public static void SetTransformParent(Transform child, Transform parent, string name)
    {
        Undo.SetTransformParent(child, parent, name);
    }

    /// <summary>
    /// 销毁对象（带撤销）
    /// </summary>
    public static void DestroyObjectImmediate(Object obj)
    {
        Undo.DestroyObjectImmediate(obj);
    }
}

