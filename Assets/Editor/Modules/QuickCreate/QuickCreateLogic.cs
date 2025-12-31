using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

/// <summary>
/// 快速创建助手逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class QuickCreateLogic
{
    /// <summary>
    /// 创建设置
    /// </summary>
    public class Settings
    {
        public bool CreateAtSelection = true;
    }

    /// <summary>
    /// 获取创建位置
    /// </summary>
    public static Vector3 GetCreatePosition(bool createAtSelection, Transform activeTransform)
    {
        // 如果勾选了在选中位置创建，且有选中物体
        if (createAtSelection && activeTransform != null)
            return activeTransform.position;

        // 否则创建在场景视口中心
        return SceneUtil.GetSceneViewPivot();
    }

    /// <summary>
    /// 创建基础模型
    /// </summary>
    public static GameObject CreatePrimitive(PrimitiveType type, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = "New_" + type.ToString();
        go.transform.position = position;

        UndoUtil.RegisterCreatedObjectUndo(go, "Create Primitive");
        EditorSelectionUtil.SetActiveGameObject(go);
        return go;
    }

    /// <summary>
    /// 创建灯光
    /// </summary>
    public static GameObject CreateLight(LightType type, Vector3 position)
    {
        GameObject go = new GameObject("New_" + type.ToString() + "Light");
        Light light = go.AddComponent<Light>();
        light.type = type;
        go.transform.position = position;

        // 针对不同灯光的默认强度优化
        if (type == LightType.Directional)
            go.transform.rotation = Quaternion.Euler(50, -30, 0);

        UndoUtil.RegisterCreatedObjectUndo(go, "Create Light");
        EditorSelectionUtil.SetActiveGameObject(go);
        return go;
    }

    /// <summary>
    /// 创建反射探针
    /// </summary>
    public static GameObject CreateReflectionProbe(Vector3 position)
    {
        GameObject go = new GameObject("New_ReflectionProbe");
        go.AddComponent<ReflectionProbe>();
        go.transform.position = position;

        UndoUtil.RegisterCreatedObjectUndo(go, "Create Reflection Probe");
        EditorSelectionUtil.SetActiveGameObject(go);
        return go;
    }

    /// <summary>
    /// 创建光照探针组
    /// </summary>
    public static GameObject CreateLightProbeGroup(Vector3 position)
    {
        GameObject go = new GameObject("New_LightProbeGroup");
        go.AddComponent<LightProbeGroup>();
        go.transform.position = position;

        UndoUtil.RegisterCreatedObjectUndo(go, "Create Light Probe Group");
        EditorSelectionUtil.SetActiveGameObject(go);
        return go;
    }

    /// <summary>
    /// 创建全局Volume（URP/HDRP）
    /// </summary>
    public static GameObject CreateVolume(Vector3 position)
    {
        GameObject go = new GameObject("New_Global_Volume");
        go.transform.position = position;

        bool volumeAdded = false;

        // 方法1: 直接尝试添加 Volume 组件（如果类型存在）
        try
        {
            // 检查 Volume 类型是否存在
            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, UnityEngine.Rendering.Core");
            if (volumeType == null)
                volumeType = Type.GetType("UnityEngine.Rendering.Volume, UnityEngine.Rendering.Runtime");
            if (volumeType == null)
            {
                // 尝试从已加载的程序集中查找
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    volumeType = assembly.GetType("UnityEngine.Rendering.Volume");
                    if (volumeType != null) break;
                }
            }

            if (volumeType != null)
            {
                var volume = go.AddComponent(volumeType);
                // 通过反射设置 isGlobal 属性
                var isGlobalProp = volumeType.GetProperty("isGlobal");
                if (isGlobalProp != null)
                {
                    isGlobalProp.SetValue(volume, true);
                    volumeAdded = true;
                    Debug.Log("<color=green>✓ 已成功创建全局 Volume。</color>");
                }
                else
                {
                    Debug.LogWarning("<color=yellow>Volume 组件已添加，但无法设置 isGlobal 属性。</color>");
                    volumeAdded = true; // 至少组件已添加
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"<color=yellow>添加 Volume 组件时出错: {e.Message}</color>");
        }

        // 方法2: 如果方法1失败，尝试使用 Unity 内置菜单项的方式
        if (!volumeAdded)
        {
            try
            {
                // 尝试调用 Unity 内置的创建方法
                Type menuItemType = Type.GetType("UnityEditor.Rendering.VolumeMenuItems, UnityEditor.Rendering.Core");
                if (menuItemType == null)
                {
                    // 从已加载的程序集中查找
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        menuItemType = assembly.GetType("UnityEditor.Rendering.VolumeMenuItems");
                        if (menuItemType != null) break;
                    }
                }

                if (menuItemType != null)
                {
                    var method = menuItemType.GetMethod("CreateGlobalVolume",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (method != null)
                    {
                        // 先删除之前创建的空物体
                        UnityEngine.Object.DestroyImmediate(go);
                        // 使用 MenuCommand 创建
                        method.Invoke(null, new object[] { new MenuCommand(null) });
                        volumeAdded = true;
                        Debug.Log("<color=green>✓ 已成功创建全局 Volume（通过 Unity 内置方法）。</color>");
                        return null; // 已经创建了新的物体，不需要返回原来的
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"<color=yellow>使用 Unity 内置方法失败: {e.Message}</color>");
            }
        }

        if (!volumeAdded)
        {
            Debug.LogWarning("<color=yellow>⚠ 无法创建 Volume 组件。\n" +
                "可能原因：\n" +
                "1. 项目未导入 URP 或 HDRP 包\n" +
                "2. Volume 类型在当前管线中不可用\n\n" +
                "建议：请手动在 GameObject 菜单中创建 Volume，或确保已导入正确的渲染管线包。</color>");
        }

        UndoUtil.RegisterCreatedObjectUndo(go, "Create Volume");
        EditorSelectionUtil.SetActiveGameObject(go);
        return go;
    }
}

