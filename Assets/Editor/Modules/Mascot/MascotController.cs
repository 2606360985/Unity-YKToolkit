using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 小吉祥物控制器 - 控制角色的动画和行为
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class MascotController : MonoBehaviour
{
    [Header("行为设置")]
    public bool autoBehavior = true;
    public bool mouseInteraction = true;

    [Header("移动设置")]
    public float walkSpeed = 2f;
    public float screenBounds = 10f; // 屏幕边界（世界坐标）

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private MascotLogic.BehaviorType currentBehavior;
    private Coroutine behaviorCoroutine;
    private bool isMoving = false;
    private float idleTimer = 0f;
    private float idleDuration = 3f; // idle持续时间
    private float randomActionTimer = 0f;
    private float randomActionInterval = 5f; // 随机动作间隔

    /// <summary>
    /// 是否正在移动（公共属性）
    /// </summary>
    public bool IsMoving => isMoving;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        
#if UNITY_EDITOR
        // 确保Editor桥接已初始化
        if (editorBridge == null)
        {
            editorBridge = new MascotControllerEditorBridge();
        }
#endif
    }

    public void Initialize()
    {
        if (autoBehavior)
        {
            StartAutoBehavior();
        }
    }

    /// <summary>
    /// Editor模式下的更新（由外部调用）
    /// </summary>
    public void EditorUpdate(float deltaTime)
    {
#if UNITY_EDITOR
        if (autoBehavior && isAutoBehaviorRunning)
        {
            EditorUpdateBehavior(deltaTime);
        }
#else
        if (!autoBehavior) return;

        // 更新idle计时器
        if (currentBehavior == MascotLogic.BehaviorType.Idle && !isMoving)
        {
            idleTimer += deltaTime;
            if (idleTimer >= idleDuration)
            {
                // idle时间到了，执行随机行为
                ExecuteRandomBehavior();
            }
        }

        // 更新随机动作计时器
        randomActionTimer += deltaTime;
        if (randomActionTimer >= randomActionInterval && !isMoving)
        {
            randomActionTimer = 0f;
            // 随机执行一个动作（attack, jump等）
            PlayRandomAction();
        }
#endif
    }

    private bool isAutoBehaviorRunning = false;
    
#if UNITY_EDITOR
    public MascotControllerEditorBridge editorBridge = new MascotControllerEditorBridge();
#else
    public MascotControllerEditorBridge editorBridge = null;
#endif

    /// <summary>
    /// 开始自动行为
    /// </summary>
    public void StartAutoBehavior()
    {
        if (isAutoBehaviorRunning) return;
        isAutoBehaviorRunning = true;
        
#if UNITY_EDITOR
        // Editor模式下不使用协程，由EditorWindow驱动
        // 立即开始一个行为
        StartNextRandomBehavior();
#else
        // 运行时使用MonoBehaviour协程
        behaviorCoroutine = StartCoroutine(AutoBehaviorCoroutine());
#endif
    }

    /// <summary>
    /// 停止自动行为
    /// </summary>
    public void StopAutoBehavior()
    {
        isAutoBehaviorRunning = false;
        isMoving = false;
        
#if UNITY_EDITOR
        if (editorBridge != null)
        {
            editorBridge.currentState = MascotControllerEditorBridge.BehaviorState.Idle;
        }
#endif
        
        if (behaviorCoroutine != null)
        {
            StopCoroutine(behaviorCoroutine);
            behaviorCoroutine = null;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor模式下的更新（由EditorWindow调用）
    /// </summary>
    public void EditorUpdateBehavior(float deltaTime)
    {
#if UNITY_EDITOR
        if (!autoBehavior || !isAutoBehaviorRunning) return;
        if (editorBridge == null) return;

        editorBridge.stateTimer += deltaTime;

        switch (editorBridge.currentState)
        {
            case MascotControllerEditorBridge.BehaviorState.Idle:
                UpdateIdleState(deltaTime);
                break;
            case MascotControllerEditorBridge.BehaviorState.WalkingIn:
            case MascotControllerEditorBridge.BehaviorState.WalkingOut:
                UpdateWalkingState(deltaTime);
                break;
            case MascotControllerEditorBridge.BehaviorState.PlayingAction:
                UpdateActionState(deltaTime);
                break;
        }
#endif
    }

    private void UpdateIdleState(float deltaTime)
    {
#if UNITY_EDITOR
        if (editorBridge == null) return;
        
        idleTimer += deltaTime;
        
        // 随机动作计时器
        randomActionTimer += deltaTime;
        if (randomActionTimer >= randomActionInterval)
        {
            randomActionTimer = 0f;
            PlayRandomAction();
            return;
        }

        // Idle时间到了，执行随机行为
        if (idleTimer >= idleDuration)
        {
            StartNextRandomBehavior();
        }
#endif
    }

    private void UpdateWalkingState(float deltaTime)
    {
#if UNITY_EDITOR
        if (editorBridge == null) return;
        
        if (editorBridge.stateTimer >= editorBridge.stateDuration)
        {
            // 移动完成
            transform.position = editorBridge.moveTargetPos;
            isMoving = false;
            
            if (editorBridge.currentState == MascotControllerEditorBridge.BehaviorState.WalkingIn)
            {
                // 切换到idle
                editorBridge.currentState = MascotControllerEditorBridge.BehaviorState.Idle;
                editorBridge.currentAnimation = MascotLogic.AnimationType.Idle;
                MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Idle);
                idleTimer = 0f;
            }
            else
            {
                // 跑出去了，等待一段时间后重新开始
                editorBridge.stateTimer = 0f;
                editorBridge.stateDuration = Random.Range(2f, 4f);
            }
        }
        else
        {
            // 更新位置
            float t = Mathf.Clamp01(editorBridge.stateTimer / editorBridge.stateDuration);
            transform.position = Vector3.Lerp(editorBridge.moveStartPos, editorBridge.moveTargetPos, t);
        }
#endif
    }

    private void UpdateActionState(float deltaTime)
    {
#if UNITY_EDITOR
        if (editorBridge == null) return;
        
        if (editorBridge.stateTimer >= editorBridge.stateDuration)
        {
            // 动作完成，切回idle
            editorBridge.currentState = MascotControllerEditorBridge.BehaviorState.Idle;
            editorBridge.currentAnimation = MascotLogic.AnimationType.Idle;
            MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Idle);
            idleTimer = 0f;
        }
#endif
    }

    private void StartNextRandomBehavior()
    {
#if UNITY_EDITOR
        if (editorBridge == null) return;
        
        float rand = Random.value;
        if (rand < 0.3f)
        {
            // 30%概率：从左边跑进来
            StartWalkInBehavior();
        }
        else if (rand < 0.6f)
        {
            // 30%概率：跑出去
            StartWalkOutBehavior();
        }
        else
        {
            // 40%概率：继续idle
            editorBridge.currentState = MascotControllerEditorBridge.BehaviorState.Idle;
            editorBridge.currentAnimation = MascotLogic.AnimationType.Idle;
            MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Idle);
            idleTimer = 0f;
            editorBridge.stateTimer = 0f;
            editorBridge.stateDuration = Random.Range(3f, 6f);
        }
#endif
    }

    private void StartWalkInBehavior()
    {
#if UNITY_EDITOR
        if (editorBridge == null) return;
        
        editorBridge.currentState = MascotControllerEditorBridge.BehaviorState.WalkingIn;
        isMoving = true;
        
        editorBridge.moveStartPos = new Vector3(-screenBounds - 2f, transform.position.y, transform.position.z);
        editorBridge.moveTargetPos = new Vector3(0f, transform.position.y, transform.position.z);
        transform.position = editorBridge.moveStartPos;
        
        spriteRenderer.flipX = false;
        editorBridge.currentAnimation = MascotLogic.AnimationType.Walk;
        MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Walk);
        
        float distance = Vector3.Distance(editorBridge.moveStartPos, editorBridge.moveTargetPos);
        editorBridge.stateDuration = distance / walkSpeed;
        editorBridge.stateTimer = 0f;
#endif
    }

    private void StartWalkOutBehavior()
    {
#if UNITY_EDITOR
        if (editorBridge == null) return;
        
        editorBridge.currentState = MascotControllerEditorBridge.BehaviorState.WalkingOut;
        isMoving = true;
        
        editorBridge.moveStartPos = transform.position;
        editorBridge.moveTargetPos = new Vector3(screenBounds + 2f, transform.position.y, transform.position.z);
        
        spriteRenderer.flipX = false;
        editorBridge.currentAnimation = MascotLogic.AnimationType.Walk;
        MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Walk);
        
        float distance = Vector3.Distance(editorBridge.moveStartPos, editorBridge.moveTargetPos);
        editorBridge.stateDuration = distance / walkSpeed;
        editorBridge.stateTimer = 0f;
#endif
    }
#endif

    /// <summary>
    /// 自动行为协程
    /// </summary>
    private IEnumerator AutoBehaviorCoroutine()
    {
        while (autoBehavior && isAutoBehaviorRunning)
        {
            // 随机选择行为
            float rand = Random.value;
            if (rand < 0.3f)
            {
                // 30%概率：从左边跑进来
                yield return WalkInBehavior();
            }
            else if (rand < 0.6f)
            {
                // 30%概率：跑出去
                yield return WalkOutBehavior();
            }
            else
            {
                // 40%概率：原地待机
                yield return IdleBehavior();
            }

            // 行为之间的间隔
            float waitTime = Random.Range(2f, 4f);
            float elapsed = 0f;
            while (elapsed < waitTime && isAutoBehaviorRunning)
            {
                elapsed += 0.016f; // 假设60fps
                yield return null;
            }
        }
        isAutoBehaviorRunning = false;
    }

    /// <summary>
    /// Idle行为
    /// </summary>
    private IEnumerator IdleBehavior()
    {
        currentBehavior = MascotLogic.BehaviorType.Idle;
        isMoving = false;
        idleTimer = 0f;

        MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Idle);
        
        float waitTime = Random.Range(3f, 6f);
        float elapsed = 0f;
        while (elapsed < waitTime)
        {
            elapsed += 0.016f;
            yield return null;
        }
    }

    /// <summary>
    /// 从左边跑进来
    /// </summary>
    private IEnumerator WalkInBehavior()
    {
        currentBehavior = MascotLogic.BehaviorType.WalkIn;
        isMoving = true;

        // 设置初始位置（屏幕左侧外）
        Vector3 startPos = new Vector3(-screenBounds - 2f, transform.position.y, transform.position.z);
        Vector3 targetPos = new Vector3(0f, transform.position.y, transform.position.z);
        transform.position = startPos;

        // 面向右
        spriteRenderer.flipX = false;

        // 播放walk动画
        MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Walk);

        // 移动到目标位置
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / walkSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += 0.016f; // Editor模式下的固定deltaTime
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        isMoving = false;

        // 切换到idle
        MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Idle);
        currentBehavior = MascotLogic.BehaviorType.Idle;
    }

    /// <summary>
    /// 跑出去
    /// </summary>
    private IEnumerator WalkOutBehavior()
    {
        currentBehavior = MascotLogic.BehaviorType.WalkOut;
        isMoving = true;

        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(screenBounds + 2f, transform.position.y, transform.position.z);

        // 面向右
        spriteRenderer.flipX = false;

        // 播放walk动画
        MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Walk);

        // 移动到目标位置
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / walkSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += 0.016f; // Editor模式下的固定deltaTime
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        isMoving = false;
    }

    /// <summary>
    /// 执行随机行为（仅运行时模式）
    /// </summary>
    private void ExecuteRandomBehavior()
    {
#if UNITY_EDITOR
        // Editor模式下不应该调用这个方法
        // Editor模式下的行为由EditorUpdateBehavior管理
        return;
#else
        float rand = Random.value;
        if (rand < 0.5f)
        {
            StartCoroutine(WalkInBehavior());
        }
        else
        {
            StartCoroutine(WalkOutBehavior());
        }
#endif
    }

    /// <summary>
    /// 播放随机动作
    /// </summary>
    private void PlayRandomAction()
    {
        if (isMoving) return;

        MascotLogic.AnimationType[] actions = {
            MascotLogic.AnimationType.Attack,
            MascotLogic.AnimationType.Jump,
            MascotLogic.AnimationType.Preslide,
            MascotLogic.AnimationType.Slide
        };

        MascotLogic.AnimationType randomAction = actions[Random.Range(0, actions.Length)];
        MascotAnimationHelper.SetAnimation(gameObject, randomAction);

#if UNITY_EDITOR
        // Editor模式下直接设置状态
#if UNITY_EDITOR
        if (editorBridge != null)
        {
            editorBridge.currentState = MascotControllerEditorBridge.BehaviorState.PlayingAction;
            editorBridge.currentAnimation = randomAction;
            editorBridge.stateTimer = 0f;
            editorBridge.stateDuration = 1f; // 动作持续时间
        }
#endif
#else
        // 动作播放完后切回idle
        StartCoroutine(ReturnToIdleAfterAction());
#endif
    }

    /// <summary>
    /// 动作播放完后切回idle
    /// </summary>
    private IEnumerator ReturnToIdleAfterAction()
    {
        float waitTime = 1f;
        float elapsed = 0f;
        while (elapsed < waitTime)
        {
            elapsed += 0.016f;
            yield return null;
        }
        
        if (!isMoving)
        {
            MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Idle);
        }
    }

    /// <summary>
    /// 鼠标点击交互
    /// </summary>
    public void OnMouseClick(Vector2 mousePosition)
    {
        if (!mouseInteraction) return;
        if (isMoving) return;

        // 播放攻击动画
        MascotAnimationHelper.SetAnimation(gameObject, MascotLogic.AnimationType.Attack);
        StartCoroutine(ReturnToIdleAfterAction());
    }

    /// <summary>
    /// 鼠标悬停交互
    /// </summary>
    public void OnMouseHover()
    {
        if (!mouseInteraction) return;
        if (isMoving) return;

        // 可以在这里添加悬停效果，比如播放jump动画
        // MascotLogic.SetAnimation(gameObject, MascotLogic.AnimationType.Jump);
    }

    /// <summary>
    /// 手动播放动画（带过渡）
    /// </summary>
    public void PlayAnimation(MascotLogic.AnimationType animType, float transitionDuration = 0.2f)
    {
        MascotAnimationHelper.SetAnimation(gameObject, animType, transitionDuration);
    }
}

