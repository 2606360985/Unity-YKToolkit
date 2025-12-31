using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 资产工具 - 封装AssetDatabase相关操作
/// </summary>
public static class AssetUtil
{
    /// <summary>
    /// 重命名资产
    /// </summary>
    public static bool RenameAsset(Object asset, string newName)
    {
        if (asset == null || string.IsNullOrEmpty(newName))
            return false;

        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
            return false;

        string result = AssetDatabase.RenameAsset(path, newName);
        if (!string.IsNullOrEmpty(result))
        {
            Debug.LogError($"Failed to rename asset: {result}");
            return false;
        }

        AssetDatabase.SaveAssets();
        return true;
    }

    /// <summary>
    /// 获取资产路径
    /// </summary>
    public static string GetAssetPath(Object asset)
    {
        return AssetDatabase.GetAssetPath(asset);
    }

    /// <summary>
    /// 检查是否为资产
    /// </summary>
    public static bool IsAsset(Object obj)
    {
        return AssetDatabase.Contains(obj);
    }

    /// <summary>
    /// 从文件夹获取所有指定类型的资产
    /// </summary>
    public static T[] FindAssetsInFolder<T>(string folderPath) where T : Object
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
            return new T[0];

        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
        List<T> assets = new List<T>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                assets.Add(asset);
        }

        return assets.ToArray();
    }

    /// <summary>
    /// 创建资产
    /// </summary>
    public static void CreateAsset(Object asset, string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 刷新资产数据库
    /// </summary>
    public static void RefreshAssets()
    {
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 保存资产
    /// </summary>
    public static void SaveAssets()
    {
        AssetDatabase.SaveAssets();
    }
}

