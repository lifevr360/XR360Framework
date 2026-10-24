using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[System.Serializable]
public class Node
{
    public string name;

    [Header("UI")]
    public Button button;

    [Tooltip("Background shown when this node's children are visible")]
    public GameObject background;

    [Header("Hierarchy")]
    public List<Node> children = new List<Node>();

    [Header("Actions (Leaf Nodes Only)")]
    public UnityEvent onSelected;

    public bool HasChildren
    {
        get { return children != null && children.Count > 0; }
    }
}

public class NodeFramework : MonoBehaviour
{
    [Header("Root Nodes (Always Visible)")]
    public List<Node> rootNodes = new List<Node>();

    [Header("Root Background (Optional)")]
    public GameObject rootBackground;

    // Networking hooks: when set (by NetworkSync), clicks are sent to the host
    // instead of being applied locally. When null, the menu works offline as before.
    public System.Action<string> onNodeRequested;
    public System.Action onBackRequested;

    // Stack used only for child navigation (never roots)
    private Stack<Node> childStack = new Stack<Node>();

    // Stores layer-1 children of currently active root
    private List<Node> activeRootChildren = null;
    private Node activeRoot = null;

    // Stable path IDs ("0", "0.1", "0.1.3") so every device agrees on which node is which
    private Dictionary<string, Node> nodesById = new Dictionary<string, Node>();
    private Dictionary<Node, string> idsByNode = new Dictionary<Node, string>();

    // ---------------- UNITY ----------------

    void Awake()
    {
        AssignIds(rootNodes, "");
        InitializeButtons(rootNodes);

        // Root buttons are always visible
        foreach (var root in rootNodes)
        {
            if (root.button != null)
                root.button.gameObject.SetActive(true);
        }

        // Hide everything else
        HideAllChildren();
        ShowBackground(rootBackground);
    }

    // ---------------- INITIALIZATION ----------------

    private void AssignIds(List<Node> nodes, string prefix)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            string id = prefix.Length == 0 ? i.ToString() : prefix + "." + i;

            nodesById[id] = node;
            idsByNode[node] = id;

            if (node.HasChildren)
                AssignIds(node.children, id);
        }
    }

    private void InitializeButtons(List<Node> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.button != null)
            {
                node.button.onClick.RemoveAllListeners();
                node.button.onClick.AddListener(() => OnNodeButtonClicked(node));
            }

            if (node.HasChildren)
                InitializeButtons(node.children);
        }
    }

    // ---------------- CLICK HANDLING ----------------

    private void OnNodeButtonClicked(Node node)
    {
        if (onNodeRequested != null)
            onNodeRequested(idsByNode[node]);
        else
            ApplyNodeClick(node, true);
    }

    // Wired to the Back button's OnClick
    public void GoBack()
    {
        if (onBackRequested != null)
            onBackRequested();
        else
            ApplyGoBack();
    }

    public void ApplyNodeClick(string id)
    {
        Node node;
        if (nodesById.TryGetValue(id, out node))
            ApplyNodeClick(node, true);
        else
            Debug.LogWarning("NodeFramework: unknown node id " + id);
    }

    private void ApplyNodeClick(Node node, bool invokeActions)
    {
        if (node == null)
            return;

        // Invoke actions FIRST (if any are wired)
        if (invokeActions && node.onSelected != null && node.onSelected.GetPersistentEventCount() > 0)
        {
            node.onSelected.Invoke();
        }

        // ROOT CLICK
        if (rootNodes.Contains(node))
        {
            childStack.Clear();

            // Childless roots (e.g. EVENTS) count as "at root" so the previous submenu is cleared
            activeRoot = node.HasChildren ? node : null;
            activeRootChildren = node.HasChildren ? node.children : null;

            RenderChildren(activeRootChildren);
            ShowBackground(node.background != null ? node.background : rootBackground);

            return;
        }

        // CHILD CLICK
        if (node.HasChildren)
        {
            childStack.Push(node);
            RenderChildren(node.children);
            ShowBackground(node.background);
        }
    }

    public void ApplyGoBack()
    {
        // Case 1: already at root only
        if (childStack.Count == 0 && activeRootChildren == null)
            return;

        // Case 2: coming back from layer 2 or deeper
        if (childStack.Count > 0)
        {
            childStack.Pop();

            if (childStack.Count > 0)
            {
                RenderChildren(childStack.Peek().children);
                ShowBackground(childStack.Peek().background);
            }
            else
            {
                // Back to layer 1
                RenderChildren(activeRootChildren);
                ShowBackground(GetActiveRootBackground());
            }

            return;
        }

        // Case 3: back from layer 1 to root only
        activeRoot = null;
        activeRootChildren = null;
        RenderChildren(null);
        ShowBackground(rootBackground);
    }

    // ---------------- STATE (for network sync) ----------------

    // ID of the deepest open menu, or "" when only the root buttons are showing
    public string CurrentMenuId
    {
        get
        {
            if (childStack.Count > 0)
                return idsByNode[childStack.Peek()];
            if (activeRoot != null)
                return idsByNode[activeRoot];
            return "";
        }
    }

    // Rebuilds the menu to show the given menu ID, without running any onSelected actions
    public void RestoreMenu(string menuId)
    {
        if (menuId == CurrentMenuId)
            return;

        childStack.Clear();
        activeRoot = null;
        activeRootChildren = null;
        RenderChildren(null);
        ShowBackground(rootBackground);

        if (string.IsNullOrEmpty(menuId))
            return;

        // Open each level of the path in turn: "0.4.1" -> "0", "0.4", "0.4.1"
        string[] parts = menuId.Split('.');
        string path = "";
        foreach (var part in parts)
        {
            path = path.Length == 0 ? part : path + "." + part;

            Node node;
            if (!nodesById.TryGetValue(path, out node))
            {
                Debug.LogWarning("NodeFramework: cannot restore menu " + menuId);
                return;
            }

            ApplyNodeClick(node, false);
        }
    }

    // ---------------- RENDERING ----------------

    private void RenderChildren(List<Node> nodesToShow)
    {
        // Always reset child visibility from scratch
        HideAllChildren();

        if (nodesToShow == null)
            return;

        foreach (var node in nodesToShow)
        {
            if (node.button != null)
                node.button.gameObject.SetActive(true);
        }
    }

    private void HideAllChildren()
    {
        HideChildrenRecursive(rootNodes);
    }

    private void HideChildrenRecursive(List<Node> nodes)
    {
        foreach (var node in nodes)
        {
            if (!rootNodes.Contains(node) && node.button != null)
                node.button.gameObject.SetActive(false);

            if (node.HasChildren)
                HideChildrenRecursive(node.children);
        }
    }

    // ---------------- BACKGROUND HANDLING ----------------

    private void ShowBackground(GameObject bg)
    {
        HideAllBackgrounds();

        if (bg != null)
            bg.SetActive(true);
    }

    private void HideAllBackgrounds()
    {
        HideBackgroundRecursive(rootNodes);
    }

    private void HideBackgroundRecursive(List<Node> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.background != null)
                node.background.SetActive(false);

            if (node.HasChildren)
                HideBackgroundRecursive(node.children);
        }
    }

    private GameObject GetActiveRootBackground()
    {
        if (activeRootChildren == null)
            return rootBackground;

        foreach (var root in rootNodes)
        {
            if (root.children == activeRootChildren)
                return root.background;
        }

        return rootBackground;
    }
}
