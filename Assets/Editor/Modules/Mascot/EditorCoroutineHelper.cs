using System.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor协程辅助类 - 在Editor模式下运行协程
/// </summary>
public static class EditorCoroutineHelper
{
    private static System.Collections.Generic.List<IEnumerator> activeCoroutines = new System.Collections.Generic.List<IEnumerator>();
    private static bool isInitialized = false;

    /// <summary>
    /// 启动协程
    /// </summary>
    public static void StartCoroutine(IEnumerator coroutine)
    {
        if (!isInitialized)
        {
            EditorApplication.update += UpdateCoroutines;
            isInitialized = true;
        }

        if (coroutine != null)
        {
            activeCoroutines.Add(coroutine);
        }
    }

    /// <summary>
    /// 停止所有协程
    /// </summary>
    public static void StopAllCoroutines()
    {
        activeCoroutines.Clear();
        if (isInitialized)
        {
            EditorApplication.update -= UpdateCoroutines;
            isInitialized = false;
        }
    }

    /// <summary>
    /// 更新协程
    /// </summary>
    private static void UpdateCoroutines()
    {
        for (int i = activeCoroutines.Count - 1; i >= 0; i--)
        {
            if (!activeCoroutines[i].MoveNext())
            {
                activeCoroutines.RemoveAt(i);
            }
        }

        if (activeCoroutines.Count == 0 && isInitialized)
        {
            EditorApplication.update -= UpdateCoroutines;
            isInitialized = false;
        }
    }
}

