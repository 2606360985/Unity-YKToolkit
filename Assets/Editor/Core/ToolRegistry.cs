using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

/// <summary>
/// 工具模块注册表 - 自动发现并注册所有ToolModule子类
/// </summary>
public static class ToolRegistry
{
    private static List<ToolModule> _modules;
    private static bool _initialized = false;

    /// <summary>
    /// 所有已注册的模块
    /// </summary>
    public static List<ToolModule> Modules
    {
        get
        {
            if (!_initialized)
            {
                Initialize();
            }
            return _modules;
        }
    }

    /// <summary>
    /// 初始化注册表（自动发现所有ToolModule子类）
    /// </summary>
    private static void Initialize()
    {
        _modules = new List<ToolModule>();

        // 使用TypeCache自动发现所有ToolModule子类
        var moduleTypes = TypeCache.GetTypesDerivedFrom<ToolModule>()
            .Where(t => !t.IsAbstract && !t.IsInterface);

        foreach (var type in moduleTypes)
        {
            try
            {
                var module = (ToolModule)Activator.CreateInstance(type);
                _modules.Add(module);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to create module instance: {type.Name}\n{e}");
            }
        }

        // 按分类和顺序排序
        _modules = _modules
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Order)
            .ToList();

        _initialized = true;

        UnityEngine.Debug.Log($"ToolRegistry: Registered {_modules.Count} modules");
    }

    /// <summary>
    /// 重新扫描并注册模块（用于热重载）
    /// </summary>
    public static void Refresh()
    {
        _initialized = false;
        Initialize();
    }

    /// <summary>
    /// 按分类获取模块
    /// </summary>
    public static IEnumerable<ToolModule> GetModulesByCategory(string category)
    {
        return Modules.Where(m => m.Category == category);
    }

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public static IEnumerable<string> GetCategories()
    {
        return Modules.Select(m => m.Category).Distinct().OrderBy(c => c);
    }
}

