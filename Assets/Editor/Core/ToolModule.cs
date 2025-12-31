using UnityEngine;
using UnityEditor;

/// <summary>
/// 工具模块抽象基类 - 所有功能模块的统一接口
/// </summary>
public abstract class ToolModule
{
    /// <summary>
    /// 模块显示名称
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 模块分类（用于分组显示）
    /// </summary>
    public virtual string Category => "General";

    /// <summary>
    /// 排序顺序（同分类内）
    /// </summary>
    public virtual int Order => 0;

    /// <summary>
    /// 模块图标名称（Unity内置图标）
    /// </summary>
    public virtual string IconName => "d_Settings";

    /// <summary>
    /// 标题栏颜色
    /// </summary>
    public virtual Color HeaderColor => new Color(0.5f, 0.5f, 0.5f);

    /// <summary>
    /// 背景颜色
    /// </summary>
    public virtual Color BackgroundColor => new Color(0.85f, 0.85f, 0.85f);

    /// <summary>
    /// 模块是否可用（基于上下文判断）
    /// </summary>
    public virtual bool IsAvailable(ToolContext context) => true;

    /// <summary>
    /// 绘制模块UI（由窗口调用）
    /// </summary>
    public abstract void OnGUI(ToolContext context);

    /// <summary>
    /// 模块初始化（窗口打开时调用一次）
    /// </summary>
    public virtual void OnInitialize() { }

    /// <summary>
    /// 模块清理（窗口关闭时调用）
    /// </summary>
    public virtual void OnCleanup() { }
}

