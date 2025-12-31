using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 工具箱主窗口 - 使用新框架，同时兼容旧模块系统
/// </summary>
public class ToolboxWindow : EditorWindow
{
    private Vector2 scrollPos;
    private ToolContext context = new ToolContext();

    // 模块管理
    private List<ModuleWrapper> modules = new List<ModuleWrapper>();
    private Dictionary<string, List<ModuleWrapper>> modulesByCategory = new Dictionary<string, List<ModuleWrapper>>();
    private Dictionary<string, bool> categoryExpanded = new Dictionary<string, bool>();
    
    // 工具栏
    private string _searchText = "";
    private bool _showStatistics = false;
    
    // 布局设置
    private bool _useTwoColumnLayout = false;
    private float _twoColumnThreshold = 600f;
    
    // 主题设置
    private enum ThemeType { Auto, Light, Dark }
    private ThemeType _currentTheme = ThemeType.Auto;
    
    // 拖拽相关
    private string draggedCategory = null;
    private int draggedModuleIndex = -1;
    private string dragTargetCategory = null;
    private int dragTargetIndex = -1;
    private Vector2 dragStartPos;
    private int dragControlID = -1;

    // 模块包装器（统一新旧系统）
    private class ModuleWrapper
    {
        public ToolModule module; // 新系统模块
        public System.Action legacyDrawAction; // 旧系统绘制方法
        public bool isLegacy; // 是否为旧系统模块
        public bool isExpanded;
        public string moduleId; // 用于持久化
        public bool _hasTrackedUsage; // 是否已记录工具使用（避免每帧都记录）

        public ModuleWrapper(ToolModule module)
        {
            this.module = module;
            this.isLegacy = false;
            this.moduleId = module.GetType().FullName;
            this._hasTrackedUsage = false;
        }

        public ModuleWrapper(System.Action drawAction, string id)
        {
            this.legacyDrawAction = drawAction;
            this.isLegacy = true;
            this.moduleId = id;
            this._hasTrackedUsage = false;
        }

        public string Name => isLegacy ? "Legacy" : module.Name;
        public string IconName => isLegacy ? "d_Settings" : module.IconName;
        public Color HeaderColor => isLegacy ? Color.gray : module.HeaderColor;
        public Color BackgroundColor => isLegacy ? Color.gray : module.BackgroundColor;
    }

    [MenuItem("Tools/🚀Unity省力小工具箱")]
    public static void ShowWindow() => GetWindow<ToolboxWindow>("Pro Toolbox");

    private void OnEnable()
    {
        InitializeModules();
        // 注册更新事件以支持烘焙进度显示
        EditorApplication.update += OnUpdate;
        // 加载布局设置
        _useTwoColumnLayout = ToolboxSettings.GetBool("UseTwoColumnLayout", false);
        _twoColumnThreshold = ToolboxSettings.GetFloat("TwoColumnThreshold", 600f);
        // 加载主题设置
        _currentTheme = (ThemeType)ToolboxSettings.GetInt("ThemeType", (int)ThemeType.Auto);
    }

    private void OnDisable()
    {
        // 清理所有模块
        foreach (var wrapper in modules)
        {
            if (!wrapper.isLegacy && wrapper.module != null)
            {
                wrapper.module.OnCleanup();
            }
        }
        // 取消注册更新事件
        EditorApplication.update -= OnUpdate;
    }

    private void OnUpdate()
    {
        // 如果正在烘焙，持续重绘窗口以更新进度
        if (Lightmapping.isRunning)
        {
            Repaint();
        }
    }


    private void OnGUI()
    {
        // 更新上下文
        context.Update();

        // 绘制标题栏
        DrawHeader();
        
        // 绘制工具栏
        DrawToolbar();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 检查窗口宽度，决定是否使用双列布局（自动模式）
        // 如果用户手动切换了布局，则使用用户设置
        if (!ToolboxSettings.GetBool("ManualLayoutOverride", false))
        {
            _useTwoColumnLayout = position.width > _twoColumnThreshold;
        }

        // 拖拽时每帧重置目标索引
        if (draggedModuleIndex >= 0)
        {
            dragTargetIndex = -1;
            dragTargetCategory = null;
        }

        // 按分类绘制模块（应用搜索过滤）
        var categories = ToolRegistry.GetCategories();
        
        if (_useTwoColumnLayout)
        {
            DrawModulesTwoColumn(categories);
        }
        else
        {
            DrawModulesSingleColumn(categories);
        }

        // 处理全局拖拽事件
        HandleGlobalDragEvents();

        EditorGUILayout.Space(20);
        EditorGUILayout.EndScrollView();
    }

    private void InitializeModules()
    {
        modules.Clear();
        modulesByCategory.Clear();
        categoryExpanded.Clear();

        // 1. 加载新系统的模块（从注册表）
        foreach (var module in ToolRegistry.Modules)
        {
            var wrapper = new ModuleWrapper(module);
            modules.Add(wrapper);
            module.OnInitialize();

            // 按分类分组
            string category = module.Category;
            if (!modulesByCategory.ContainsKey(category))
            {
                modulesByCategory[category] = new List<ModuleWrapper>();
            }
            modulesByCategory[category].Add(wrapper);
        }

        // 2. 加载旧系统的模块（从原窗口）
        LoadLegacyModules();

        // 3. 加载保存的顺序和折叠状态
        LoadModuleOrderAndState();
        
        // 4. 加载分类折叠状态
        LoadCategoryExpandedStates();
        
        // 5. 按Order排序每个分类内的模块
        foreach (var kvp in modulesByCategory)
        {
            kvp.Value.Sort((a, b) =>
            {
                if (a.isLegacy || b.isLegacy) return 0;
                return a.module.Order.CompareTo(b.module.Order);
            });
        }
    }
    
    private void LoadCategoryExpandedStates()
    {
        var categories = ToolRegistry.GetCategories();
        foreach (var category in categories)
        {
            categoryExpanded[category] = ToolboxSettings.LoadCategoryExpanded(category, true);
        }
    }
    
    private bool IsCategoryExpanded(string category)
    {
        return categoryExpanded.ContainsKey(category) ? categoryExpanded[category] : true;
    }
    
    private void SetCategoryExpanded(string category, bool expanded)
    {
        categoryExpanded[category] = expanded;
        ToolboxSettings.SaveCategoryExpanded(category, expanded);
    }

    private void LoadLegacyModules()
    {
        // 这里可以添加旧系统的模块
        // 例如：modules.Add(new ModuleWrapper(DrawLegacyModule, "Legacy_1"));
    }

    private void LoadModuleOrderAndState()
    {
        int[] savedOrder = ToolboxSettings.LoadModuleOrder();
        if (savedOrder != null && savedOrder.Length == modules.Count)
        {
            // 按保存的顺序重新排列
            var orderedModules = new List<ModuleWrapper>(modules.Count);
            foreach (int id in savedOrder)
            {
                if (id >= 0 && id < modules.Count)
                {
                    orderedModules.Add(modules[id]);
                }
            }
            // 添加任何缺失的模块
            foreach (var module in modules)
            {
                if (!orderedModules.Contains(module))
                {
                    orderedModules.Add(module);
                }
            }
            modules = orderedModules;
        }

        // 加载折叠状态
        foreach (var wrapper in modules)
        {
            wrapper.isExpanded = ToolboxSettings.LoadModuleExpanded(wrapper.moduleId, false);
        }
    }

    private void DrawCategoryHeader(string category)
    {
        Event evt = Event.current;
        
        EditorGUILayout.BeginVertical("box");
        
        // 一级菜单：更高的标题栏（34px），更明显的视觉差异
        Rect headerRect = EditorGUILayout.GetControlRect(false, 34);
        Color categoryColor = GetCategoryColor(category);
        
        // 背景色（更深的分类颜色，一级菜单更明显）
        Color bgColor = new Color(categoryColor.r * 0.35f, categoryColor.g * 0.35f, categoryColor.b * 0.35f, 0.5f);
        
        // 悬停效果
        bool isHovering = headerRect.Contains(evt.mousePosition);
        if (isHovering)
        {
            bgColor = new Color(categoryColor.r * 0.45f, categoryColor.g * 0.45f, categoryColor.b * 0.45f, 0.6f);
        }
        
        EditorGUI.DrawRect(headerRect, bgColor);
        
        // 左侧彩色条（12px宽，一级菜单更宽）
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 12, headerRect.height), categoryColor);
        
        // 折叠按钮区域
        Rect foldoutRect = new Rect(headerRect.x + 16, headerRect.y + 7, 20, 20);
        bool expanded = IsCategoryExpanded(category);
        
        // 点击整个标题栏切换折叠
        bool isInClickableArea = headerRect.Contains(evt.mousePosition);
        
        // 鼠标悬停时改变光标
        if (isInClickableArea)
        {
            EditorGUIUtility.AddCursorRect(headerRect, MouseCursor.Link);
        }
        
        if (evt.type == EventType.MouseDown && isInClickableArea && evt.button == 0)
        {
            SetCategoryExpanded(category, !expanded);
            GUI.changed = true;
            evt.Use();
            Repaint();
        }
        
        // 绘制折叠按钮（仅显示状态，但更大更明显）
        EditorGUI.BeginDisabledGroup(true);
        EditorGUI.Foldout(foldoutRect, expanded, "", true);
        EditorGUI.EndDisabledGroup();
        
        // 分类图标和名称（一级菜单字体）
        string[] iconNames = GetCategoryIcons(category);
        int moduleCount = modulesByCategory.ContainsKey(category) ? modulesByCategory[category].Count : 0;
        string displayText = $"{GetCategoryDisplayName(category)} ({moduleCount})";
        
        Texture2D icon = IconHelper.GetIconSafely(iconNames);
        GUIContent content = new GUIContent(displayText);
        if (icon != null)
        {
            content.image = icon;
        }
        Rect labelRect = new Rect(headerRect.x + 40, headerRect.y + 3, headerRect.width - 44, 22); // 向上空间缩小：从+6改为+3
        
        GUIStyle categoryStyle = ToolboxStyles.CategoryStyle;
        categoryStyle.fontSize = 13; // 字体缩小1级：从14改为13
        categoryStyle.normal.textColor = Color.white; // 一级菜单使用白色文字，更醒目
        categoryStyle.fontStyle = FontStyle.Bold;
        GUI.Label(labelRect, content, categoryStyle);
        
        // 添加底部边框线，增强一级菜单的视觉层次（更明显的分隔）
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y + headerRect.height - 2, headerRect.width, 3), 
            new Color(categoryColor.r * 0.6f, categoryColor.g * 0.6f, categoryColor.b * 0.6f, 0.4f));
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(-12); // 一级菜单间距更大，与二级菜单区分更明显
    }
    
    private void DrawDraggableModule(string category, int indexInCategory, ModuleWrapper wrapper)
    {
        Event evt = Event.current;

        // 二级菜单缩进：缩进大小等于一级菜单标头色块宽度（12px）
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10); // 一级菜单标头色块宽度
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(false));

        // 二级菜单：较小的标题栏（24px），更浅的背景
        Rect titleRect = EditorGUILayout.GetControlRect(false, 24);
        Color titleBgColor = new Color(wrapper.HeaderColor.r * 0.1f, wrapper.HeaderColor.g * 0.1f, wrapper.HeaderColor.b * 0.1f, 0.2f);

        // 如果正在拖拽此模块，高亮显示
        if (draggedCategory == category && draggedModuleIndex == indexInCategory)
        {
            titleBgColor = new Color(wrapper.HeaderColor.r * 0.3f, wrapper.HeaderColor.g * 0.3f, wrapper.HeaderColor.b * 0.3f, 0.4f);
        }

        EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, titleRect.width, titleRect.height), titleBgColor);

        // 绘制左侧彩色条（3px宽，二级菜单更细）
        EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, 3, titleRect.height), wrapper.HeaderColor);

        // 定义区域（二级菜单缩进更多，体现层级关系）
        Rect foldoutRect = new Rect(titleRect.x + 20, titleRect.y + 2, 18, 20);
        Rect dragHandleRect = new Rect(titleRect.x + 42, titleRect.y + 3, 16, 16);

        // 处理拖拽
        bool isInDragHandle = dragHandleRect.Contains(evt.mousePosition);
        bool isInFoldout = foldoutRect.Contains(evt.mousePosition);
        bool isInTitleRect = titleRect.Contains(evt.mousePosition);
        // 可点击区域：整个标题栏，但排除拖拽手柄
        bool isInClickableArea = isInTitleRect && !isInDragHandle;

        // 处理拖拽开始
        if (evt.type == EventType.MouseDown && isInDragHandle && !isInFoldout && evt.button == 0 && draggedModuleIndex == -1)
        {
            draggedCategory = category;
            draggedModuleIndex = indexInCategory;
            dragStartPos = evt.mousePosition;
            dragTargetIndex = -1;
            dragTargetCategory = null;
            dragControlID = GUIUtility.GetControlID("ModuleDrag".GetHashCode(), FocusType.Passive);
            GUIUtility.hotControl = dragControlID;
            evt.Use();
            Repaint();
        }

        // 处理点击标题栏切换折叠（排除拖拽手柄和拖拽中）
        if (evt.type == EventType.MouseDown && isInClickableArea && evt.button == 0 && draggedModuleIndex == -1)
        {
            wrapper.isExpanded = !wrapper.isExpanded;
            ToolboxSettings.SaveModuleExpanded(wrapper.moduleId, wrapper.isExpanded);
            GUI.changed = true;
            evt.Use();
            Repaint();
        }

        // 拖拽中：计算目标模块
        if (draggedModuleIndex >= 0 && draggedCategory == category && indexInCategory != draggedModuleIndex && isInTitleRect && !isInFoldout)
        {
            dragTargetCategory = category;
            dragTargetIndex = indexInCategory;
        }

        // 绘制拖拽手柄（悬停时显示）
        bool showDragHandle = isInDragHandle || (draggedCategory == category && draggedModuleIndex == indexInCategory);
        if (showDragHandle || isInTitleRect)
        {
            DrawDragHandle(dragHandleRect, showDragHandle);
        }

        // 绘制折叠按钮（仅显示状态，不处理点击）
        EditorGUI.BeginDisabledGroup(true);
        EditorGUI.Foldout(foldoutRect, wrapper.isExpanded, "", true);
        EditorGUI.EndDisabledGroup();

        // 绘制标题和图标（二级菜单字体更小，颜色更浅）
        GUIStyle titleStyle = ToolboxStyles.TitleStyle;
        titleStyle.fontSize = 11; // 二级菜单字体更小
        titleStyle.normal.textColor = new Color(wrapper.HeaderColor.r * 0.9f, wrapper.HeaderColor.g * 0.9f, wrapper.HeaderColor.b * 0.9f); // 二级菜单颜色更浅
        titleStyle.fontStyle = FontStyle.Normal; // 二级菜单不加粗

        string displayTitle = wrapper.Name; // 移除全局序号
        GUIContent titleContent = IconHelper.GetIconContent(wrapper.IconName, " " + displayTitle);

        Rect labelRect = new Rect(titleRect.x + 62, titleRect.y + 3, titleRect.width - 64, 18);
        GUI.Label(labelRect, titleContent, titleStyle);
        
        // 绘制状态指示器（如果模块不可用）
        if (!wrapper.isLegacy && !wrapper.module.IsAvailable(context))
        {
            Rect statusRect = new Rect(titleRect.x + titleRect.width - 28, titleRect.y + 5, 12, 12);
            Texture2D warningIcon = IconHelper.GetIconSafely("d_console.warnicon", "console.warnicon", "d_Warning", "Warning", "d_console.warnicon.sml");
            if (warningIcon != null)
            {
                GUI.DrawTexture(statusRect, warningIcon);
            }
        }

        // 如果正在拖拽其他模块到此位置，显示插入指示线
        if (draggedModuleIndex >= 0 && draggedCategory == category && draggedModuleIndex != indexInCategory && dragTargetCategory == category && dragTargetIndex == indexInCategory)
        {
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y - 2, titleRect.width, 2), new Color(0.2f, 0.6f, 1f, 0.8f));
        }

        EditorGUILayout.Space(-3);

        // 如果展开，绘制内容
        if (wrapper.isExpanded)
        {
            // 记录工具使用（当模块展开时，说明用户在使用这个工具）
            if (!wrapper.isLegacy && wrapper.module != null)
            {
                // 只在第一次展开时记录，避免每帧都记录
                if (!wrapper._hasTrackedUsage)
                {
                    ToolUsageTracker.Track(wrapper.module.Name);
                    wrapper._hasTrackedUsage = true;
                }
            }

            // 模块内容区域：不设置最小高度限制，让内容自然展开显示
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(false));

            // 绘制模块内容（模块内部会处理可用性检查）
            if (wrapper.isLegacy)
            {
                wrapper.legacyDrawAction?.Invoke();
            }
            else
            {
                // 总是调用OnGUI，让模块自己处理不可用的情况（显示提示信息）
                wrapper.module.OnGUI(context);
            }

            EditorGUILayout.EndVertical();
        }
        else
        {
            // 当模块折叠时，重置追踪标记，下次展开时重新记录
            if (!wrapper.isLegacy && wrapper.module != null)
            {
                wrapper._hasTrackedUsage = false;
            }
        }

        EditorGUILayout.Space(-3);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(-8); // 二级菜单之间的间距
    }
    
    private Color GetCategoryColor(string category)
    {
        switch (category)
        {
            case "Assets": return new Color(0.2f, 0.6f, 1f);      // 蓝色
            case "Scene": return new Color(0.3f, 0.8f, 0.3f);      // 绿色
            case "Lighting": return new Color(0.9f, 0.7f, 0.2f);   // 橙色
            case "Tools": return new Color(0.6f, 0.6f, 0.6f);     // 灰色
            case "Analytics": return new Color(0.5f, 0.3f, 0.9f);  // 紫色
            default: return Color.gray;
        }
    }
    
    private string[] GetCategoryIcons(string category)
    {
        switch (category)
        {
            case "Assets": 
                return new string[] { "d_Folder", "Folder", "d_Folder Icon", "Folder Icon" };
            case "Scene": 
                return new string[] { "d_SceneAsset", "SceneAsset", "d_Scene Icon", "Scene Icon" };
            case "Lighting": 
                return new string[] { "d_Lighting", "Lighting", "d_Light", "Light" };
            case "Tools": 
                return new string[] { "d_Settings", "Settings", "d_Settings Icon", "Settings Icon" };
            case "Analytics": 
                return new string[] { "Profiler.Statistics", "d_Profiler.Statistics", "Profiler", "d_Profiler", "d_Chart", "Chart" };
            default: 
                return new string[] { "d_Settings", "Settings", "d_Settings Icon", "Settings Icon" };
        }
    }
    
    // 保留旧方法以保持兼容性
    private string GetCategoryIcon(string category)
    {
        string[] icons = GetCategoryIcons(category);
        return icons != null && icons.Length > 0 ? icons[0] : "d_Settings";
    }
    
    private string GetCategoryDisplayName(string category)
    {
        switch (category)
        {
            case "Assets": return "📁 资产管理";
            case "Scene": return "🎬 场景编辑";
            case "Lighting": return "💡 光照";
            case "Tools": return "🔧 工具";
            case "Analytics": return "📊 分析";
            default: return category;
        }
    }

    private void DrawDragHandle(Rect rect, bool isHighlighted)
    {
        Texture2D dragHandleIcon = IconHelper.GetIconSafely("d_Grid", "Grid", "d_MoveTool", "MoveTool", "d_Grip");

        if (dragHandleIcon != null)
        {
            GUI.color = isHighlighted ? new Color(1f, 1f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.4f);
            GUI.DrawTexture(rect, dragHandleIcon, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }
        else
        {
            // 绘制简单的拖拽手柄（三条横线）
            Color handleColor = isHighlighted ?
                new Color(0.7f, 0.7f, 0.7f, 0.9f) :
                new Color(0.5f, 0.5f, 0.5f, 0.5f);

            float lineWidth = 12f;
            float lineHeight = 1.5f;
            float spacing = 2.5f;
            float startX = rect.x + (rect.width - lineWidth) * 0.5f;
            float startY = rect.y + (rect.height - (lineHeight * 3 + spacing * 2)) * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                Rect lineRect = new Rect(startX, startY + i * (lineHeight + spacing), lineWidth, lineHeight);
                EditorGUI.DrawRect(lineRect, handleColor);
            }
        }
    }

    private void HandleGlobalDragEvents()
    {
        Event evt = Event.current;

        if (draggedModuleIndex >= 0 && !string.IsNullOrEmpty(draggedCategory))
        {
            // 拖拽中：吞掉 MouseDrag，避免其它控件抢事件
            if (evt.type == EventType.MouseDrag)
            {
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseUp)
            {
                // 执行模块移动（同一分类内）
                if (!string.IsNullOrEmpty(dragTargetCategory) && dragTargetCategory == draggedCategory && 
                    dragTargetIndex >= 0 && draggedModuleIndex != dragTargetIndex)
                {
                    var categoryModules = modulesByCategory[draggedCategory];
                    if (dragTargetIndex < categoryModules.Count)
                    {
                        var draggedModule = categoryModules[draggedModuleIndex];
                        categoryModules.RemoveAt(draggedModuleIndex);

                        int insertIndex = dragTargetIndex;
                        if (draggedModuleIndex < dragTargetIndex)
                            insertIndex--;

                        if (insertIndex >= 0 && insertIndex <= categoryModules.Count)
                        {
                            categoryModules.Insert(insertIndex, draggedModule);
                            SaveModuleOrder();
                            GUI.changed = true;
                        }
                    }
                }

                // 重置拖拽状态
                draggedCategory = null;
                draggedModuleIndex = -1;
                dragTargetCategory = null;
                dragTargetIndex = -1;
                GUIUtility.hotControl = 0;
                dragControlID = -1;
                evt.Use();
                Repaint();
            }
            // 如果鼠标移出窗口，也重置状态
            else if (evt.type == EventType.MouseLeaveWindow)
            {
                draggedCategory = null;
                draggedModuleIndex = -1;
                dragTargetCategory = null;
                dragTargetIndex = -1;
                GUIUtility.hotControl = 0;
                dragControlID = -1;
                Repaint();
            }
        }
    }

    private void SaveModuleOrder()
    {
        // 保存模块顺序（按分类和Order排序）
        // 这里可以保存分类内的顺序，但暂时保持简单
        // 如果需要跨分类拖拽，需要更复杂的实现
    }

    private void DrawHeader()
    {
        // 绘制渐变背景标题栏
        Rect headerRect = EditorGUILayout.GetControlRect(false, 55);
        
        // 根据主题调整颜色
        bool isDark = IsDarkTheme();
        Color headerColor1 = isDark ? 
            new Color(0.1f, 0.1f, 0.15f) : 
            new Color(0.15f, 0.35f, 0.75f);
        Color headerColor2 = isDark ?
            new Color(0.2f, 0.2f, 0.3f) :
            new Color(0.25f, 0.55f, 0.95f);

        // 绘制背景
        EditorGUI.DrawRect(headerRect, headerColor1);

        // 绘制底部装饰线
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y + headerRect.height - 3, headerRect.width, 3), headerColor2);

        // 绘制标题和图标
        GUIContent headerContent = IconHelper.GetIconContent("d_Settings", "🚀 Unity Pro Toolbox v2.0 | Unity 极速助手 | RepinSKY");

        Rect labelRect = new Rect(headerRect.x + 10, headerRect.y, headerRect.width - 20, headerRect.height);
        GUI.Label(labelRect, headerContent, ToolboxStyles.HeaderStyle);

        EditorGUILayout.Space(8);
    }
    
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // 搜索框
        GUILayout.Label("🔍", GUILayout.Width(20));
        string newSearchText = GUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(150));
        if (newSearchText != _searchText)
        {
            _searchText = newSearchText;
            Repaint();
        }
        
        // 清除搜索按钮
        if (!string.IsNullOrEmpty(_searchText))
        {
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _searchText = "";
                Repaint();
            }
        }
        
        GUILayout.FlexibleSpace();
        
        // 统计按钮
        if (GUILayout.Button("📊 统计", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            _showStatistics = !_showStatistics;
            Repaint();
        }
        
        // 布局切换按钮（仅在宽窗口时显示）
        if (position.width > _twoColumnThreshold)
        {
            string layoutText = _useTwoColumnLayout ? "单列" : "双列";
            if (GUILayout.Button($"📐 {layoutText}", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _useTwoColumnLayout = !_useTwoColumnLayout;
                ToolboxSettings.SetBool("UseTwoColumnLayout", _useTwoColumnLayout);
                ToolboxSettings.SetBool("ManualLayoutOverride", true);
                Repaint();
            }
        }
        
        // 主题切换按钮
        string themeIcon = GetThemeIcon();
        if (GUILayout.Button(themeIcon, EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            CycleTheme();
        }
        
        // 设置按钮
        if (GUILayout.Button("⚙️ 设置", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ShowSettingsMenu();
        }
        
        EditorGUILayout.EndHorizontal();
        
        // 显示统计面板
        if (_showStatistics)
        {
            DrawStatisticsPanel();
        }
    }
    
    private void DrawStatisticsPanel()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📊 工具箱统计", EditorStyles.boldLabel);
        
        int totalModules = modules.Count;
        EditorGUILayout.LabelField($"总模块数: {totalModules}");
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("各分类模块数:", EditorStyles.boldLabel);
        foreach (var kvp in modulesByCategory)
        {
            string categoryName = GetCategoryDisplayName(kvp.Key);
            EditorGUILayout.LabelField($"  {categoryName}: {kvp.Value.Count}");
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("展开所有分类", GUILayout.Height(20)))
        {
            ExpandAllCategories();
        }
        if (GUILayout.Button("折叠所有分类", GUILayout.Height(20)))
        {
            CollapseAllCategories();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }
    
    private void ShowSettingsMenu()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("展开所有分类"), false, () => ExpandAllCategories());
        menu.AddItem(new GUIContent("折叠所有分类"), false, () => CollapseAllCategories());
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("主题/自动"), _currentTheme == ThemeType.Auto, () => SetTheme(ThemeType.Auto));
        menu.AddItem(new GUIContent("主题/浅色"), _currentTheme == ThemeType.Light, () => SetTheme(ThemeType.Light));
        menu.AddItem(new GUIContent("主题/深色"), _currentTheme == ThemeType.Dark, () => SetTheme(ThemeType.Dark));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("重置所有设置"), false, () => {
            if (EditorUtility.DisplayDialog("确认重置", "确定要重置所有设置吗？", "确定", "取消"))
            {
                ResetAllSettings();
            }
        });
        menu.ShowAsContext();
    }
    
    private void CycleTheme()
    {
        ThemeType nextTheme = (ThemeType)(((int)_currentTheme + 1) % 3);
        SetTheme(nextTheme);
    }
    
    private void SetTheme(ThemeType theme)
    {
        _currentTheme = theme;
        ToolboxSettings.SetInt("ThemeType", (int)theme);
        Repaint();
    }
    
    private string GetThemeIcon()
    {
        bool isDark = IsDarkTheme();
        return isDark ? "🌙" : "☀️";
    }
    
    private bool IsDarkTheme()
    {
        switch (_currentTheme)
        {
            case ThemeType.Light: return false;
            case ThemeType.Dark: return true;
            case ThemeType.Auto:
            default:
                // 自动检测Unity编辑器主题
                return EditorGUIUtility.isProSkin;
        }
    }
    
    private void ExpandAllCategories()
    {
        foreach (var category in ToolRegistry.GetCategories())
        {
            SetCategoryExpanded(category, true);
        }
        Repaint();
    }
    
    private void CollapseAllCategories()
    {
        foreach (var category in ToolRegistry.GetCategories())
        {
            SetCategoryExpanded(category, false);
        }
        Repaint();
    }
    
    private void ResetAllSettings()
    {
        // 重置分类折叠状态
        foreach (var category in ToolRegistry.GetCategories())
        {
            ToolboxSettings.DeleteKey("CategoryExpanded_" + category);
        }
        // 重置模块折叠状态
        foreach (var wrapper in modules)
        {
            ToolboxSettings.DeleteKey("ModuleExpanded_" + wrapper.moduleId);
        }
        LoadCategoryExpandedStates();
        Repaint();
    }
    
    private List<ModuleWrapper> GetFilteredModules(string category)
    {
        if (!modulesByCategory.ContainsKey(category))
            return new List<ModuleWrapper>();
        
        if (string.IsNullOrEmpty(_searchText))
            return modulesByCategory[category];
        
        string searchLower = _searchText.ToLower();
        return modulesByCategory[category].Where(wrapper =>
        {
            string name = wrapper.Name.ToLower();
            string categoryName = category.ToLower();
            return name.Contains(searchLower) || categoryName.Contains(searchLower);
        }).ToList();
    }
    
    private void DrawModulesSingleColumn(System.Collections.Generic.IEnumerable<string> categories)
    {
        foreach (var category in categories)
        {
            if (!modulesByCategory.ContainsKey(category) || modulesByCategory[category].Count == 0)
                continue;

            // 应用搜索过滤
            var filteredModules = GetFilteredModules(category);
            if (filteredModules.Count == 0 && !string.IsNullOrEmpty(_searchText))
                continue;

            DrawCategoryHeader(category);
            
            if (IsCategoryExpanded(category))
            {
                for (int i = 0; i < filteredModules.Count; i++)
                {
                    var wrapper = filteredModules[i];
                    int originalIndex = modulesByCategory[category].IndexOf(wrapper);
                    DrawDraggableModule(category, originalIndex, wrapper);
                }
            }
        }
    }
    
    private void DrawModulesTwoColumn(System.Collections.Generic.IEnumerable<string> categories)
    {
        var categoryList = categories.ToList();
        int totalCategories = categoryList.Count;
        int leftColumnCount = (totalCategories + 1) / 2;
        
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(false));
        
        // 左列 - 不扩展高度，让内容自然展开显示
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.49f), GUILayout.ExpandHeight(false));
        for (int i = 0; i < leftColumnCount; i++)
        {
            var category = categoryList[i];
            DrawCategoryWithModules(category);
        }
        EditorGUILayout.EndVertical();
        
        // 右列 - 不扩展高度，让内容自然展开显示
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.49f), GUILayout.ExpandHeight(false));
        for (int i = leftColumnCount; i < totalCategories; i++)
        {
            var category = categoryList[i];
            DrawCategoryWithModules(category);
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawCategoryWithModules(string category)
    {
        if (!modulesByCategory.ContainsKey(category) || modulesByCategory[category].Count == 0)
            return;

        // 应用搜索过滤
        var filteredModules = GetFilteredModules(category);
        if (filteredModules.Count == 0 && !string.IsNullOrEmpty(_searchText))
            return;

        DrawCategoryHeader(category);
        
        if (IsCategoryExpanded(category))
        {
            for (int i = 0; i < filteredModules.Count; i++)
            {
                var wrapper = filteredModules[i];
                int originalIndex = modulesByCategory[category].IndexOf(wrapper);
                DrawDraggableModule(category, originalIndex, wrapper);
            }
        }
    }
}

