using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 桌面宠物模块 - 在桌面上显示可拖拽的宠物窗口
/// </summary>
public class DesktopMascotModule : ToolModule
{
    public override string Name => "🐾 桌面宠物";
    public override string Category => "Tools";
    public override int Order => 999; // 放在最后
    public override string IconName => "d_Avatar";
    public override Color HeaderColor => new Color(1f, 0.7f, 0.9f);
    public override Color BackgroundColor => new Color(1f, 0.95f, 0.98f);

    public override void OnGUI(ToolContext context)
    {
        EditorGUILayout.HelpBox(
            "点击按钮打开桌面宠物窗口。宠物会显示在桌面上，可以拖拽移动，点击互动。\n" +
            "右键点击宠物窗口可以关闭。",
            MessageType.Info);
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("🐾 打开桌面宠物", GUILayout.Height(35)))
        {
            DesktopMascotWindow.Open();
        }
    }
}

/// <summary>
/// 桌面宠物窗口 - 可拖拽的浮动窗口，显示宠物动画
/// </summary>
public class DesktopMascotWindow : EditorWindow
{
    private static DesktopMascotWindow instance;

    // --- 资源变量 ---
    private static Texture2D textureIdle;
    private static Texture2D[] talkFrames;

    // --- 配置参数 ---
    private Vector2 petSize = new Vector2(128, 128);
    private Vector2 windowTotalSize = new Vector2(128, 160);
    private float animFPS = 12f;

    // --- 状态变量 ---
    private double lastFrameTime;
    private float animTimer;
    private Vector2 clickOffset;

    // --- 交互状态 ---
    private float talkDurationTimer = 0f; // 说话剩余时间倒计时
    private double talkStartTime = 0f;    // 记录开始说话的具体时刻
    private string currentMessage = "";
    private bool isDragging = false;

    public static void Open()
    {
        if (instance != null) instance.Close();
        instance = CreateInstance<DesktopMascotWindow>();
        instance.ShowPopup();

        Resolution res = Screen.currentResolution;
        instance.position = new Rect(res.width / 2, res.height / 2, 128, 160);
    }

    private void OnEnable()
    {
        LoadResources();
        EditorApplication.update += UpdateAnimation;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateAnimation;
    }

    private void LoadResources()
    {
        // 加载待机图
        if (textureIdle == null) textureIdle = Resources.Load<Texture2D>("Mascot_Idle");

        // 加载动画序列帧文件夹
        if (talkFrames == null || talkFrames.Length == 0)
        {
            // 加载指定文件夹下的所有 Texture2D
            var loadedObjs = Resources.LoadAll<Texture2D>("Mascot_Talk_Anim");

            // 关键：按文件名排序，确保动画顺序正确 (需要 using System.Linq)
            talkFrames = loadedObjs.OrderBy(t => t.name).ToArray();

            if (talkFrames.Length == 0)
            {
                Debug.LogWarning("未在 Resources/Mascot_Talk_Anim 文件夹中找到动画序列帧！将使用待机图代替。");
            }
        }
    }

    private void UpdateAnimation()
    {
        // 倒计时逻辑
        if (talkDurationTimer > 0)
        {
            talkDurationTimer -= (float)(EditorApplication.timeSinceStartup - lastFrameTime);
        }

        // 帧率控制，强制刷新界面
        if (EditorApplication.timeSinceStartup - lastFrameTime > 0.033f)
        {
            lastFrameTime = EditorApplication.timeSinceStartup;
            // 这里的 animTimer 仅用于身体的上下浮动，与说话动画无关
            animTimer += 0.1f;
            Repaint();
        }
    }

    private void OnGUI()
    {
        if (textureIdle == null) LoadResources();

        GUI.backgroundColor = Color.clear;

        // 1. 绘制文字气泡
        if (talkDurationTimer > 0)
        {
            DrawSpeechBubble();
        }

        // 2. 绘制角色 (核心修改逻辑)
        Texture2D currentTex = textureIdle; // 默认显示待机图

        // 如果正在说话状态，并且动画帧数组有效
        if (talkDurationTimer > 0 && talkFrames != null && talkFrames.Length > 0)
        {
            // 计算当前应该播放哪一帧
            // 计算从开始说话到现在经过的时间
            double timeElapsed = EditorApplication.timeSinceStartup - talkStartTime;
            // 经过时间 * FPS = 总共播放了多少帧
            int totalFramesPlayed = (int)(timeElapsed * animFPS);
            // 取余数，实现循环播放 (Loop)
            int currentFrameIndex = totalFramesPlayed % talkFrames.Length;

            currentTex = talkFrames[currentFrameIndex];
        }

        // 简单的浮动动画 (呼吸效果)
        float yOffset = Mathf.Sin(animTimer) * 5f;

        Rect contentRect = new Rect(0, 30 + yOffset, petSize.x, petSize.y);

        if (currentTex != null)
        {
            GUI.DrawTexture(contentRect, currentTex, ScaleMode.ScaleToFit);
        }

        // 3. 处理交互
        HandleEvents();
    }

    private void DrawSpeechBubble()
    {
        GUIStyle bubbleStyle = new GUIStyle(GUI.skin.box);
        bubbleStyle.alignment = TextAnchor.MiddleCenter;
        bubbleStyle.fontSize = 12;
        bubbleStyle.normal.textColor = Color.white;

        Texture2D bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0, 0, 0, 0.8f));
        bg.Apply();
        bubbleStyle.normal.background = bg;

        Rect bubbleRect = new Rect(10, 0, windowTotalSize.x - 20, 30);
        GUI.Box(bubbleRect, currentMessage, bubbleStyle);
    }

    private void HandleEvents()
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            clickOffset = e.mousePosition;
            isDragging = true;
            TriggerTalk();
            e.Use();
        }

        if (e.type == EventType.MouseDrag && e.button == 0 && isDragging)
        {
            Vector2 mouseScreenPos = GUIUtility.GUIToScreenPoint(e.mousePosition);
            position = new Rect(mouseScreenPos.x - clickOffset.x, mouseScreenPos.y - clickOffset.y, windowTotalSize.x, windowTotalSize.y);
            Repaint();
            e.Use();
        }

        if (e.type == EventType.MouseUp)
        {
            isDragging = false;
        }

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("关闭"), false, () => this.Close());
            menu.ShowAsContext();
            e.Use();
        }
    }

    private void TriggerTalk()
    {
        talkDurationTimer = 3.0f; // 说话持续 3 秒 (可以根据动画长度调整)
        // 记录开始时间
        talkStartTime = EditorApplication.timeSinceStartup;

        string[] pool = new string[] { "oiiai！", "喵！", "别戳我啦！", "动画播放中...", "Unity 启动！", "摸鱼中...", "我是一个Bug", "别点了，今晚给我加班~", "恭喜发财~", "好饿啊，求投喂~" };
        currentMessage = pool[Random.Range(0, pool.Length)];
        Repaint();
    }
}

