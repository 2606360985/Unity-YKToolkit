using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 小吉祥物业务逻辑 - 运行时版本（枚举和类型定义）
/// </summary>
public static class MascotLogic
{
    /// <summary>
    /// 设置类
    /// </summary>
    [System.Serializable]
    public class Settings
    {
        public bool AutoBehavior = true;
        public bool MouseInteraction = true;
    }

    /// <summary>
    /// 动画类型枚举
    /// </summary>
    public enum AnimationType
    {
        Idle,
        Walk,
        Attack,
        Jump,
        Preslide,
        Slide
    }

    /// <summary>
    /// 行为类型枚举
    /// </summary>
    public enum BehaviorType
    {
        Idle,           // 原地待机
        WalkIn,         // 从左边跑进来
        WalkOut,        // 跑出去
        RandomAction    // 随机动作
    }

#if UNITY_EDITOR
    /// <summary>
    /// 获取动画控制器路径（Editor专用）
    /// </summary>
    public static string GetAnimationControllerPath(AnimationType animType)
    {
        string animName = animType.ToString().ToLower();
        // 修正attack拼写
        if (animName == "attack") animName = "atack";
        
        return $"Assets/Nine Pines Animation/2D Character Sprite Animation - Penguin/Animations/penguin_{animName}_01.controller";
    }

    /// <summary>
    /// 设置动画（Editor专用）
    /// </summary>
    public static void SetAnimation(GameObject character, AnimationType animType)
    {
        if (character == null) return;

        Animator animator = character.GetComponent<Animator>();
        if (animator == null) return;

        string controllerPath = GetAnimationControllerPath(animType);
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }
    }
#endif
}

