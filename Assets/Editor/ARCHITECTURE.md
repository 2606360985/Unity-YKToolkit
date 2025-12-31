# Unity Pro Toolbox 架构说明

## 📁 目录结构

```
Assets/Editor/
├── Core/                    # 核心框架（几乎不改）
│   ├── ToolModule.cs        # 工具模块抽象基类
│   ├── ToolContext.cs       # 工具上下文（解耦Unity API）
│   ├── ToolRegistry.cs      # 模块自动注册系统
│   └── ToolboxWindow.cs     # 主窗口（使用新框架）
│
├── Modules/                 # 功能模块（不断增加）
│   └── BatchRename/         # 示例：批量重命名模块
│       ├── BatchRenameModule.cs    # UI部分
│       └── BatchRenameLogic.cs     # 逻辑部分（与UI解耦）
│
├── UI/                      # 通用UI组件
│   ├── ToolboxStyles.cs     # UI样式管理
│   └── IconHelper.cs        # 图标加载助手
│
├── Utils/                   # 通用工具
│   ├── EditorSelectionUtil.cs   # Selection封装
│   ├── AssetUtil.cs            # AssetDatabase封装
│   └── UndoUtil.cs             # Undo封装
│
├── Settings/                # 配置 & 持久化
│   └── ToolboxSettings.cs   # 统一设置管理
│
└── UnityProToolbox.cs       # 旧版窗口（保留兼容）
```

## 🏗️ 核心概念

### 1. ToolModule（功能模块接口）

所有功能模块必须继承 `ToolModule` 抽象类：

```csharp
public class MyModule : ToolModule
{
    public override string Name => "我的模块";
    public override string Category => "General";
    public override int Order => 0;
    
    public override void OnGUI(ToolContext context)
    {
        // 绘制UI
        if (GUILayout.Button("执行"))
        {
            MyLogic.Execute(context);
        }
    }
}
```

### 2. ToolContext（上下文解耦）

封装Unity API，避免功能模块直接依赖Unity编辑器：

```csharp
public class ToolContext
{
    public GameObject[] SelectedObjects { get; set; }
    public Transform[] SelectedTransforms { get; set; }
    public Scene ActiveScene { get; set; }
    public bool IsPlaying { get; set; }
    // ...
}
```

### 3. ToolRegistry（自动注册）

自动发现并注册所有 `ToolModule` 子类，无需手动注册：

```csharp
// 自动扫描所有ToolModule子类
foreach (var module in ToolRegistry.Modules)
{
    if (module.IsAvailable(context))
        module.OnGUI(context);
}
```

## 📝 如何添加新功能模块

### 步骤1：创建逻辑类（与UI解耦）

```csharp
// Modules/MyFeature/MyFeatureLogic.cs
public static class MyFeatureLogic
{
    public class Settings
    {
        public int Value = 10;
    }
    
    public static void Execute(Settings settings)
    {
        // 纯逻辑，不依赖UI
        Debug.Log($"执行功能: {settings.Value}");
    }
}
```

### 步骤2：创建模块类（UI部分）

```csharp
// Modules/MyFeature/MyFeatureModule.cs
public class MyFeatureModule : ToolModule
{
    public override string Name => "我的功能";
    public override string Category => "General";
    public override int Order => 0;
    
    private MyFeatureLogic.Settings _settings = new MyFeatureLogic.Settings();
    
    public override void OnGUI(ToolContext context)
    {
        _settings.Value = EditorGUILayout.IntField("值", _settings.Value);
        
        if (GUILayout.Button("执行"))
        {
            MyFeatureLogic.Execute(_settings);
        }
    }
}
```

### 步骤3：完成！

模块会自动被 `ToolRegistry` 发现并注册，无需修改任何其他代码。

## 🔄 迁移旧功能到新架构

### 示例：批量重命名

**旧代码（UI和逻辑混在一起）：**
```csharp
private void BatchRenamePro()
{
    Object[] os = Selection.objects;
    Undo.RecordObjects(os, "Batch Rename");
    // ... 逻辑代码
}
```

**新代码（分离UI和逻辑）：**

1. **逻辑类** (`BatchRenameLogic.cs`)：
```csharp
public static class BatchRenameLogic
{
    public static void ExecuteBatchRename(Object[] objects, Settings settings)
    {
        // 纯逻辑代码
    }
}
```

2. **模块类** (`BatchRenameModule.cs`)：
```csharp
public class BatchRenameModule : ToolModule
{
    public override void OnGUI(ToolContext context)
    {
        // UI代码
        if (GUILayout.Button("重命名"))
        {
            BatchRenameLogic.ExecuteBatchRename(context.SelectedObjectsAll, _settings);
        }
    }
}
```

## 🎯 设计原则

1. **UI与逻辑分离**：所有业务逻辑放在 `*Logic.cs` 中，UI放在 `*Module.cs` 中
2. **自动注册**：新模块自动被发现，无需手动注册
3. **上下文解耦**：通过 `ToolContext` 访问Unity API，便于测试和复用
4. **渐进式迁移**：旧代码保留，新功能用新架构，逐步迁移

## 📚 工具类使用

### EditorSelectionUtil
```csharp
GameObject[] selected = EditorSelectionUtil.GetSelectedGameObjects();
```

### AssetUtil
```csharp
AssetUtil.RenameAsset(asset, "NewName");
```

### UndoUtil
```csharp
UndoUtil.RecordObjects(objects, "Operation");
```

### ToolboxSettings
```csharp
ToolboxSettings.SetInt("MyKey", 100);
int value = ToolboxSettings.GetInt("MyKey", 0);
```

## 🚀 下一步

1. 逐步将旧功能迁移到新架构
2. 每个功能拆分为 `*Logic.cs` 和 `*Module.cs`
3. 使用 `ToolContext` 替代直接访问Unity API
4. 使用工具类封装常用操作

