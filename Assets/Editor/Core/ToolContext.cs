using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// 工具上下文 - 封装Unity API，解耦功能模块与Unity编辑器
/// </summary>
public class ToolContext
{
    /// <summary>
    /// 当前选中的GameObject数组
    /// </summary>
    public GameObject[] SelectedObjects { get; set; }

    /// <summary>
    /// 当前选中的Transform数组
    /// </summary>
    public Transform[] SelectedTransforms { get; set; }

    /// <summary>
    /// 当前选中的Object数组（包括资产）
    /// </summary>
    public Object[] SelectedObjectsAll { get; set; }

    /// <summary>
    /// 当前激活的场景
    /// </summary>
    public Scene ActiveScene { get; set; }

    /// <summary>
    /// 是否正在播放模式
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// 当前选中的活动GameObject
    /// </summary>
    public GameObject ActiveGameObject { get; set; }

    /// <summary>
    /// 当前选中的活动Transform
    /// </summary>
    public Transform ActiveTransform { get; set; }

    /// <summary>
    /// 更新上下文（每帧由窗口调用）
    /// </summary>
    public void Update()
    {
        SelectedObjects = Selection.gameObjects;
        SelectedTransforms = Selection.transforms;
        SelectedObjectsAll = Selection.objects;
        ActiveGameObject = Selection.activeGameObject;
        ActiveTransform = Selection.activeTransform;
        ActiveScene = SceneManager.GetActiveScene();
        IsPlaying = EditorApplication.isPlaying;
    }

    /// <summary>
    /// 是否有选中的GameObject
    /// </summary>
    public bool HasSelectedObjects => SelectedObjects != null && SelectedObjects.Length > 0;

    /// <summary>
    /// 是否有选中的Transform
    /// </summary>
    public bool HasSelectedTransforms => SelectedTransforms != null && SelectedTransforms.Length > 0;

    /// <summary>
    /// 是否有选中的对象（包括资产）
    /// </summary>
    public bool HasSelectedObjectsAll => SelectedObjectsAll != null && SelectedObjectsAll.Length > 0;
}

