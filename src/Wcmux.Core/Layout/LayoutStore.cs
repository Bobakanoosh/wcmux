namespace Wcmux.Core.Layout;

/// <summary>
/// Observable layout state owning the split tree, active pane tracking,
/// focus history, and pane rectangle computation. All mutations go through
/// reducer commands -- UI widgets render the tree but never define it.
/// </summary>
public sealed class LayoutStore
{
    private readonly List<string> _focusHistory = new();
    private readonly object _lock = new();
    private LayoutNode _root;
    private string _activePaneId;
    private Dictionary<string, PaneRect> _paneRects = new();
    private double _containerWidth;
    private double _containerHeight;

    /// <summary>Fired after any layout mutation.</summary>
    public event Action? LayoutChanged;

    /// <summary>Fired when the active pane changes.</summary>
    public event Action<string>? ActivePaneChanged;

    /// <summary>
    /// Creates a layout store with a single root pane.
    /// </summary>
    public LayoutStore(string initialPaneId, string initialSessionId)
    {
        _root = new LeafNode
        {
            PaneId = initialPaneId,
            SessionId = initialSessionId,
        };
        _activePaneId = initialPaneId;
        _focusHistory.Add(initialPaneId);
    }

    /// <summary>The current tree root.</summary>
    public LayoutNode Root
    {
        get { lock (_lock) return _root; }
    }

    /// <summary>The currently focused pane ID.</summary>
    public string ActivePaneId
    {
        get { lock (_lock) return _activePaneId; }
    }

    /// <summary>
    /// Ordered focus history (most recent last). Used for focus
    /// restoration after pane close.
    /// </summary>
    public IReadOnlyList<string> FocusHistory
    {
        get { lock (_lock) return _focusHistory.ToList().AsReadOnly(); }
    }

    /// <summary>
    /// Current computed pane rectangles keyed by pane ID.
    /// </summary>
    public IReadOnlyDictionary<string, PaneRect> PaneRects
    {
        get { lock (_lock) return new Dictionary<string, PaneRect>(_paneRects); }
    }

    /// <summary>
    /// All pane IDs currently in the tree.
    /// </summary>
    public List<string> AllPaneIds
    {
        get { lock (_lock) return LayoutReducer.GetAllPaneIds(_root); }
    }

    /// <summary>
    /// Gets the session ID for the specified pane.
    /// </summary>
    public string? GetSessionId(string paneId)
    {
        lock (_lock)
        {
            return FindSessionId(_root, paneId);
        }
    }

    /// <summary>
    /// Splits the active pane along the given axis. Returns the new pane ID
    /// and session ID pairing so the caller can launch a session.
    /// </summary>
    public (string NewPaneId, LeafNode NewLeaf) SplitActivePane(
        SplitAxis axis,
        string newPaneId,
        string newSessionId)
    {
        lock (_lock)
        {
            var (newRoot, newLeaf) = LayoutReducer.SplitPane(
                _root, _activePaneId, axis, newPaneId, newSessionId);

            _root = newRoot;
            SetActivePaneInternal(newPaneId);
            RecomputeRects();
        }

        LayoutChanged?.Invoke();
        return (newPaneId, (LeafNode)FindNode(_root, newPaneId)!);
    }

    /// <summary>
    /// Closes the specified pane. Returns the session ID that was attached
    /// so the caller can tear it down.
    /// </summary>
    public string? ClosePane(string paneId)
    {
        string? sessionId;
        string? newFocus;

        lock (_lock)
        {
            sessionId = FindSessionId(_root, paneId);

            // Determine focus target before closing
            newFocus = LayoutReducer.FindFocusAfterClose(_root, paneId, _focusHistory);

            var newRoot = LayoutReducer.ClosePane(_root, paneId);
            if (newRoot is null)
            {
                // Last pane closed -- null out root so AllPaneIds/Root reflect empty state
                _root = null!;
                _focusHistory.RemoveAll(id => id == paneId);
                return sessionId;
            }

            _root = newRoot;
            _focusHistory.RemoveAll(id => id == paneId);

            if (_activePaneId == paneId && newFocus is not null)
            {
                SetActivePaneInternal(newFocus);
            }

            RecomputeRects();
        }

        LayoutChanged?.Invoke();
        return sessionId;
    }

    /// <summary>
    /// Closes the currently active pane.
    /// </summary>
    public string? CloseActivePane()
    {
        string active;
        lock (_lock)
        {
            active = _activePaneId;
        }
        return ClosePane(active);
    }

    /// <summary>
    /// Sets the active pane by ID. Fires ActivePaneChanged.
    /// </summary>
    public void SetActivePane(string paneId)
    {
        lock (_lock)
        {
            if (!ContainsPane(_root, paneId)) return;
            if (_activePaneId == paneId) return;
            SetActivePaneInternal(paneId);
        }

        ActivePaneChanged?.Invoke(paneId);
    }

    /// <summary>
    /// Moves focus in the given direction using geometric pane rectangles.
    /// Returns the new active pane ID, or null if no pane was found.
    /// </summary>
    public string? FocusDirection(Direction direction)
    {
        string? target;

        lock (_lock)
        {
            target = LayoutReducer.FindDirectionalFocus(
                _root, _activePaneId, direction, _paneRects);

            if (target is null) return null;

            SetActivePaneInternal(target);
        }

        ActivePaneChanged?.Invoke(target);
        return target;
    }

    /// <summary>
    /// Resizes the active pane in the given direction.
    /// </summary>
    public void ResizeActivePane(Direction direction, double step = LayoutReducer.ResizeStep)
    {
        lock (_lock)
        {
            _root = LayoutReducer.ResizePane(_root, _activePaneId, direction, step);
            RecomputeRects();
        }

        LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Updates the container dimensions and recomputes pane rectangles.
    /// </summary>
    public void UpdateContainerSize(double width, double height)
    {
        lock (_lock)
        {
            _containerWidth = width;
            _containerHeight = height;
            RecomputeRects();
        }

        LayoutChanged?.Invoke();
    }

    /// <summary>
    /// Returns whether the tree contains only one pane (root is a leaf).
    /// </summary>
    public bool IsSinglePane
    {
        get { lock (_lock) return _root is LeafNode; }
    }

    #region Private helpers

    private void SetActivePaneInternal(string paneId)
    {
        _activePaneId = paneId;
        // Keep focus history bounded and deduplicated at the tail
        _focusHistory.Remove(paneId);
        _focusHistory.Add(paneId);

        // Bound history length
        if (_focusHistory.Count > 50)
        {
            _focusHistory.RemoveAt(0);
        }
    }

    private void RecomputeRects()
    {
        if (_containerWidth <= 0 || _containerHeight <= 0)
            return;

        _paneRects = LayoutReducer.ComputePaneRects(
            _root, _containerWidth, _containerHeight);
    }

    private static string? FindSessionId(LayoutNode node, string paneId)
    {
        if (node is LeafNode leaf && leaf.PaneId == paneId)
            return leaf.SessionId;

        if (node is SplitNode split)
        {
            var result = FindSessionId(split.First, paneId);
            if (result is not null) return result;
            return FindSessionId(split.Second, paneId);
        }

        return null;
    }

    private static LayoutNode? FindNode(LayoutNode node, string paneId)
    {
        if (node is LeafNode leaf && leaf.PaneId == paneId)
            return leaf;

        if (node is SplitNode split)
        {
            var result = FindNode(split.First, paneId);
            if (result is not null) return result;
            return FindNode(split.Second, paneId);
        }

        return null;
    }

    private static bool ContainsPane(LayoutNode node, string paneId)
    {
        if (node is LeafNode leaf)
            return leaf.PaneId == paneId;

        if (node is SplitNode split)
            return ContainsPane(split.First, paneId) || ContainsPane(split.Second, paneId);

        return false;
    }

    #endregion
}
