using Wcmux.Core.Layout;
using Wcmux.Core.Runtime;

namespace Wcmux.App.ViewModels;

/// <summary>
/// Orchestrates tab lifecycle: creates/switches/closes/renames tabs,
/// manages per-tab WorkspaceViewModel instances, and surfaces events
/// for the UI layer. Wraps TabStore + SessionManager.
/// </summary>
public sealed class TabViewModel : IAsyncDisposable
{
    private readonly TabStore _tabStore;
    private readonly SessionManager _sessionManager;
    private readonly Dictionary<string, WorkspaceViewModel> _workspaces = new();
    private bool _disposed;

    /// <summary>Fired when the last tab is closed -- signals app exit.</summary>
    public event Action? LastTabClosed;

    /// <summary>The underlying tab store for UI binding.</summary>
    public TabStore TabStore => _tabStore;

    /// <summary>The session manager shared across all tabs.</summary>
    public SessionManager SessionManager => _sessionManager;

    /// <summary>
    /// The WorkspaceViewModel for the currently active tab, or null if no tabs.
    /// </summary>
    public WorkspaceViewModel? ActiveWorkspace =>
        _tabStore.ActiveTabId is not null && _workspaces.TryGetValue(_tabStore.ActiveTabId, out var ws)
            ? ws
            : null;

    public TabViewModel(SessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _tabStore = new TabStore();

        _tabStore.LastTabClosed += () => LastTabClosed?.Invoke();
    }

    /// <summary>
    /// Creates a new tab with a fresh session in the user's home directory.
    /// Returns the new tab ID.
    /// </summary>
    public async Task<string> CreateNewTabAsync(CancellationToken ct = default)
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var spec = SessionLaunchSpec.CreateDefaultPowerShell(homeDir);
        var session = await _sessionManager.CreateSessionAsync(spec, ct);

        var tabId = Guid.NewGuid().ToString("N");
        var paneId = Guid.NewGuid().ToString("N");

        var workspace = new WorkspaceViewModel(_sessionManager, paneId, session);
        _workspaces[tabId] = workspace;

        // When last pane in a tab closes, close the tab itself
        workspace.LastPaneClosed += () =>
        {
            _ = CloseTabAsync(tabId);
        };

        var label = PathHelper.FormatTabLabel(homeDir);
        _tabStore.CreateTab(tabId, paneId, session.SessionId, label);

        return tabId;
    }

    /// <summary>Switches to the specified tab.</summary>
    public void SwitchTab(string tabId) => _tabStore.SwitchTab(tabId);

    /// <summary>
    /// Closes the specified tab, disposing its WorkspaceViewModel.
    /// </summary>
    public async Task CloseTabAsync(string tabId, CancellationToken ct = default)
    {
        if (_workspaces.Remove(tabId, out var workspace))
        {
            await workspace.DisposeAsync();
        }
        _tabStore.CloseTab(tabId);
    }

    /// <summary>Renames a tab.</summary>
    public void RenameTab(string tabId, string newLabel)
        => _tabStore.RenameTab(tabId, newLabel);

    /// <summary>Enables or disables Windows notifications for a tab.</summary>
    public void SetTabNotificationsMuted(string tabId, bool muted)
        => _tabStore.SetNotificationsMuted(tabId, muted);

    /// <summary>
    /// Gets the WorkspaceViewModel for a specific tab ID.
    /// </summary>
    public WorkspaceViewModel? GetWorkspace(string tabId)
        => _workspaces.GetValueOrDefault(tabId);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var workspace in _workspaces.Values)
        {
            try
            {
                await workspace.DisposeAsync();
            }
            catch
            {
                // Best-effort during shutdown
            }
        }
        _workspaces.Clear();
    }
}
