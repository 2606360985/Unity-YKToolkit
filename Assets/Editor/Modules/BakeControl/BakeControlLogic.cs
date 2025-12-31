using UnityEngine;
using UnityEditor;

/// <summary>
/// 烘焙精度双档切换逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class BakeControlLogic
{
    /// <summary>
    /// 烘焙设置
    /// </summary>
    public class Settings
    {
        public bool IsPreviewMode = true;
        public int SelectedPreset = 0;
        public int DirectSamples = 16;
        public int IndirectSamples = 64;
        public int EnvSamples = 64;
        public int Bounces = 2;
        public bool StartBakeAfterSwitch = false;
    }

    /// <summary>
    /// 预设名称
    /// </summary>
    public static readonly string[] PresetNames = { 
        "极快预览", "中等预览", "高质预览", "标准生产", "高质生产", "影视级烘焙" 
    };

    /// <summary>
    /// 应用预设值
    /// </summary>
    public static void ApplyPreset(Settings settings)
    {
        if (settings.IsPreviewMode)
        {
            switch (settings.SelectedPreset)
            {
                case 0: // 极快预览
                    settings.DirectSamples = 8;
                    settings.IndirectSamples = 32;
                    settings.EnvSamples = 32;
                    settings.Bounces = 1;
                    break;
                case 1: // 中等预览
                    settings.DirectSamples = 16;
                    settings.IndirectSamples = 64;
                    settings.EnvSamples = 64;
                    settings.Bounces = 2;
                    break;
                default: // 高质预览
                    settings.DirectSamples = 32;
                    settings.IndirectSamples = 128;
                    settings.EnvSamples = 128;
                    settings.Bounces = 2;
                    break;
            }
        }
        else
        {
            switch (settings.SelectedPreset)
            {
                case 3: // 标准生产
                    settings.DirectSamples = 32;
                    settings.IndirectSamples = 512;
                    settings.EnvSamples = 256;
                    settings.Bounces = 2;
                    break;
                case 4: // 高质生产
                    settings.DirectSamples = 64;
                    settings.IndirectSamples = 1024;
                    settings.EnvSamples = 512;
                    settings.Bounces = 3;
                    break;
                case 5: // 影视级
                    settings.DirectSamples = 128;
                    settings.IndirectSamples = 2048;
                    settings.EnvSamples = 1024;
                    settings.Bounces = 4;
                    break;
                default:
                    settings.DirectSamples = 32;
                    settings.IndirectSamples = 256;
                    settings.EnvSamples = 256;
                    settings.Bounces = 2;
                    break;
            }
        }
    }

    /// <summary>
    /// 应用设置到资产
    /// </summary>
    public static bool ApplySettingsToAsset(Settings settings)
    {
        // 获取当前场景关联的 LightingSettings 资产 (Unity 2020.3+)
        LightingSettings lightingSettings = Lightmapping.lightingSettings;

        if (lightingSettings == null)
        {
            EditorUtility.DisplayDialog("未找到设置资产", 
                "当前场景未关联 'Lighting Settings' 资产！\n\n请在 Lighting 窗口点击 'New Lighting Settings' 按钮创建并保存。", 
                "确定");
            return false;
        }

        // 记录撤销并直接操作 API
        UndoUtil.RecordObject(lightingSettings, "Update Bake Settings");

        lightingSettings.directSampleCount = settings.DirectSamples;
        lightingSettings.indirectSampleCount = settings.IndirectSamples;
        lightingSettings.environmentSampleCount = settings.EnvSamples;
        lightingSettings.maxBounces = settings.Bounces;

        // 强制保存资产
        EditorUtility.SetDirty(lightingSettings);
        AssetUtil.SaveAssets();

        // 强制 UI 刷新
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        Debug.Log($"<color=green>✓ 已成功更新光照设置资产: {lightingSettings.name}</color>");
        return true;
    }

    /// <summary>
    /// 预估烘焙时间
    /// </summary>
    public static string EstimateBakeTime(Settings settings)
    {
        long complexity = (long)settings.DirectSamples * settings.IndirectSamples * (settings.Bounces + 1);
        if (complexity < 10000) return "极快 (< 2分钟)";
        if (complexity < 100000) return "中等 (5-15分钟)";
        if (complexity < 1000000) return "较慢 (30-60分钟)";
        return "漫长 (需数小时，建议挂机)";
    }

    /// <summary>
    /// 开始烘焙
    /// </summary>
    public static bool StartBake(Settings settings)
    {
        if (Lightmapping.isRunning)
        {
            if (EditorUtility.DisplayDialog("烘焙中", "当前正在进行烘焙，是否要取消当前任务？", "取消当前烘焙", "继续等待"))
            {
                Lightmapping.ForceStop();
            }
            return false;
        }

        // 烘焙前强制同步一次设置
        if (!ApplySettingsToAsset(settings))
        {
            return false;
        }

        if (Lightmapping.BakeAsync())
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取当前烘焙进度
    /// </summary>
    public static float GetBakeProgress()
    {
        return Lightmapping.buildProgress;
    }

    /// <summary>
    /// 是否正在烘焙
    /// </summary>
    public static bool IsBaking()
    {
        return Lightmapping.isRunning;
    }
}

