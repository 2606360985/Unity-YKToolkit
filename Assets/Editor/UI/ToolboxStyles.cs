using UnityEngine;
using UnityEditor;

/// <summary>
/// 工具箱样式 - 统一管理UI样式
/// </summary>
public static class ToolboxStyles
{
    private static GUIStyle _headerStyle;
    private static GUIStyle _titleStyle;
    private static GUIStyle _categoryStyle;
    private static GUIStyle _buttonStyle;

    /// <summary>
    /// 标题栏样式
    /// </summary>
    public static GUIStyle HeaderStyle
    {
        get
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(15, 0, 0, 0)
                };
            }
            return _headerStyle;
        }
    }

    /// <summary>
    /// 模块标题样式
    /// </summary>
    public static GUIStyle TitleStyle
    {
        get
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(8, 0, 3, 0)
                };
            }
            return _titleStyle;
        }
    }

    /// <summary>
    /// 分类标题样式（一级菜单）
    /// </summary>
    public static GUIStyle CategoryStyle
    {
        get
        {
            if (_categoryStyle == null)
            {
                _categoryStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(8, 0, 2, 0) // 减少上边距：从4改为2
                };
            }
            return _categoryStyle;
        }
    }

    /// <summary>
    /// 按钮样式
    /// </summary>
    public static GUIStyle ButtonStyle(Color buttonColor)
    {
        return new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(10, 10, 5, 5),
            normal = { textColor = Color.white },
            hover = { textColor = Color.white }
        };
    }
}

