using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Rendering;

/// <summary>
/// Unity 开发者终极生产力工具箱 - v1.7 - RepinSKY
/// 包含：智能批量材质生成、物理对齐、批量重命名、资产替换、布局助手、系统清理、
///       快速对齐与等距分布、批量静态设置、查找重复物体、烘焙精度双档切换、快速创建助手
///       (增强版烘焙控制：预设配置、时间预估、进度显示、自动烘焙)
///       
/// v1.7 新增功能：
/// - ✅ 模块折叠系统（默认折叠，点击展开）
/// - ✅ 拖拽排序功能（支持自定义模块顺序）
/// - ✅ 拖拽后标题序号自动更新
/// - ✅ 安全的图标加载系统（避免警告）
/// - ✅ 界面优化（紧凑布局，自定义间距）
/// </summary>
public class UnityProToolbox : EditorWindow
{
    private Vector2 scrollPos;

    // --- [PBR 匹配关键词定义] - 支持大小写不敏感识别 ---
    // Albedo/Diffuse 关键词（支持多种命名格式，按长度从长到短排序，优先匹配长关键词）
    private readonly string[] albedoKeys = {
        // 复合关键词（优先匹配）
        "_albedotransparency", "_albedo_transparency", "albedotransparency", "albedo_transparency",
        "_basecolortransparency", "_base_color_transparency", "basecolortransparency", "base_color_transparency",
        "_basemap", "_base_map", "_basecolor", "_base_color", "_albedo", "_diffuse", "_d", "_c", "_color", "_maintex", "_main_tex",
        // 支持无下划线前缀的格式
        "basemap", "base_map", "basecolor", "base_color", "albedo", "diffuse", "color", "maintex", "main_tex"
    };
    // Normal/Bump 关键词
    private readonly string[] normalKeys = {
        "_normal", "_normalmap", "_normal_map", "_n", "_norm", "_bump", "_bumpmap", "_bump_map",
        "normal", "normalmap", "normal_map", "norm", "bump", "bumpmap", "bump_map"
    };
    // Mask/Metallic/Roughness 关键词（按长度从长到短排序）
    private readonly string[] maskKeys = {
        // 复合关键词（优先匹配）
        "_metallicsmoothness", "_metallic_smoothness", "metallicsmoothness", "metallic_smoothness",
        "_metallicroughness", "_metallic_roughness", "metallicroughness", "metallic_roughness",
        "_roughnessmetallic", "_roughness_metallic", "roughnessmetallic", "roughness_metallic",
        "_maskmap", "_mask_map", "_mask", "_ms", "_metallic", "_m", "_roughness", "_r", "_rough", "_specular", "_s", "_ao", "_metallicglossmap", "_metallic_gloss_map",
        "maskmap", "mask_map", "mask", "metallic", "roughness", "rough", "specular", "ao", "metallicglossmap", "metallic_gloss_map"
    };
    // Height/Displacement 关键词
    private readonly string[] heightKeys = {
        "_height", "_heightmap", "_height_map", "_h", "_disp", "_displacement", "_parallaxmap", "_parallax_map",
        "height", "heightmap", "height_map", "disp", "displacement", "parallaxmap", "parallax_map"
    };

    // --- [2. 物理对齐变量] ---
    private int groundLayerMask = -1;

    // --- [3. 批量重命名变量] ---
    private string renamePrefix = "";
    private string renameBase = "Object";
    private string renameSuffix = "";
    private int renameStartIndex = 0;
    private int renameDigits = 2;
    private bool renameReplaceAll = true;

    // --- [4. 资产替换变量] ---
    private GameObject replacementPrefab;

    // --- [5. 布局助手变量] ---
    private Vector3 duplicateOffset = new Vector3(2, 0, 0);

    // --- [6. 随机化变量] ---
    private float minScale = 0.8f, maxScale = 1.2f;
    private bool randYRotation = true;

    // --- [8. 对齐与等距分布变量] ---
    private int alignAxis = 0; // 0=X, 1=Y, 2=Z
    private bool alignMode = false; // false=对齐, true=等距分布

    // --- [9. 批量静态设置变量] ---
    private bool batchContributeGI = true;
    private bool batchReflectionProbe = true;
    private bool batchOccluderStatic = false;
    private bool batchOccludeeStatic = false;
    private bool batchBatchingStatic = false;
    private bool batchNavigationStatic = false;
    private bool batchOffMeshLinkGeneration = false;
    private bool batchReflectionProbeStatic = true;

    // --- [11. 烘焙精度双档切换变量 - 优化版] ---
    private bool isPreviewMode = true;
    private bool showAdvancedSettings = false;
    private bool autoEstimateTime = true;
    private bool saveSettings = true;
    private bool startBakeAfterSwitch = false;
    private bool showBakeProgress = false;
    private float bakeProgress = 0f;
    private string bakeStatus = "";
    private string[] presetNames = { "极快预览", "中等预览", "高质预览", "标准生产", "高质生产", "影视级烘焙" };
    private int selectedPreset = 0;

    // 参数存储（内存中转）
    private int curDirectSamples = 16;
    private int curIndirectSamples = 64;
    private int curEnvSamples = 64;
    private int curBounces = 2;



    // --- [12. 快速创建模块变量] ---
    private bool createAtSelection = true;

    // --- [模块折叠和排序系统] ---
    private class ModuleInfo
    {
        public int id;
        public bool isExpanded;
        public string baseTitle;
        public string iconName;
        public Color headerColor;
        public Color bgColor;
        public System.Action drawContent;

        public ModuleInfo(int id, string baseTitle, string iconName, Color headerColor, Color bgColor, System.Action drawContent)
        {
            this.id = id;
            this.baseTitle = baseTitle;
            this.iconName = iconName;
            this.headerColor = headerColor;
            this.bgColor = bgColor;
            this.drawContent = drawContent;
            this.isExpanded = false; // 默认折叠
        }
    }

    private List<ModuleInfo> modules = new List<ModuleInfo>();
    private int draggedModuleIndex = -1;
    private int dragTargetIndex = -1;
    private Vector2 dragStartPos;
    private int dragControlID = -1;
    private const string MODULE_ORDER_KEY = "UnityProToolbox_ModuleOrder";
    private const string MODULE_EXPANDED_KEY = "UnityProToolbox_ModuleExpanded_";

    [MenuItem("Tools/🚀Unity省力小工具箱")]
    public static void ShowWindow()
    {
        // 使用新框架窗口（如果存在）
        ToolboxWindow.ShowWindow();
    }

    // [MenuItem("Tools/🚀Unity省力小工具箱 (旧版)", false, 1)]
    // public static void ShowLegacyWindow() => GetWindow<UnityProToolbox>("Pro Toolbox (Legacy)");

    private void OnEnable()
    {
        // 加载保存的烘焙设置
        LoadBakeSettings();
        // 初始化模块系统
        InitializeModules();
    }

    private void OnGUI()
    {
        // 美化标题栏
        DrawHeader();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 拖拽时每帧重置目标索引，让模块基于鼠标悬停重新计算
        //（包括 MouseUp 帧：如果松手不在任何标题栏上，就不会发生移动）
        if (draggedModuleIndex >= 0)
            dragTargetIndex = -1;

        // 绘制所有模块（按顺序）
        for (int i = 0; i < modules.Count; i++)
        {
            DrawDraggableModule(i);
        }

        // 处理全局拖拽事件（放在模块绘制之后，确保 MouseUp 时已计算出 dragTargetIndex）
        HandleGlobalDragEvents();

        EditorGUILayout.Space(20);
        EditorGUILayout.EndScrollView();
    }

    private void HandleGlobalDragEvents()
    {
        Event evt = Event.current;

        // 处理拖拽结束事件
        if (draggedModuleIndex >= 0)
        {
            // 拖拽中：吞掉 MouseDrag，避免其它控件抢事件，同时持续刷新
            if (evt.type == EventType.MouseDrag)
            {
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseUp)
            {
                // 执行模块移动
                if (dragTargetIndex >= 0 && draggedModuleIndex != dragTargetIndex && dragTargetIndex < modules.Count)
                {
                    var draggedModule = modules[draggedModuleIndex];
                    modules.RemoveAt(draggedModuleIndex);

                    int insertIndex = dragTargetIndex;
                    if (draggedModuleIndex < dragTargetIndex)
                        insertIndex--;

                    if (insertIndex >= 0 && insertIndex <= modules.Count)
                    {
                        modules.Insert(insertIndex, draggedModule);

                        // 保存新的顺序
                        SaveModuleOrder();

                        GUI.changed = true;
                    }
                }

                // 重置拖拽状态
                draggedModuleIndex = -1;
                dragTargetIndex = -1;
                GUIUtility.hotControl = 0;
                dragControlID = -1;
                evt.Use();
                Repaint();
            }
            // 如果鼠标移出窗口，也重置状态
            else if (evt.type == EventType.MouseLeaveWindow)
            {
                draggedModuleIndex = -1;
                dragTargetIndex = -1;
                GUIUtility.hotControl = 0;
                dragControlID = -1;
                Repaint();
            }
        }
    }

    private void InitializeModules()
    {
        modules.Clear();

        // 加载保存的模块顺序
        string savedOrder = EditorPrefs.GetString(MODULE_ORDER_KEY, "");
        int[] moduleOrder = null;

        if (!string.IsNullOrEmpty(savedOrder))
        {
            string[] orderStrings = savedOrder.Split(',');
            moduleOrder = new int[orderStrings.Length];
            for (int i = 0; i < orderStrings.Length; i++)
            {
                if (int.TryParse(orderStrings[i], out int id))
                    moduleOrder[i] = id;
            }
        }

        // 定义所有模块
        List<ModuleInfo> tempModules = new List<ModuleInfo>
        {
            new ModuleInfo(1, "智能批量材质生成", "d_Material", new Color(0.2f, 0.6f, 1f), new Color(0.4f, 0.7f, 1f), DrawModule1),
            new ModuleInfo(2, "物理对齐", "d_MoveTool", new Color(0.2f, 0.8f, 0.3f), new Color(0.5f, 1f, 0.5f), DrawModule2),
            new ModuleInfo(3, "批量重命名", "d_TextAsset", new Color(0.7f, 0.7f, 0.7f), new Color(0.85f, 0.85f, 0.85f), DrawModule3),
            new ModuleInfo(4, "资产替换", "d_Prefab", new Color(1f, 0.7f, 0.2f), new Color(1f, 0.8f, 0.4f), DrawModule4),
            new ModuleInfo(5, "布局助手", "d_Grid", new Color(0.3f, 0.8f, 0.9f), new Color(0.7f, 1f, 1f), DrawModule5),
            new ModuleInfo(6, "随机变换", "d_RotateTool", new Color(1f, 0.4f, 0.8f), new Color(1f, 0.5f, 1f), DrawModule6),
            new ModuleInfo(7, "系统清理", "d_Settings", new Color(0.5f, 0.5f, 0.5f), Color.gray, DrawModule7),
            new ModuleInfo(8, "快速对齐与等距分布", "d_Grid", new Color(0.3f, 0.6f, 1f), new Color(0.6f, 0.8f, 1f), DrawModule8),
            new ModuleInfo(9, "批量静态设置", "d_Static", new Color(0.9f, 0.6f, 0.3f), new Color(0.9f, 0.7f, 0.5f), DrawModule9),
            new ModuleInfo(10, "查找重复物体", "d_Search", new Color(1f, 0.5f, 0.5f), new Color(1f, 0.6f, 0.6f), DrawModule10),
            new ModuleInfo(11, "烘焙精度双档切换", "d_Lighting", new Color(0.7f, 0.9f, 0.4f), new Color(0.8f, 0.9f, 0.5f), DrawModule11),
            new ModuleInfo(12, "快速创建助手", "d_ToolHandleLocal", new Color(0.9f, 0.5f, 0.2f), new Color(0.9f, 0.6f, 0.4f), DrawModule12)
        };

        // 如果存在保存的顺序，按顺序排列；否则使用默认顺序
        if (moduleOrder != null && moduleOrder.Length == tempModules.Count)
        {
            modules = new List<ModuleInfo>(tempModules.Count);
            foreach (int id in moduleOrder)
            {
                var module = tempModules.Find(m => m.id == id);
                if (module != null)
                {
                    // 加载折叠状态
                    module.isExpanded = EditorPrefs.GetBool(MODULE_EXPANDED_KEY + id, false);
                    modules.Add(module);
                }
            }
            // 添加任何缺失的模块
            foreach (var module in tempModules)
            {
                if (!modules.Exists(m => m.id == module.id))
                {
                    module.isExpanded = EditorPrefs.GetBool(MODULE_EXPANDED_KEY + module.id, false);
                    modules.Add(module);
                }
            }
        }
        else
        {
            modules = tempModules;
            // 加载折叠状态
            foreach (var module in modules)
            {
                module.isExpanded = EditorPrefs.GetBool(MODULE_EXPANDED_KEY + module.id, false);
            }
        }
    }

    private void DrawDraggableModule(int index)
    {
        ModuleInfo module = modules[index];
        Event evt = Event.current;

        // 绘制折叠标题栏
        EditorGUILayout.BeginVertical("box");

        // 绘制标题栏背景
        Rect titleRect = EditorGUILayout.GetControlRect(false, 26);
        Color titleBgColor = new Color(module.headerColor.r * 0.15f, module.headerColor.g * 0.15f, module.headerColor.b * 0.15f, 0.3f);

        // 如果正在拖拽此模块，高亮显示
        if (draggedModuleIndex == index)
        {
            titleBgColor = new Color(module.headerColor.r * 0.4f, module.headerColor.g * 0.4f, module.headerColor.b * 0.4f, 0.5f);
        }

        EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, titleRect.width, titleRect.height), titleBgColor);

        // 绘制左侧彩色条
        EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, 4, titleRect.height), module.headerColor);

        // 定义区域
        Rect foldoutRect = new Rect(titleRect.x + 6, titleRect.y + 2, 20, 22);
        Rect dragHandleRect = new Rect(titleRect.x + 28, titleRect.y + 4, 18, 18);

        // 先处理拖拽开始（在 Foldout 之前，避免被拦截）
        bool isInDragHandle = dragHandleRect.Contains(evt.mousePosition);
        bool isInFoldout = foldoutRect.Contains(evt.mousePosition);
        bool isInTitleRect = titleRect.Contains(evt.mousePosition);

        // 处理拖拽开始（优先处理，避免被折叠按钮拦截）
        if (evt.type == EventType.MouseDown && isInDragHandle && !isInFoldout && evt.button == 0 && draggedModuleIndex == -1)
        {
            draggedModuleIndex = index;
            dragStartPos = evt.mousePosition;
            dragTargetIndex = -1;
            dragControlID = GUIUtility.GetControlID("ModuleDrag".GetHashCode(), FocusType.Passive);
            GUIUtility.hotControl = dragControlID;
            evt.Use();
            Repaint();
        }

        // 拖拽中：基于鼠标悬停实时计算目标模块（不 Use 事件，让所有模块都有机会更新）
        if (draggedModuleIndex >= 0 && index != draggedModuleIndex && isInTitleRect && !isInFoldout)
        {
            dragTargetIndex = index;
        }

        // 绘制拖拽手柄图标（尝试多个图标名称）
        Texture2D dragHandleIcon = GetIconSafely("d_Grid", "Grid", "d_MoveTool", "MoveTool", "d_Grip");

        // 绘制拖拽手柄
        if (dragHandleIcon != null)
        {
            // 使用图标
            if (isInDragHandle || draggedModuleIndex == index)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                GUI.DrawTexture(dragHandleRect, dragHandleIcon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                GUI.DrawTexture(dragHandleRect, dragHandleIcon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
        }
        else
        {
            // 如果没有图标，绘制一个简单的拖拽手柄（三条横线）
            Color handleColor = (isInDragHandle || draggedModuleIndex == index) ?
                                new Color(0.7f, 0.7f, 0.7f, 0.9f) :
                                new Color(0.5f, 0.5f, 0.5f, 0.5f);

            float lineWidth = 12f;
            float lineHeight = 1.5f;
            float spacing = 2.5f;
            float startX = dragHandleRect.x + (dragHandleRect.width - lineWidth) * 0.5f;
            float startY = dragHandleRect.y + (dragHandleRect.height - (lineHeight * 3 + spacing * 2)) * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                Rect lineRect = new Rect(startX, startY + i * (lineHeight + spacing), lineWidth, lineHeight);
                EditorGUI.DrawRect(lineRect, handleColor);
            }
        }

        // 绘制折叠按钮（在拖拽事件处理之后，避免拦截拖拽事件）
        // 如果正在拖拽，禁用折叠按钮的交互
        EditorGUI.BeginDisabledGroup(draggedModuleIndex >= 0);
        bool newExpanded = EditorGUI.Foldout(foldoutRect, module.isExpanded, "", true);
        EditorGUI.EndDisabledGroup();

        if (newExpanded != module.isExpanded && draggedModuleIndex == -1)
        {
            module.isExpanded = newExpanded;
            EditorPrefs.SetBool(MODULE_EXPANDED_KEY + module.id, module.isExpanded);
            GUI.changed = true;
        }

        // 绘制标题和图标
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = module.headerColor },
            padding = new RectOffset(8, 0, 3, 0)
        };

        string displayTitle = $"{index + 1}. {module.baseTitle}";
        GUIContent titleContent = new GUIContent(" " + displayTitle);
        // 安全地加载图标
        Texture2D icon = GetIconSafely(module.iconName);
        if (icon != null)
        {
            titleContent.image = icon;
        }

        // 标题标签位置：折叠按钮(6+20=26) + 拖拽手柄(28+18=46) + 间距(4) = 50
        Rect labelRect = new Rect(titleRect.x + 50, titleRect.y + 2, titleRect.width - 52, 22);
        GUI.Label(labelRect, titleContent, titleStyle);

        // 如果正在拖拽其他模块到此位置，显示插入指示线
        if (draggedModuleIndex >= 0 && draggedModuleIndex != index && dragTargetIndex == index)
        {
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y - 2, titleRect.width, 2), new Color(0.2f, 0.6f, 1f, 0.8f));
        }

        EditorGUILayout.Space(-3);

        // 如果展开，绘制内容
        if (module.isExpanded)
        {
            EditorGUILayout.BeginVertical("box");
            module.drawContent?.Invoke();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(-3);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(-3);
    }

    private void SaveModuleOrder()
    {
        string order = string.Join(",", modules.Select(m => m.id.ToString()));
        EditorPrefs.SetString(MODULE_ORDER_KEY, order);
    }

    // ================= 各模块的绘制方法 =================

    private void DrawModule1()
    {
        EditorGUILayout.HelpBox(
            "操作：在 Project 窗口选中【贴图文件夹】或【多张贴图】，点击下方按钮。\n" +
            "系统会自动根据文件名关键词匹配 Albedo/Normal/Mask/Height 并生成材质。\n\n" +
            "📋 标准命名规范（支持大小写不敏感）：\n" +
            "• Albedo: _BaseMap, _Albedo, _Diffuse, _Color, _BaseColor, _MainTex 等\n" +
            "• Normal: _Normal, _NormalMap, _Bump, _BumpMap 等\n" +
            "• Mask: _MaskMap, _Metallic, _Roughness, _AO, _MetallicGlossMap 等\n" +
            "• Height: _Height, _HeightMap, _Displacement, _ParallaxMap 等\n\n" +
            "💡 示例：Stone_Albedo.png + Stone_Normal.png → Stone_Mat.mat\n" +
            "   支持格式：前缀_类型、前缀类型、类型（无前缀）等",
            MessageType.Info);
        DrawIconButton("✨ 一键识别并生成材质", "d_Material", new Color(0.2f, 0.6f, 1f), 40, CreateMaterialsFromSelection);
    }

    private void DrawModule2()
    {
        groundLayerMask = LayerMaskField("地面层级", groundLayerMask);
        DrawIconButton("⬇️ 一键对齐地面 (Ctrl+G)", "d_MoveTool", new Color(0.2f, 0.8f, 0.3f), 30, SnapToGroundPro);
    }

    private void DrawModule3()
    {
        renameReplaceAll = EditorGUILayout.Toggle("完全替换名", renameReplaceAll);
        if (renameReplaceAll) renameBase = EditorGUILayout.TextField("基础名", renameBase);
        EditorGUILayout.BeginHorizontal();
        renamePrefix = EditorGUILayout.TextField("前缀", renamePrefix);
        renameSuffix = EditorGUILayout.TextField("后缀", renameSuffix);
        EditorGUILayout.EndHorizontal();
        renameStartIndex = EditorGUILayout.IntField("起始编号", renameStartIndex);
        renameDigits = EditorGUILayout.IntSlider("编号位数", renameDigits, 1, 5);
        DrawIconButton("📝 执行批量重命名", "d_TextAsset", new Color(0.6f, 0.6f, 0.6f), 25, BatchRenamePro);
    }

    private void DrawModule4()
    {
        replacementPrefab = (GameObject)EditorGUILayout.ObjectField("目标预制体", replacementPrefab, typeof(GameObject), false);
        DrawIconButton("🔄 一键替换选中项", "d_Prefab", new Color(1f, 0.7f, 0.2f), 25, ReplaceWithPrefab);
    }

    private void DrawModule5()
    {
        duplicateOffset = EditorGUILayout.Vector3Field("阵列偏移量", duplicateOffset);
        DrawIconButton("📋 偏移复制并移动", "d_TreeEditor.Duplicate", new Color(0.3f, 0.8f, 0.9f), 25, DuplicateWithOffset);
        EditorGUILayout.Space(2);
        DrawIconButton("📁 快速打组", "d_Folder", new Color(0.3f, 0.8f, 0.9f), 25, QuickGroup);
    }

    private void DrawModule6()
    {
        randYRotation = EditorGUILayout.Toggle("随机 Y 轴旋转", randYRotation);
        EditorGUILayout.BeginHorizontal();
        minScale = EditorGUILayout.FloatField("Min Scale", minScale);
        maxScale = EditorGUILayout.FloatField("Max Scale", maxScale);
        EditorGUILayout.EndHorizontal();
        DrawIconButton("🎲 应用随机效果", "d_RotateTool", new Color(1f, 0.4f, 0.8f), 25, ApplyRandomization);
    }

    private void DrawModule7()
    {
        DrawIconButton("⚠️ 查找场景 Missing Scripts", "d_console.warnicon", new Color(0.9f, 0.7f, 0.2f), 25, FindMissingScripts);
        DrawIconButton("🗑️ 清空所有本地缓存", "d_Refresh", new Color(0.7f, 0.7f, 0.7f), 25, ClearCache);
        DrawIconButton("🏷️ 一键选中同 Tag 物体", "d_FilterByLabel", new Color(0.6f, 0.8f, 1f), 25, SelectByTag);
    }

    private void DrawModule8()
    {
        EditorGUILayout.HelpBox("选中多个物体，按轴方向对齐或等距分布。", MessageType.Info);
        alignAxis = EditorGUILayout.Popup("对齐轴", alignAxis, new string[] { "X 轴", "Y 轴", "Z 轴" });
        alignMode = EditorGUILayout.Toggle("等距分布模式", alignMode);
        DrawIconButton(alignMode ? "📏 执行等距分布" : "📐 执行对齐", "d_Grid", new Color(0.3f, 0.6f, 1f), 30, AlignAndDistribute);
    }

    private void DrawModule9()
    {
        EditorGUILayout.HelpBox("批量设置选中物体的静态标志位，用于场景优化和光照烘焙。", MessageType.Info);
        batchContributeGI = EditorGUILayout.Toggle("Contribute GI", batchContributeGI);
        batchReflectionProbe = EditorGUILayout.Toggle("Reflection Probe Static", batchReflectionProbe);
        batchOccluderStatic = EditorGUILayout.Toggle("Occluder Static", batchOccluderStatic);
        batchOccludeeStatic = EditorGUILayout.Toggle("Occludee Static", batchOccludeeStatic);
        batchBatchingStatic = EditorGUILayout.Toggle("Batching Static", batchBatchingStatic);
        batchNavigationStatic = EditorGUILayout.Toggle("Navigation Static", batchNavigationStatic);
        batchOffMeshLinkGeneration = EditorGUILayout.Toggle("Off Mesh Link Generation", batchOffMeshLinkGeneration);
        EditorGUILayout.Space(5);
        DrawIconButton("⚙️ 应用静态设置到选中物体", "d_Static", new Color(0.9f, 0.6f, 0.3f), 30, BatchStaticToggle);
    }

    private void DrawModule10()
    {
        EditorGUILayout.HelpBox("扫描场景中所有层级（包括子物体）的位置、旋转、模型完全一致的重复物体并高亮显示。", MessageType.Info);
        DrawIconButton("🔍 扫描并高亮重复物体", "d_Search", new Color(1f, 0.5f, 0.5f), 30, FindDuplicateObjects);
    }

    private void DrawModule11()
    {
        EditorGUILayout.HelpBox("提示：现代 Unity 必须在 Lighting 窗口先创建 'Lighting Settings' 资产才能生效。", MessageType.Info);

        // 打开 Lighting 窗口按钮
        DrawIconButton("🔧 打开 Lighting 窗口", "d_Lighting", new Color(0.4f, 0.7f, 1f), 30, OpenLightingWindow);

        EditorGUILayout.BeginHorizontal();
        Color originalColor = GUI.color;
        GUI.color = isPreviewMode ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.5f, 0.5f);
        Texture2D lightingIcon = GetIconSafely("d_Lighting");
        GUIContent modeContent = new GUIContent(isPreviewMode ? "预览模式" : "生产模式", lightingIcon);
        if (GUILayout.Button(modeContent, GUILayout.Height(30)))
        {
            isPreviewMode = !isPreviewMode;
            ApplyPreset();
        }
        GUI.color = originalColor;

        selectedPreset = EditorGUILayout.Popup(selectedPreset, presetNames, GUILayout.Width(120), GUILayout.Height(30));
        DrawIconButton("应用预设", "d_Refresh", new Color(0.7f, 0.9f, 0.4f), 30, ApplyPreset);
        EditorGUILayout.EndHorizontal();

        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "参数微调 (当前模式)");
        if (showAdvancedSettings)
        {
            EditorGUILayout.BeginVertical("box");
            curDirectSamples = EditorGUILayout.IntSlider("Direct Samples", curDirectSamples, 1, 1024);
            curIndirectSamples = EditorGUILayout.IntSlider("Indirect Samples", curIndirectSamples, 1, 4096);
            curEnvSamples = EditorGUILayout.IntSlider("Env Samples", curEnvSamples, 1, 1024);
            curBounces = EditorGUILayout.IntSlider("Bounces", curBounces, 0, 4);
            EditorGUILayout.EndVertical();
        }

        if (autoEstimateTime)
        {
            EditorGUILayout.HelpBox($"⏱ 预计时长: {EstimateBakeTime()}", MessageType.None);
        }

        EditorGUILayout.BeginHorizontal();
        DrawIconButton("💾 写入设置到资产", "d_SaveAs", new Color(0.2f, 0.7f, 0.9f), 35, () =>
        {
            ApplySettingsToAsset();
            if (startBakeAfterSwitch) StartBake();
        });
        DrawIconButton("🔥 立即开始烘焙", "d_Lighting", new Color(0.7f, 0.9f, 0.4f), 35, StartBake);
        EditorGUILayout.EndHorizontal();

        startBakeAfterSwitch = EditorGUILayout.Toggle("写入后立即烘焙", startBakeAfterSwitch);

        if (showBakeProgress)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(r, bakeProgress, bakeStatus);
        }
    }

    private void DrawModule12()
    {
        createAtSelection = EditorGUILayout.Toggle("在选中位置创建", createAtSelection);

        EditorGUILayout.LabelField("📦 基础模型", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawIconButton("立方体", "d_Cube", new Color(0.4f, 0.7f, 1f), 25, () => CreatePrimitive(PrimitiveType.Cube));
        DrawIconButton("球体", "d_Sphere", new Color(0.4f, 0.7f, 1f), 25, () => CreatePrimitive(PrimitiveType.Sphere));
        DrawIconButton("平面", "d_Plane", new Color(0.4f, 0.7f, 1f), 25, () => CreatePrimitive(PrimitiveType.Plane));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        DrawIconButton("圆柱体", "d_Cylinder", new Color(0.4f, 0.7f, 1f), 25, () => CreatePrimitive(PrimitiveType.Cylinder));
        DrawIconButton("胶囊体", "d_Capsule", new Color(0.4f, 0.7f, 1f), 25, () => CreatePrimitive(PrimitiveType.Capsule));
        DrawIconButton("四边形", "d_Quad", new Color(0.4f, 0.7f, 1f), 25, () => CreatePrimitive(PrimitiveType.Quad));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("💡 灯光组件", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawIconButton("平行光", "d_DirectionalLight", new Color(1f, 0.9f, 0.3f), 25, () => CreateLight(LightType.Directional));
        DrawIconButton("点光源", "d_Light", new Color(1f, 0.9f, 0.3f), 25, () => CreateLight(LightType.Point));
        DrawIconButton("聚光灯", "d_Spotlight", new Color(1f, 0.9f, 0.3f), 25, () => CreateLight(LightType.Spot));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🔍 探针与环境", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawIconButton("反射探针", "d_ReflectionProbe", new Color(0.4f, 0.8f, 1f), 25, CreateReflectionProbe);
        DrawIconButton("光照探针组", "d_LightProbeGroup", new Color(0.4f, 0.8f, 1f), 25, CreateLightProbeGroup);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🌐 环境配置 (URP/HDRP)", EditorStyles.miniLabel);
        DrawIconButton("全局 Volume", "d_SceneViewFx", new Color(0.6f, 0.4f, 0.9f), 25, CreateVolume);
    }

    // ================= 核心功能逻辑 =================

    // ================= 核心逻辑：智能材质生成 =================

    private void CreateMaterialsFromSelection()
    {
        // 1. 获取所有选中的贴图
        HashSet<string> texturePaths = new HashSet<string>();
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
            {
                // 如果是文件夹，搜寻文件夹内所有贴图
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { path });
                foreach (var guid in guids) texturePaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            else if (obj is Texture2D)
            {
                texturePaths.Add(path);
            }
        }

        if (texturePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "未选中任何贴图或包含贴图的文件夹！", "确定");
            return;
        }

        // 2. 按前缀进行组队 (例如 Stone_Albedo 和 Stone_Normal 都会归入 "Stone" 组)
        Dictionary<string, List<string>> materialGroups = new Dictionary<string, List<string>>();
        foreach (string path in texturePaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string baseName = GetBaseName(fileName);
            if (!materialGroups.ContainsKey(baseName)) materialGroups[baseName] = new List<string>();
            materialGroups[baseName].Add(path);
        }

        // 3. 开始批量创建材质
        int count = 0;
        AssetDatabase.StartAssetEditing(); // 提升批量处理速度
        try
        {
            foreach (var group in materialGroups)
            {
                CreatePBRMaterialFromGroup(group.Key, group.Value);
                count++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"<color=green>成功生成 {count} 个材质球！</color>");
        EditorUtility.DisplayDialog("完成", $"已成功基于名称匹配生成了 {count} 个材质球。", "确定");
    }

    private string GetBaseName(string fileName)
    {
        // 统一转换为小写进行匹配（大小写不敏感）
        string lowerName = fileName.ToLower();
        string processedFileName = fileName;
        string numberSuffix = null;

        // 先提取数字后缀（如 ".1001"）
        int lastDotIndex = fileName.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            string beforeDot = fileName.Substring(0, lastDotIndex);
            string afterDot = fileName.Substring(lastDotIndex + 1);
            // 如果点号后是纯数字，保存这个数字后缀
            if (System.Text.RegularExpressions.Regex.IsMatch(afterDot, @"^\d+$"))
            {
                numberSuffix = afterDot;
                processedFileName = beforeDot;
                lowerName = processedFileName.ToLower();
            }
        }

        // 尝试从文件名中移除已知的贴图类型后缀关键词（大小写不敏感）
        // 按长度排序，优先匹配长关键词（如先匹配 "albedotransparency"，再匹配 "albedo"）
        string[] allKeys = albedoKeys.Concat(normalKeys).Concat(maskKeys).Concat(heightKeys)
            .OrderByDescending(k => k.Length)
            .ToArray();

        foreach (var key in allKeys)
        {
            string lowerKey = key.ToLower();

            // 支持多种匹配方式：
            // 1. 以关键词结尾（如 "Stone_Albedo"）
            // 2. 关键词前有下划线或连字符（如 "Stone_AlbedoTransparency"）
            // 3. 关键词后跟其他词（如 "AlbedoTransparency"）
            bool matched = false;
            int removeLength = 0;

            // 情况1：完全以关键词结尾
            if (lowerName.EndsWith(lowerKey))
            {
                matched = true;
                removeLength = key.Length;

                // 检查前面是否有分隔符
                int keyStartIndex = processedFileName.Length - key.Length;
                if (keyStartIndex > 0)
                {
                    char beforeKey = processedFileName[keyStartIndex - 1];
                    if (beforeKey == '_' || beforeKey == '-')
                    {
                        removeLength = key.Length + 1;
                    }
                }
            }
            // 情况2：关键词前有分隔符，且后面可能还有其他内容（复合词）
            // 使用 LastIndexOf 找到最后一个匹配位置（关键词通常在文件名末尾）
            else if (lowerName.Contains("_" + lowerKey) || lowerName.Contains("-" + lowerKey))
            {
                // 找到关键词的位置（从后往前找，找到最后一个匹配）
                int keyIndex = lowerName.LastIndexOf("_" + lowerKey);
                if (keyIndex == -1) keyIndex = lowerName.LastIndexOf("-" + lowerKey);

                if (keyIndex >= 0)
                {
                    // 检查是否在文件名末尾附近（允许后面有少量字符，如 "Transparency"）
                    int keyEndIndex = keyIndex + lowerKey.Length + 1;
                    // 如果关键词在末尾，或者后面只有少量字符（可能是复合词的一部分），则匹配
                    if (keyEndIndex >= lowerName.Length - 20) // 允许后面最多20个字符（如 "Transparency"）
                    {
                        matched = true;
                        removeLength = processedFileName.Length - keyIndex;
                    }
                }
            }
            // 情况3：关键词后跟其他词（如 "AlbedoTransparency"）
            else if (lowerName.Contains(lowerKey))
            {
                // 使用 LastIndexOf 找到最后一个匹配位置
                int keyIndex = lowerName.LastIndexOf(lowerKey);
                if (keyIndex >= 0)
                {
                    // 检查前面是否有分隔符
                    if (keyIndex > 0)
                    {
                        char beforeKey = lowerName[keyIndex - 1];
                        if (beforeKey == '_' || beforeKey == '-')
                        {
                            // 检查是否在末尾附近
                            int keyEndIndex = keyIndex + lowerKey.Length;
                            if (keyEndIndex >= lowerName.Length - 20) // 允许后面最多20个字符
                            {
                                matched = true;
                                removeLength = processedFileName.Length - keyIndex + 1; // +1 包括前面的分隔符
                            }
                        }
                    }
                    // 如果关键词在开头，也尝试匹配（如 "AlbedoTransparency"）
                    else if (keyIndex == 0 && lowerName.Length <= lowerKey.Length + 20)
                    {
                        matched = true;
                        removeLength = processedFileName.Length; // 整个文件名都是关键词
                        return numberSuffix ?? "Material"; // 返回默认名称
                    }
                }
            }

            if (matched)
            {
                // 确保不越界
                if (processedFileName.Length >= removeLength)
                {
                    string baseName = processedFileName.Substring(0, processedFileName.Length - removeLength);
                    // 如果移除后缀后为空或只有分隔符，使用数字后缀（如果有）
                    if (string.IsNullOrEmpty(baseName) || baseName == "_" || baseName == "-")
                    {
                        return numberSuffix ?? processedFileName;
                    }
                    // 移除尾部分隔符（下划线或连字符）
                    baseName = baseName.TrimEnd('_', '-');
                    // 移除前导分隔符
                    if (baseName.StartsWith("_") || baseName.StartsWith("-"))
                        baseName = baseName.Substring(1);
                    return string.IsNullOrEmpty(baseName) ? (numberSuffix ?? processedFileName) : baseName;
                }
            }
        }

        // 如果没有匹配到任何贴图类型后缀，返回处理后的文件名或数字后缀
        return numberSuffix ?? processedFileName;
    }

    private void CreatePBRMaterialFromGroup(string baseName, List<string> paths)
    {
        // 自动识别当前管线
        string shaderName = "Standard";
        bool isHDRP = false, isURP = false;
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            string pipe = GraphicsSettings.currentRenderPipeline.GetType().ToString();
            if (pipe.Contains("HDRenderPipeline")) { shaderName = "HDRP/Lit"; isHDRP = true; }
            else if (pipe.Contains("UniversalRenderPipeline")) { shaderName = "Universal Render Pipeline/Lit"; isURP = true; }
        }

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"<color=red>无法找到着色器: {shaderName}</color>");
            return;
        }

        Material mat = new Material(shader);
        string folder = Path.GetDirectoryName(paths[0]);
        string matPath = $"{folder}/{baseName}_Mat.mat";

        // 用于记录已分配的贴图类型，避免重复分配
        bool hasAlbedo = false, hasNormal = false, hasMask = false, hasHeight = false;

        foreach (string path in paths)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"<color=yellow>无法加载贴图: {path}</color>");
                continue;
            }

            // 统一转换为小写进行匹配（大小写不敏感）
            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();
            string fileNameOriginal = Path.GetFileNameWithoutExtension(path);
            bool textureAssigned = false;

            // 改进的匹配逻辑：检查文件名是否包含关键词（支持大小写不敏感和多种分隔符）
            // Albedo/BaseMap
            if (!hasAlbedo && !textureAssigned)
            {
                foreach (var key in albedoKeys)
                {
                    string lowerKey = key.ToLower();
                    // 支持多种匹配方式：包含关键词，或关键词前后有分隔符
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        // HDRP 使用 _BaseColorMap，URP 使用 _BaseMap，Standard 使用 _MainTex
                        string propName = isHDRP ? "_BaseColorMap" : (isURP ? "_BaseMap" : "_MainTex");

                        // 尝试设置贴图
                        try
                        {
                            if (mat.HasProperty(propName))
                            {
                                mat.SetTexture(propName, tex);
                                Debug.Log($"<color=green>✓ 分配 Albedo: {fileNameOriginal} → {propName}</color>");
                                hasAlbedo = true;
                                textureAssigned = true;
                                break;
                            }
                            else
                            {
                                // 尝试不带下划线的属性名
                                string altPropName = propName.TrimStart('_');
                                if (mat.HasProperty(altPropName))
                                {
                                    mat.SetTexture(altPropName, tex);
                                    Debug.Log($"<color=green>✓ 分配 Albedo: {fileNameOriginal} → {altPropName}</color>");
                                    hasAlbedo = true;
                                    textureAssigned = true;
                                    break;
                                }
                                else
                                {
                                    Debug.LogWarning($"<color=yellow>材质缺少属性: {propName} 或 {altPropName}</color>");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"<color=red>设置贴图失败: {propName} - {e.Message}</color>");
                        }
                    }
                }
            }

            // Normal
            if (!hasNormal && !textureAssigned)
            {
                foreach (var key in normalKeys)
                {
                    string lowerKey = key.ToLower();
                    // 支持多种匹配方式：包含关键词，或关键词前后有分隔符
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        // 设置法线贴图的 TextureImporter
                        SetNormalMapTexture(path);

                        string propName = isHDRP ? "_NormalMap" : (isURP ? "_BumpMap" : "_BumpMap");

                        try
                        {
                            if (mat.HasProperty(propName))
                            {
                                mat.SetTexture(propName, tex);
                                mat.EnableKeyword("_NORMALMAP");
                                Debug.Log($"<color=green>✓ 分配 Normal: {fileNameOriginal} → {propName}</color>");
                                hasNormal = true;
                                textureAssigned = true;
                                break;
                            }
                            else
                            {
                                string altPropName = propName.TrimStart('_');
                                if (mat.HasProperty(altPropName))
                                {
                                    mat.SetTexture(altPropName, tex);
                                    mat.EnableKeyword("_NORMALMAP");
                                    Debug.Log($"<color=green>✓ 分配 Normal: {fileNameOriginal} → {altPropName}</color>");
                                    hasNormal = true;
                                    textureAssigned = true;
                                    break;
                                }
                                else
                                {
                                    Debug.LogWarning($"<color=yellow>材质缺少属性: {propName} 或 {altPropName}</color>");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"<color=red>设置法线贴图失败: {propName} - {e.Message}</color>");
                        }
                    }
                }
            }

            // Mask
            if (!hasMask && !textureAssigned)
            {
                foreach (var key in maskKeys)
                {
                    string lowerKey = key.ToLower();
                    // 支持多种匹配方式：包含关键词，或关键词前后有分隔符
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        string propName;
                        if (isHDRP)
                        {
                            propName = "_MaskMap";
                        }
                        else if (isURP)
                        {
                            propName = "_MetallicGlossMap";
                        }
                        else
                        {
                            propName = "_MetallicGlossMap";
                        }

                        try
                        {
                            if (mat.HasProperty(propName))
                            {
                                mat.SetTexture(propName, tex);
                                if (isURP || !isHDRP)
                                {
                                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                                }
                                Debug.Log($"<color=green>✓ 分配 Mask: {fileNameOriginal} → {propName}</color>");
                                hasMask = true;
                                textureAssigned = true;
                                break;
                            }
                            else
                            {
                                string altPropName = propName.TrimStart('_');
                                if (mat.HasProperty(altPropName))
                                {
                                    mat.SetTexture(altPropName, tex);
                                    if (isURP || !isHDRP)
                                    {
                                        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                                    }
                                    Debug.Log($"<color=green>✓ 分配 Mask: {fileNameOriginal} → {altPropName}</color>");
                                    hasMask = true;
                                    textureAssigned = true;
                                    break;
                                }
                                else
                                {
                                    Debug.LogWarning($"<color=yellow>材质缺少属性: {propName} 或 {altPropName}</color>");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"<color=red>设置 Mask 贴图失败: {propName} - {e.Message}</color>");
                        }
                    }
                }
            }

            // Height
            if (!hasHeight && !textureAssigned)
            {
                foreach (var key in heightKeys)
                {
                    string lowerKey = key.ToLower();
                    // 支持多种匹配方式：包含关键词，或关键词前后有分隔符
                    if (fileName.Contains(lowerKey) ||
                        fileName.Contains("_" + lowerKey) ||
                        fileName.Contains("-" + lowerKey) ||
                        fileName.EndsWith(lowerKey) ||
                        fileName.EndsWith("_" + lowerKey) ||
                        fileName.EndsWith("-" + lowerKey))
                    {
                        string propName = isHDRP ? "_HeightMap" : (isURP ? "_ParallaxMap" : "_ParallaxMap");

                        try
                        {
                            if (mat.HasProperty(propName))
                            {
                                mat.SetTexture(propName, tex);
                                Debug.Log($"<color=green>✓ 分配 Height: {fileNameOriginal} → {propName}</color>");
                                hasHeight = true;
                                textureAssigned = true;
                                break;
                            }
                            else
                            {
                                string altPropName = propName.TrimStart('_');
                                if (mat.HasProperty(altPropName))
                                {
                                    mat.SetTexture(altPropName, tex);
                                    Debug.Log($"<color=green>✓ 分配 Height: {fileNameOriginal} → {altPropName}</color>");
                                    hasHeight = true;
                                    textureAssigned = true;
                                    break;
                                }
                                else
                                {
                                    Debug.LogWarning($"<color=yellow>材质缺少属性: {propName} 或 {altPropName}</color>");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"<color=red>设置 Height 贴图失败: {propName} - {e.Message}</color>");
                        }
                    }
                }
            }

            if (!textureAssigned)
            {
                Debug.LogWarning($"<color=yellow>未识别贴图类型: {fileNameOriginal}</color>");
            }
        }

        // 创建材质资产
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan>材质已创建: {matPath}</color>");
        Debug.Log($"<color=cyan>管线类型: {(isHDRP ? "HDRP" : (isURP ? "URP" : "Built-in"))}</color>");

        // 调试：列出材质的所有纹理属性
        Shader matShader = mat.shader;
        int propertyCount = matShader.GetPropertyCount();
        Debug.Log($"<color=cyan>材质属性总数: {propertyCount}</color>");
        for (int i = 0; i < propertyCount; i++)
        {
            if (matShader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
            {
                string propName = matShader.GetPropertyName(i);
                Debug.Log($"<color=cyan>  纹理属性: {propName}</color>");
            }
        }
    }

    private void SetNormalMapTexture(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    private void SnapToGroundPro()
    {
        Transform[] ts = Selection.transforms; Undo.RecordObjects(ts, "Snap To Ground");
        foreach (var t in ts)
        {
            float hgt = 2.0f; if (t.TryGetComponent<Renderer>(out var r)) hgt = r.bounds.size.y + 0.5f;
            if (Physics.Raycast(t.position + Vector3.up * hgt, Vector3.down, out RaycastHit h, 2000f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (h.transform == t || h.transform.IsChildOf(t))
                {
                    if (!Physics.Raycast(h.point + Vector3.down * 0.1f, Vector3.down, out h, 2000f, groundLayerMask, QueryTriggerInteraction.Ignore)) continue;
                }
                Vector3 p = h.point; if (t.TryGetComponent<Renderer>(out var ren)) p.y += (t.position.y - ren.bounds.min.y);
                t.position = p;
            }
        }
    }

    private void BatchRenamePro()
    {
        Object[] os = Selection.objects; Undo.RecordObjects(os, "Batch Rename");
        for (int i = 0; i < os.Length; i++)
        {
            string idx = (renameStartIndex + i).ToString("D" + renameDigits);
            string b = renameReplaceAll ? renameBase : os[i].name;
            string n = $"{renamePrefix}{b}_{idx}{renameSuffix}";
            if (AssetDatabase.Contains(os[i])) AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(os[i]), n);
            else os[i].name = n;
        }
        AssetDatabase.SaveAssets();
    }

    private void DuplicateWithOffset()
    {
        GameObject act = Selection.activeGameObject; if (act == null) return;
        GameObject n = Instantiate(act, act.transform.parent);
        n.name = act.name;
        Undo.RegisterCreatedObjectUndo(n, "Duplicate Offset");
        n.transform.position = act.transform.position + duplicateOffset;
        Selection.activeGameObject = n;
    }

    private void QuickGroup()
    {
        Transform[] ss = Selection.transforms; if (ss.Length == 0) return;
        GameObject p = new GameObject("Group_New");
        Undo.RegisterCreatedObjectUndo(p, "Quick Group");
        p.transform.position = ss[0].position;
        foreach (var t in ss) Undo.SetTransformParent(t, p.transform, "Group");
        Selection.activeGameObject = p;
    }

    private void ReplaceWithPrefab()
    {
        if (replacementPrefab == null) return;
        foreach (var g in Selection.gameObjects)
        {
            GameObject n = (GameObject)PrefabUtility.InstantiatePrefab(replacementPrefab);
            Undo.RegisterCreatedObjectUndo(n, "Replace");
            n.transform.SetPositionAndRotation(g.transform.position, g.transform.rotation);
            n.transform.localScale = g.transform.localScale;
            n.transform.parent = g.transform.parent;
            Undo.DestroyObjectImmediate(g);
        }
    }

    private void ApplyRandomization()
    {
        Undo.RecordObjects(Selection.transforms, "Randomize");
        foreach (var t in Selection.transforms)
        {
            if (randYRotation) t.Rotate(0, Random.Range(0, 360f), 0);
            t.localScale = Vector3.one * Random.Range(minScale, maxScale);
        }
    }

    private void FindMissingScripts()
    {
        int c = 0; foreach (var g in GameObject.FindObjectsOfType<GameObject>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g) > 0) { Debug.LogWarning($"Missing: {g.name}", g); c++; }
        EditorUtility.DisplayDialog("完成", $"发现 {c} 个缺失脚本物体。", "OK");
    }

    private void ClearCache() { if (EditorUtility.DisplayDialog("警告", "清空PlayerPrefs？", "是", "否")) PlayerPrefs.DeleteAll(); }

    private void SelectByTag() { try { Selection.objects = GameObject.FindGameObjectsWithTag(Selection.activeGameObject.tag); } catch { } }

    // ================= 模块 8：快速对齐与等距分布 =================

    private void AlignAndDistribute()
    {
        Transform[] transforms = Selection.transforms;
        if (transforms.Length < 2)
        {
            EditorUtility.DisplayDialog("提示", "请至少选中 2 个物体！", "确定");
            return;
        }

        Undo.RecordObjects(transforms, alignMode ? "Distribute Objects" : "Align Objects");

        if (alignMode)
        {
            // 等距分布模式
            DistributeObjects(transforms, alignAxis);
        }
        else
        {
            // 对齐模式
            AlignObjects(transforms, alignAxis);
        }

        Debug.Log($"<color=green>成功{(alignMode ? "等距分布" : "对齐")} {transforms.Length} 个物体！</color>");
    }

    private void AlignObjects(Transform[] transforms, int axis)
    {
        // 计算所有物体的平均位置作为对齐目标
        Vector3 sumPos = Vector3.zero;
        foreach (var t in transforms) sumPos += t.position;
        Vector3 targetPos = sumPos / transforms.Length;

        foreach (var t in transforms)
        {
            Vector3 pos = t.position;
            if (axis == 0) pos.x = targetPos.x;
            else if (axis == 1) pos.y = targetPos.y;
            else if (axis == 2) pos.z = targetPos.z;
            t.position = pos;
        }
    }

    private void DistributeObjects(Transform[] transforms, int axis)
    {
        // 按指定轴排序
        List<Transform> sorted = new List<Transform>(transforms);
        sorted.Sort((a, b) =>
        {
            float valA = axis == 0 ? a.position.x : (axis == 1 ? a.position.y : a.position.z);
            float valB = axis == 0 ? b.position.x : (axis == 1 ? b.position.y : b.position.z);
            return valA.CompareTo(valB);
        });

        // 计算起始和结束位置
        float startVal = axis == 0 ? sorted[0].position.x : (axis == 1 ? sorted[0].position.y : sorted[0].position.z);
        float endVal = axis == 0 ? sorted[sorted.Count - 1].position.x : (axis == 1 ? sorted[sorted.Count - 1].position.y : sorted[sorted.Count - 1].position.z);
        float totalDistance = endVal - startVal;

        // 等距分布
        for (int i = 0; i < sorted.Count; i++)
        {
            float ratio = sorted.Count > 1 ? (float)i / (sorted.Count - 1) : 0f;
            float newVal = startVal + totalDistance * ratio;

            Vector3 pos = sorted[i].position;
            if (axis == 0) pos.x = newVal;
            else if (axis == 1) pos.y = newVal;
            else if (axis == 2) pos.z = newVal;
            sorted[i].position = pos;
        }
    }

    // ================= 模块 9：批量静态设置 =================

    private void BatchStaticToggle()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请至少选中 1 个物体！", "确定");
            return;
        }

        Undo.RecordObjects(selected, "Batch Static Toggle");

        int count = 0;
        foreach (GameObject go in selected)
        {
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);

            // 设置各个标志位
            flags = SetStaticFlag(flags, StaticEditorFlags.ContributeGI, batchContributeGI);
            flags = SetStaticFlag(flags, StaticEditorFlags.ReflectionProbeStatic, batchReflectionProbe);
            flags = SetStaticFlag(flags, StaticEditorFlags.OccluderStatic, batchOccluderStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.OccludeeStatic, batchOccludeeStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.BatchingStatic, batchBatchingStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.NavigationStatic, batchNavigationStatic);
            flags = SetStaticFlag(flags, StaticEditorFlags.OffMeshLinkGeneration, batchOffMeshLinkGeneration);

            GameObjectUtility.SetStaticEditorFlags(go, flags);
            count++;
        }

        Debug.Log($"<color=green>成功设置 {count} 个物体的静态标志！</color>");
        EditorUtility.DisplayDialog("完成", $"已成功设置 {count} 个物体的静态标志。", "确定");
    }

    private StaticEditorFlags SetStaticFlag(StaticEditorFlags flags, StaticEditorFlags flag, bool enable)
    {
        if (enable)
            return flags | flag;
        else
            return flags & ~flag;
    }

    // ================= 模块 10：查找重复物体 =================

    private void FindDuplicateObjects()
    {
        // 获取场景中所有物体（包括子物体和隐藏物体）
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);
        List<GameObject> duplicates = new List<GameObject>();
        HashSet<GameObject> processed = new HashSet<GameObject>();

        const float positionThreshold = 0.001f;
        const float rotationThreshold = 0.1f;

        Debug.Log($"<color=cyan>开始扫描场景，共 {allObjects.Length} 个物体（包含所有层级）...</color>");

        for (int i = 0; i < allObjects.Length; i++)
        {
            if (processed.Contains(allObjects[i])) continue;

            GameObject obj1 = allObjects[i];

            // 移除对根物体的限制，现在检查所有层级的物体
            MeshFilter mf1 = obj1.GetComponent<MeshFilter>();
            if (mf1 == null || mf1.sharedMesh == null) continue;

            List<GameObject> group = new List<GameObject> { obj1 };

            for (int j = i + 1; j < allObjects.Length; j++)
            {
                GameObject obj2 = allObjects[j];
                if (processed.Contains(obj2)) continue; // 移除对子物体的过滤

                MeshFilter mf2 = obj2.GetComponent<MeshFilter>();
                if (mf2 == null || mf2.sharedMesh == null) continue;

                // 检查模型是否相同
                if (mf1.sharedMesh != mf2.sharedMesh) continue;

                // 检查位置是否相同（使用世界坐标）
                Vector3 posDiff = obj1.transform.position - obj2.transform.position;
                if (posDiff.magnitude > positionThreshold) continue;

                // 检查旋转是否相同（使用世界旋转）
                Quaternion rotDiff = obj1.transform.rotation * Quaternion.Inverse(obj2.transform.rotation);
                float angle = Mathf.Abs(Quaternion.Angle(Quaternion.identity, rotDiff));
                if (angle > rotationThreshold) continue;

                // 检查缩放是否相同（使用本地缩放）
                Vector3 scaleDiff = obj1.transform.localScale - obj2.transform.localScale;
                if (scaleDiff.magnitude > positionThreshold) continue;

                group.Add(obj2);
                processed.Add(obj2);
            }

            if (group.Count > 1)
            {
                duplicates.AddRange(group);
                processed.Add(obj1);
            }
        }

        if (duplicates.Count == 0)
        {
            EditorUtility.DisplayDialog("完成", "未发现重复物体。\n\n已扫描所有层级的物体（包括子物体）。", "确定");
            Debug.Log("<color=green>扫描完成：未发现重复物体</color>");
            return;
        }

        // 高亮显示重复物体
        Selection.objects = duplicates.ToArray();

        // 按层级分组显示重复物体
        var groupedDuplicates = duplicates.GroupBy(obj => GetHierarchyPath(obj.transform));
        int groupCount = 0;
        foreach (var group in groupedDuplicates)
        {
            groupCount++;
            Debug.LogWarning($"<color=yellow>重复组 #{groupCount} ({group.Count()} 个物体):</color>");
            foreach (var dup in group)
            {
                string path = GetHierarchyPath(dup.transform);
                Debug.LogWarning($"  • {path} | 位置: {dup.transform.position}", dup);
            }
        }

        EditorUtility.DisplayDialog("完成",
            $"发现 {duplicates.Count} 个重复物体（共 {groupCount} 组），已在 Hierarchy 中选中并高亮显示。\n\n" +
            "已扫描所有层级的物体（包括子物体）。",
            "确定");
    }

    // 辅助方法：获取物体在层级中的完整路径
    private string GetHierarchyPath(Transform transform)
    {
        List<string> path = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }
        return string.Join("/", path);
    }

    // ================= 模块 11 核心逻辑优化 =================




    private UnityEngine.Object GetLightingSettingsObject()
    {
        // 尝试多种方式获取 LightingSettings
        UnityEngine.Object lightmapSettingsObj = null;

        // 方法1: 反射访问内部方法
        System.Reflection.MethodInfo getLightmapSettingsMethod = typeof(LightmapEditorSettings).GetMethod(
            "GetLightmapSettings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );

        if (getLightmapSettingsMethod != null)
        {
            try
            {
                lightmapSettingsObj = getLightmapSettingsMethod.Invoke(null, null) as UnityEngine.Object;
            }
            catch { }
        }

        // 方法2: 尝试使用 Lightmapping.lightingSettings
        if (lightmapSettingsObj == null)
        {
            var lightingSettingsProperty = typeof(Lightmapping).GetProperty("lightingSettings",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (lightingSettingsProperty != null)
            {
                lightmapSettingsObj = lightingSettingsProperty.GetValue(null) as UnityEngine.Object;
            }
        }

        // 方法3: 从场景中查找 LightingDataAsset
        if (lightmapSettingsObj == null)
        {
            var lightingDataAssets = AssetDatabase.FindAssets("t:LightingDataAsset");
            if (lightingDataAssets.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(lightingDataAssets[0]);
                lightmapSettingsObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            }
        }

        return lightmapSettingsObj;
    }

    private void ApplyPreset()
    {
        // 根据选择的下拉菜单和当前的模式，智能分配数值
        if (isPreviewMode)
        {
            switch (selectedPreset)
            {
                case 0: curDirectSamples = 8; curIndirectSamples = 32; curEnvSamples = 32; curBounces = 1; break; // 极快
                case 1: curDirectSamples = 16; curIndirectSamples = 64; curEnvSamples = 64; curBounces = 2; break; // 中等
                default: curDirectSamples = 32; curIndirectSamples = 128; curEnvSamples = 128; curBounces = 2; break; // 高质
            }
        }
        else
        {
            switch (selectedPreset)
            {
                case 3: curDirectSamples = 32; curIndirectSamples = 512; curEnvSamples = 256; curBounces = 2; break; // 标准生产
                case 4: curDirectSamples = 64; curIndirectSamples = 1024; curEnvSamples = 512; curBounces = 3; break; // 高质生产
                case 5: curDirectSamples = 128; curIndirectSamples = 2048; curEnvSamples = 1024; curBounces = 4; break; // 影视级
                default: curDirectSamples = 32; curIndirectSamples = 256; curEnvSamples = 256; curBounces = 2; break;
            }
        }
    }

    private void ApplySettingsToAsset()
    {
        // 1. 获取当前场景关联的 LightingSettings 资产 (Unity 2020.3+)
        LightingSettings lightingSettings = Lightmapping.lightingSettings;

        if (lightingSettings == null)
        {
            EditorUtility.DisplayDialog("未找到设置资产", "当前场景未关联 'Lighting Settings' 资产！\n\n请在 Lighting 窗口点击 'New Lighting Settings' 按钮创建并保存。", "去创建");
            return;
        }

        // 2. 记录撤销并直接操作 API (不再使用复杂的反射或正则)
        Undo.RecordObject(lightingSettings, "Update Bake Settings");

        lightingSettings.directSampleCount = curDirectSamples;
        lightingSettings.indirectSampleCount = curIndirectSamples;
        lightingSettings.environmentSampleCount = curEnvSamples;
        lightingSettings.maxBounces = curBounces;

        // 3. 强制保存资产
        EditorUtility.SetDirty(lightingSettings);
        AssetDatabase.SaveAssets();

        // 4. 强制 UI 刷新
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        Debug.Log($"<color=green>✓ 已成功更新光照设置资产: {lightingSettings.name}</color>");
    }

    private string EstimateBakeTime()
    {
        long complexity = (long)curDirectSamples * curIndirectSamples * (curBounces + 1);
        if (complexity < 10000) return "极快 (< 2分钟)";
        if (complexity < 100000) return "中等 (5-15分钟)";
        if (complexity < 1000000) return "较慢 (30-60分钟)";
        return "漫长 (需数小时，建议挂机)";
    }

    private void StartBake()
    {
        if (Lightmapping.isRunning)
        {
            if (EditorUtility.DisplayDialog("烘焙中", "当前正在进行烘焙，是否要取消当前任务？", "取消当前烘焙", "继续等待"))
                Lightmapping.ForceStop();
            return;
        }

        ApplySettingsToAsset(); // 烘焙前强制同步一次
        if (Lightmapping.BakeAsync())
        {
            showBakeProgress = true;
            EditorApplication.update += UpdateBakeProgress;
        }
    }

    private void UpdateBakeProgress()
    {
        if (Lightmapping.isRunning)
        {
            bakeProgress = Lightmapping.buildProgress;
            bakeStatus = $"正在烘焙: {Mathf.RoundToInt(bakeProgress * 100)}%";
            if (bakeProgress < 0) bakeStatus = "正在准备...";
        }
        else
        {
            bakeProgress = 1f;
            bakeStatus = "烘焙完成!";
            EditorApplication.update -= UpdateBakeProgress;
            // 3秒后隐藏进度条
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(t => showBakeProgress = false);
        }
        Repaint();
    }

    private void LoadBakeSettings()
    {
        // 从EditorPrefs加载设置（如果需要的话）
        // 这里可以添加加载逻辑，比如恢复上次的参数设置
    }

    /// <summary>
    /// 打开 Unity Lighting 窗口
    /// </summary>
    private void OpenLightingWindow()
    {
        // 尝试多种方式打开 Lighting 窗口（兼容不同 Unity 版本）
        // Unity 2019+ 使用 "Window/Rendering/Lighting"
        // Unity 2018 及更早版本可能使用 "Window/Lighting"
        if (!EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting"))
        {
            // 如果失败，尝试旧版本的菜单路径
            EditorApplication.ExecuteMenuItem("Window/Lighting");
        }
    }





    // ================= 模块 12：快速创建助手 =================

    private Vector3 GetCreatePosition()
    {
        // 如果勾选了在选中位置创建，且有选中物体
        if (createAtSelection && Selection.activeTransform != null)
            return Selection.activeTransform.position;

        // 否则创建在场景视口中心
        if (SceneView.lastActiveSceneView != null)
            return SceneView.lastActiveSceneView.pivot;

        return Vector3.zero;
    }

    private void CreateLight(LightType type)
    {
        GameObject go = new GameObject("New_" + type.ToString() + "Light");
        Light light = go.AddComponent<Light>();
        light.type = type;
        go.transform.position = GetCreatePosition();

        // 针对不同灯光的默认强度优化
        if (type == LightType.Directional) go.transform.rotation = Quaternion.Euler(50, -30, 0);

        Undo.RegisterCreatedObjectUndo(go, "Create Light");
        Selection.activeGameObject = go;
    }

    private void CreateReflectionProbe()
    {
        GameObject go = new GameObject("New_ReflectionProbe");
        go.AddComponent<ReflectionProbe>();
        go.transform.position = GetCreatePosition();

        Undo.RegisterCreatedObjectUndo(go, "Create Reflection Probe");
        Selection.activeGameObject = go;
    }

    private void CreateLightProbeGroup()
    {
        GameObject go = new GameObject("New_LightProbeGroup");
        go.AddComponent<LightProbeGroup>();
        go.transform.position = GetCreatePosition();

        Undo.RegisterCreatedObjectUndo(go, "Create Light Probe Group");
        Selection.activeGameObject = go;
    }

    private void CreatePrimitive(PrimitiveType type)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = "New_" + type.ToString();
        go.transform.position = GetCreatePosition();

        Undo.RegisterCreatedObjectUndo(go, "Create Primitive");
        Selection.activeGameObject = go;
    }

    private void CreateVolume()
    {
        GameObject go = new GameObject("New_Global_Volume");
        go.transform.position = GetCreatePosition();

        // 尝试多种方式添加 Volume 组件（兼容 URP 和 HDRP）
        bool volumeAdded = false;

        // 方法1: 直接尝试添加 Volume 组件（如果类型存在）
        try
        {
            // 检查 Volume 类型是否存在
            System.Type volumeType = System.Type.GetType("UnityEngine.Rendering.Volume, UnityEngine.Rendering.Core");
            if (volumeType == null)
                volumeType = System.Type.GetType("UnityEngine.Rendering.Volume, UnityEngine.Rendering.Runtime");
            if (volumeType == null)
            {
                // 尝试从已加载的程序集中查找
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    volumeType = assembly.GetType("UnityEngine.Rendering.Volume");
                    if (volumeType != null) break;
                }
            }

            if (volumeType != null)
            {
                var volume = go.AddComponent(volumeType);
                // 通过反射设置 isGlobal 属性
                var isGlobalProp = volumeType.GetProperty("isGlobal");
                if (isGlobalProp != null)
                {
                    isGlobalProp.SetValue(volume, true);
                    volumeAdded = true;
                    Debug.Log("<color=green>✓ 已成功创建全局 Volume。</color>");
                }
                else
                {
                    Debug.LogWarning("<color=yellow>Volume 组件已添加，但无法设置 isGlobal 属性。</color>");
                    volumeAdded = true; // 至少组件已添加
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"<color=yellow>添加 Volume 组件时出错: {e.Message}</color>");
        }

        // 方法2: 如果方法1失败，尝试使用 Unity 内置菜单项的方式
        if (!volumeAdded)
        {
            try
            {
                // 尝试调用 Unity 内置的创建方法
                var menuItemType = System.Type.GetType("UnityEditor.Rendering.VolumeMenuItems, UnityEditor.Rendering.Core");
                if (menuItemType == null)
                {
                    // 从已加载的程序集中查找
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        menuItemType = assembly.GetType("UnityEditor.Rendering.VolumeMenuItems");
                        if (menuItemType != null) break;
                    }
                }

                if (menuItemType != null)
                {
                    var method = menuItemType.GetMethod("CreateGlobalVolume",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    if (method != null)
                    {
                        // 先删除之前创建的空物体
                        Object.DestroyImmediate(go);
                        // 使用 MenuCommand 创建
                        method.Invoke(null, new object[] { new UnityEditor.MenuCommand(null) });
                        volumeAdded = true;
                        Debug.Log("<color=green>✓ 已成功创建全局 Volume（通过 Unity 内置方法）。</color>");
                        return; // 已经创建了新的物体，不需要继续
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"<color=yellow>使用 Unity 内置方法失败: {e.Message}</color>");
            }
        }

        if (!volumeAdded)
        {
            Debug.LogWarning("<color=yellow>⚠ 无法创建 Volume 组件。\n" +
                "可能原因：\n" +
                "1. 项目未导入 URP 或 HDRP 包\n" +
                "2. Volume 类型在当前管线中不可用\n\n" +
                "建议：请手动在 GameObject 菜单中创建 Volume，或确保已导入正确的渲染管线包。</color>");
        }

        Undo.RegisterCreatedObjectUndo(go, "Create Volume");
        Selection.activeGameObject = go;
    }

    // ================= UI 辅助工具 =================

    private void DrawHeader()
    {
        // 绘制渐变背景标题栏
        Rect headerRect = EditorGUILayout.GetControlRect(false, 55);
        Color headerColor1 = new Color(0.15f, 0.35f, 0.75f);
        Color headerColor2 = new Color(0.25f, 0.55f, 0.95f);

        // 绘制背景
        EditorGUI.DrawRect(headerRect, headerColor1);

        // 绘制底部装饰线
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y + headerRect.height - 3, headerRect.width, 3), headerColor2);

        // 绘制标题和图标
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white },
            padding = new RectOffset(15, 0, 0, 0)
        };

        GUIContent headerContent = new GUIContent("🚀 Unity Pro Toolbox v1.7 | 智能批量材质 & 生产力工具 | RepinSKY");

        // 安全地添加图标
        Texture2D headerIcon = GetIconSafely("d_Settings");
        if (headerIcon != null)
        {
            headerContent.image = headerIcon;
        }

        Rect labelRect = new Rect(headerRect.x + 10, headerRect.y, headerRect.width - 20, headerRect.height);
        GUI.Label(labelRect, headerContent, headerStyle);

        EditorGUILayout.Space(8);
    }

    private void BeginSection(string title, string iconName, Color headerColor, Color bgColor)
    {
        EditorGUILayout.BeginVertical("box");

        // 绘制标题栏背景
        Rect titleRect = EditorGUILayout.GetControlRect(false, 26);
        Color titleBgColor = new Color(headerColor.r * 0.15f, headerColor.g * 0.15f, headerColor.b * 0.15f, 0.3f);
        EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, titleRect.width, titleRect.height), titleBgColor);

        // 绘制左侧彩色条
        EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, 4, titleRect.height), headerColor);

        // 绘制标题和图标
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = headerColor },
            padding = new RectOffset(8, 0, 3, 0)
        };

        GUIContent titleContent = new GUIContent(" " + title);

        // 安全地加载图标
        Texture2D icon = GetIconSafely(iconName);
        if (icon != null)
        {
            titleContent.image = icon;
        }

        Rect labelRect = new Rect(titleRect.x + 8, titleRect.y + 2, titleRect.width - 16, 22);
        GUI.Label(labelRect, titleContent, titleStyle);

        EditorGUILayout.Space(5);
    }

    private void EndSection()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);
    }

    private void DrawIconButton(string text, string iconName, Color buttonColor, float height, System.Action action)
    {
        Color originalBgColor = GUI.backgroundColor;
        Color originalContentColor = GUI.contentColor;

        // 设置按钮背景色（稍微淡化以保持可读性）
        GUI.backgroundColor = buttonColor * 0.8f;
        GUI.contentColor = Color.white;

        GUIContent buttonContent = new GUIContent(text);

        // 安全地加载图标
        Texture2D icon = GetIconSafely(iconName);
        if (icon != null)
        {
            buttonContent.image = icon;
        }

        // 创建按钮样式
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(10, 10, 5, 5)
        };

        // 绘制按钮
        if (GUILayout.Button(buttonContent, buttonStyle, GUILayout.Height(height)))
        {
            action?.Invoke();
        }

        // 恢复原始颜色
        GUI.backgroundColor = originalBgColor;
        GUI.contentColor = originalContentColor;
    }

    // 安全地加载图标，避免警告
    private Texture2D GetIconSafely(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        // 使用反射临时禁用日志，避免警告
        var logType = UnityEngine.Application.GetStackTraceLogType(LogType.Warning);
        UnityEngine.Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

        try
        {
            GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                // 检查图标是否真的存在（通过检查 tooltip 或 image 名称）
                Texture2D icon = iconContent.image as Texture2D;
                if (icon != null)
                {
                    return icon;
                }
            }
        }
        catch { }
        finally
        {
            // 恢复日志输出设置
            UnityEngine.Application.SetStackTraceLogType(LogType.Warning, logType);
        }

        return null;
    }

    // 尝试加载多个备选图标
    private Texture2D GetIconSafely(params string[] iconNames)
    {
        foreach (string iconName in iconNames)
        {
            Texture2D icon = GetIconSafely(iconName);
            if (icon != null)
                return icon;
        }
        return null;
    }

    private int LayerMaskField(string label, int mask)
    {
        List<string> ls = new List<string>(); List<int> ln = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            string n = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(n)) { ls.Add(n); ln.Add(i); }
        }
        int m = 0; for (int i = 0; i < ln.Count; i++) if (((1 << ln[i]) & mask) != 0) m |= (1 << i);
        m = EditorGUILayout.MaskField(label, m, ls.ToArray());
        int f = 0; for (int i = 0; i < ln.Count; i++) if ((m & (1 << i)) != 0) f |= (1 << ln[i]);
        return f;
    }
}