using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 动画设置辅助类 - 提供运行时和Editor模式的统一接口
/// </summary>
public static class MascotAnimationHelper
{
#if UNITY_EDITOR
    /// <summary>
    /// 设置动画（Editor模式，带过渡）
    /// </summary>
    public static void SetAnimation(GameObject character, MascotLogic.AnimationType animType, float transitionDuration = 0.2f)
    {
        if (character == null) return;

        Animator animator = character.GetComponent<Animator>();
        if (animator == null) return;

        string animName = animType.ToString().ToLower();
        // 修正attack拼写
        if (animName == "attack") animName = "atack";
        
        string controllerPath = $"Assets/Nine Pines Animation/2D Character Sprite Animation - Penguin/Animations/penguin_{animName}_01.controller";
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (controller != null)
        {
            // 如果控制器相同，使用CrossFade实现过渡
            if (animator.runtimeAnimatorController == controller && animator.isInitialized)
            {
                // 使用CrossFade实现平滑过渡
                animator.CrossFade(animName, transitionDuration);
            }
            else
            {
                // 控制器不同，直接切换
                animator.runtimeAnimatorController = controller;
                // 确保动画循环播放
                if (animator.isInitialized)
                {
                    animator.speed = 1f;
                }
            }
        }
    }
#else
    /// <summary>
    /// 设置动画（运行时模式 - 需要不同的实现）
    /// </summary>
    public static void SetAnimation(GameObject character, MascotLogic.AnimationType animType)
    {
        // 运行时模式需要从Resources加载或使用其他方式
        // 这里暂时留空，如果需要运行时支持可以后续实现
        Debug.LogWarning("MascotAnimationHelper.SetAnimation: Runtime mode not implemented yet");
    }
#endif
}

