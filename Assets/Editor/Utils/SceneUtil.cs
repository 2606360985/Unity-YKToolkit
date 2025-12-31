using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 场景工具 - 封装场景相关操作
/// </summary>
public static class SceneUtil
{
    /// <summary>
    /// LayerMask 字段（用于编辑器UI）
    /// </summary>
    public static int LayerMaskField(string label, int mask)
    {
        List<string> layerNames = new List<string>();
        List<int> layerNumbers = new List<int>();
        
        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                layerNames.Add(layerName);
                layerNumbers.Add(i);
            }
        }
        
        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & mask) != 0)
                maskWithoutEmpty |= (1 << i);
        }
        
        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layerNames.ToArray());
        
        int maskWithEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
                maskWithEmpty |= (1 << layerNumbers[i]);
        }
        
        return maskWithEmpty;
    }

    /// <summary>
    /// 获取场景视口中心位置
    /// </summary>
    public static Vector3 GetSceneViewPivot()
    {
        if (SceneView.lastActiveSceneView != null)
            return SceneView.lastActiveSceneView.pivot;
        return Vector3.zero;
    }
}

