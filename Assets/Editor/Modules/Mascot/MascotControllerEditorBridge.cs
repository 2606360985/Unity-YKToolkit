using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor模式下的行为状态管理
/// </summary>
public class MascotControllerEditorBridge
{
    public enum BehaviorState
    {
        Idle,
        WalkingIn,
        WalkingOut,
        PlayingAction
    }

    public BehaviorState currentState = BehaviorState.Idle;
    public float stateTimer = 0f;
    public float stateDuration = 0f;
    public Vector3 moveStartPos;
    public Vector3 moveTargetPos;
    public MascotLogic.AnimationType currentAnimation = MascotLogic.AnimationType.Idle;
}

