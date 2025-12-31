using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 批量重命名逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class BatchRenameLogic
{
    /// <summary>
    /// 批量重命名设置
    /// </summary>
    public class BatchRenameSettings
    {
        public string Prefix = "";
        public string Base = "Object";
        public string Suffix = "";
        public int StartIndex = 0;
        public int Digits = 2;
        public bool ReplaceAll = true;
    }

    /// <summary>
    /// 执行批量重命名
    /// </summary>
    public static void ExecuteBatchRename(Object[] objects, BatchRenameSettings settings)
    {
        if (objects == null || objects.Length == 0)
        {
            Debug.LogWarning("BatchRename: No objects selected");
            return;
        }

        UndoUtil.RecordObjects(objects, "Batch Rename");

        for (int i = 0; i < objects.Length; i++)
        {
            string index = (settings.StartIndex + i).ToString("D" + settings.Digits);
            string baseName = settings.ReplaceAll ? settings.Base : objects[i].name;
            string newName = $"{settings.Prefix}{baseName}_{index}{settings.Suffix}";

            if (AssetUtil.IsAsset(objects[i]))
            {
                AssetUtil.RenameAsset(objects[i], newName);
            }
            else
            {
                objects[i].name = newName;
            }
        }

        AssetUtil.SaveAssets();
        Debug.Log($"<color=green>成功重命名 {objects.Length} 个对象</color>");
    }

    /// <summary>
    /// 验证设置是否有效
    /// </summary>
    public static bool ValidateSettings(BatchRenameSettings settings)
    {
        if (settings.Digits < 1 || settings.Digits > 5)
        {
            Debug.LogWarning("BatchRename: Digits must be between 1 and 5");
            return false;
        }

        if (settings.StartIndex < 0)
        {
            Debug.LogWarning("BatchRename: StartIndex must be >= 0");
            return false;
        }

        return true;
    }
}

