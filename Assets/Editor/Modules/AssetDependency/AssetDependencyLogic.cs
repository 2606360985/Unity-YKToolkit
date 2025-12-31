using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// 资产依赖可视化逻辑 - 纯逻辑，与UI完全解耦
/// </summary>
public static class AssetDependencyLogic
{
    /// <summary>
    /// 依赖节点数据
    /// </summary>
    public class DependencyNode
    {
        public string AssetPath;
        public string AssetName;
        public Object AssetObject;
        public List<string> Dependencies = new List<string>(); // 依赖的资产
        public List<string> ReferencedBy = new List<string>(); // 引用此资产的资产
        public NodeType Type;

        public DependencyNode(string path)
        {
            AssetPath = path;
            AssetName = Path.GetFileName(path);
            AssetObject = AssetDatabase.LoadAssetAtPath<Object>(path);
            Type = GetNodeType(path);
        }

        private NodeType GetNodeType(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".prefab") return NodeType.Prefab;
            if (ext == ".mat") return NodeType.Material;
            if (ext == ".cs" || ext == ".js") return NodeType.Script;
            if (ext == ".png" || ext == ".jpg" || ext == ".tga" || ext == ".exr") return NodeType.Texture;
            if (ext == ".asset") return NodeType.Asset;
            if (AssetDatabase.IsValidFolder(path)) return NodeType.Folder;
            return NodeType.Other;
        }
    }

    /// <summary>
    /// 节点类型
    /// </summary>
    public enum NodeType
    {
        Prefab,
        Material,
        Script,
        Texture,
        Asset,
        Folder,
        Other
    }

    /// <summary>
    /// 依赖图数据
    /// </summary>
    public class DependencyGraph
    {
        public DependencyNode RootNode;
        public List<DependencyNode> AllNodes = new List<DependencyNode>();
        public Dictionary<string, DependencyNode> NodeMap = new Dictionary<string, DependencyNode>();

        public void AddNode(DependencyNode node)
        {
            if (!NodeMap.ContainsKey(node.AssetPath))
            {
                AllNodes.Add(node);
                NodeMap[node.AssetPath] = node;
            }
        }

        public DependencyNode GetOrCreateNode(string path)
        {
            if (NodeMap.TryGetValue(path, out var node))
                return node;

            node = new DependencyNode(path);
            AddNode(node);
            return node;
        }
    }

    /// <summary>
    /// 分析资产的依赖关系
    /// </summary>
    public static DependencyGraph AnalyzeDependencies(Object[] selectedAssets)
    {
        if (selectedAssets == null || selectedAssets.Length == 0)
            return null;

        DependencyGraph graph = new DependencyGraph();
        HashSet<string> processedPaths = new HashSet<string>();

        // 处理每个选中的资产
        foreach (var asset in selectedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                continue;

            var rootNode = graph.GetOrCreateNode(assetPath);
            if (graph.RootNode == null)
                graph.RootNode = rootNode;

            // 查找依赖（这个资产引用了什么）
            FindDependencies(assetPath, rootNode, graph, processedPaths);

            // 查找引用者（谁引用了这个资产）
            FindReferencers(assetPath, rootNode, graph, processedPaths);
        }

        return graph;
    }

    /// <summary>
    /// 查找资产的依赖
    /// </summary>
    private static void FindDependencies(string assetPath, DependencyNode node, DependencyGraph graph, HashSet<string> processed)
    {
        if (processed.Contains(assetPath))
            return;

        processed.Add(assetPath);

        // 获取直接依赖
        string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
        
        foreach (string depPath in dependencies)
        {
            // 排除自己
            if (depPath == assetPath)
                continue;

            // 排除脚本（通常不需要显示）
            if (Path.GetExtension(depPath).ToLower() == ".cs")
                continue;

            node.Dependencies.Add(depPath);
            
            // 递归查找依赖的依赖（可选，避免太深）
            if (!processed.Contains(depPath))
            {
                var depNode = graph.GetOrCreateNode(depPath);
                FindDependencies(depPath, depNode, graph, processed);
            }
        }
    }

    /// <summary>
    /// 查找引用此资产的资产
    /// </summary>
    private static void FindReferencers(string assetPath, DependencyNode node, DependencyGraph graph, HashSet<string> processed)
    {
        // 获取所有资产路径
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
        
        foreach (string otherPath in allAssetPaths)
        {
            // 跳过脚本和文件夹
            string ext = Path.GetExtension(otherPath).ToLower();
            if (ext == ".cs" || ext == ".js" || AssetDatabase.IsValidFolder(otherPath))
                continue;

            // 跳过自己
            if (otherPath == assetPath)
                continue;

            // 检查这个资产是否依赖目标资产
            string[] deps = AssetDatabase.GetDependencies(otherPath, false);
            if (deps.Contains(assetPath))
            {
                node.ReferencedBy.Add(otherPath);
                graph.GetOrCreateNode(otherPath);
            }
        }
    }

    /// <summary>
    /// 获取节点类型的颜色
    /// </summary>
    public static Color GetNodeTypeColor(NodeType type)
    {
        switch (type)
        {
            case NodeType.Prefab:
                return new Color(0.2f, 0.8f, 0.3f); // 绿色
            case NodeType.Material:
                return new Color(0.2f, 0.6f, 1f); // 蓝色
            case NodeType.Script:
                return new Color(0.8f, 0.8f, 0.2f); // 黄色
            case NodeType.Texture:
                return new Color(1f, 0.5f, 0.2f); // 橙色
            case NodeType.Asset:
                return new Color(0.7f, 0.7f, 0.7f); // 灰色
            case NodeType.Folder:
                return new Color(0.5f, 0.5f, 0.5f); // 深灰
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// 获取节点类型的图标
    /// </summary>
    public static string GetNodeTypeIcon(NodeType type)
    {
        switch (type)
        {
            case NodeType.Prefab:
                return "d_Prefab";
            case NodeType.Material:
                return "d_Material";
            case NodeType.Script:
                return "d_Script";
            case NodeType.Texture:
                return "d_Texture2D";
            case NodeType.Asset:
                return "d_ScriptableObject";
            case NodeType.Folder:
                return "d_Folder";
            default:
                return "d_DefaultAsset";
        }
    }
}

