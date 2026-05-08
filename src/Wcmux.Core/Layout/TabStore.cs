namespace Wcmux.Core.Layout;

/// <summary>
/// Tab collection state management. Owns per-tab LayoutStore instances,
/// tracks active tab, fires events on tab lifecycle changes.
/// Pure state -- no session or UI dependencies.
/// </summary>
public sealed class TabStore
{
    private readonly Dictionary<string, TabState> _tabs = new();
    private readonly List<string> _tabOrder = new();
    private string? _activeTabId;

    /// <summary>Fired after any structural tab change (create, close, rename).</summary>
    public event Action? TabsChanged;

    /// <summary>Fired when the active tab changes. Passes new active tab ID.</summary>
    public event Action<string>? ActiveTabChanged;

    /// <summary>Fired when the last tab is closed -- signals app exit.</summary>
    public event Action? LastTabClosed;

    /// <summary>The currently active tab ID, or null if no tabs.</summary>
    public string? ActiveTabId => _activeTabId;

    /// <summary>Ordered tab IDs (insertion order).</summary>
    public IReadOnlyList<string> TabOrder => _tabOrder.AsReadOnly();

    /// <summary>Number of tabs in the collection.</summary>
    public int TabCount => _tabs.Count;

    /// <summary>
    /// Convenience: returns the active tab's LayoutStore, or null if no tabs.
    /// </summary>
    public LayoutStore? ActiveLayout =>
        _activeTabId is not null && _tabs.TryGetValue(_activeTabId, out var tab)
            ? tab.Layout
            : null;

    /// <summary>
    /// Returns the TabState for the given ID, or null if not found.
    /// </summary>
    public TabState? GetTab(string tabId) =>
        _tabs.GetValueOrDefault(tabId);

    /// <summary>
    /// Creates a new tab with a fresh LayoutStore. Sets it as the active tab.
    /// </summary>
    public string CreateTab(string tabId, string initialPaneId,
        string initialSessionId, string defaultLabel)
    {
        var layout = new LayoutStore(initialPaneId, initialSessionId);
        _tabs[tabId] = new TabState(tabId, layout, defaultLabel, IsCustomLabel: false, NotificationsMuted: false);
        _tabOrder.Add(tabId);
        _activeTabId = tabId;

        TabsChanged?.Invoke();
        ActiveTabChanged?.Invoke(tabId);
        return tabId;
    }

    /// <summary>
    /// Switches to the specified tab. No-op if already active or unknown ID.
    /// </summary>
    public void SwitchTab(string tabId)
    {
        if (!_tabs.ContainsKey(tabId)) return;
        if (_activeTabId == tabId) return;

        _activeTabId = tabId;
        ActiveTabChanged?.Invoke(tabId);
    }

    /// <summary>
    /// Closes the specified tab. Returns the closed TabState so the caller
    /// can dispose resources. When closing the last tab, fires LastTabClosed.
    /// When closing the active tab, switches to the right neighbor (or left if none).
    /// </summary>
    public TabState? CloseTab(string tabId)
    {
        if (!_tabs.TryGetValue(tabId, out var closedTab)) return null;

        var index = _tabOrder.IndexOf(tabId);
        _tabs.Remove(tabId);
        _tabOrder.RemoveAt(index);

        if (_tabs.Count == 0)
        {
            _activeTabId = null;
            TabsChanged?.Invoke();
            LastTabClosed?.Invoke();
            return closedTab;
        }

        if (_activeTabId == tabId)
        {
            // Prefer right neighbor, then left
            var newIndex = index < _tabOrder.Count ? index : index - 1;
            _activeTabId = _tabOrder[newIndex];
            TabsChanged?.Invoke();
            ActiveTabChanged?.Invoke(_activeTabId);
        }
        else
        {
            TabsChanged?.Invoke();
        }

        return closedTab;
    }

    /// <summary>
    /// Renames a tab and marks it as custom-labeled. Fires TabsChanged.
    /// </summary>
    public void RenameTab(string tabId, string newLabel)
    {
        if (!_tabs.TryGetValue(tabId, out var tab)) return;

        _tabs[tabId] = tab with { Label = newLabel, IsCustomLabel = true };
        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Enables or disables Windows notifications for a tab. Fires TabsChanged.
    /// </summary>
    public void SetNotificationsMuted(string tabId, bool muted)
    {
        if (!_tabs.TryGetValue(tabId, out var tab)) return;
        if (tab.NotificationsMuted == muted) return;

        _tabs[tabId] = tab with { NotificationsMuted = muted };
        TabsChanged?.Invoke();
    }

    /// <summary>
    /// Immutable record representing a single tab's state.
    /// </summary>
    public record TabState(
        string TabId,
        LayoutStore Layout,
        string Label,
        bool IsCustomLabel,
        bool NotificationsMuted);
}
