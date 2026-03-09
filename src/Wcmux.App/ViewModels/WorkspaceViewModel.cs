using Wcmux.Core.Layout;
using Wcmux.Core.Runtime;

namespace Wcmux.App.ViewModels;

/// <summary>
/// App-shell orchestration for pane commands, session creation, and cwd
/// inheritance. Routes split, close, focus, and resize commands through
/// the reducer-owned layout store instead of directly mutating UI controls.
/// </summary>
public sealed class WorkspaceViewModel : IAsyncDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly LayoutStore _layoutStore;
    private readonly Dictionary<string, ISession> _paneSessions = new();
    private bool _disposed;

    /// <summary>Fired when the layout tree changes and the view should re-render.</summary>
    public event Action? LayoutChanged;

    /// <summary>Fired when the active pane changes so the view can update highlights.</summary>
    public event Action<string>? ActivePaneChanged;

    /// <summary>Fired when the last pane is closed (app should exit).</summary>
    public event Action? LastPaneClosed;

    /// <summary>The layout store powering the split tree.</summary>
    public LayoutStore LayoutStore => _layoutStore;

    /// <summary>The session manager for creating and closing sessions.</summary>
    public SessionManager SessionManager => _sessionManager;

    /// <summary>
    /// Creates a workspace view model with the initial root session already created.
    /// </summary>
    public WorkspaceViewModel(
        SessionManager sessionManager,
        string initialPaneId,
        ISession initialSession)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _layoutStore = new LayoutStore(initialPaneId, initialSession.SessionId);
        _paneSessions[initialPaneId] = initialSession;

        _layoutStore.LayoutChanged += () => LayoutChanged?.Invoke();
        _layoutStore.ActivePaneChanged += paneId => ActivePaneChanged?.Invoke(paneId);
    }

    /// <summary>
    /// Splits the active pane along the given axis. Launches a fresh session
    /// that inherits the source pane's last known cwd from the runtime layer.
    /// Returns the new pane ID.
    /// </summary>
    public async Task<string> SplitActivePaneAsync(SplitAxis axis, CancellationToken ct = default)
    {
        var sourcePaneId = _layoutStore.ActivePaneId;
        var sourceSession = GetSessionForPane(sourcePaneId);

        // Determine cwd: use last known cwd from source, fall back to launch spec
        var cwd = sourceSession?.LastKnownCwd
                  ?? sourceSession?.LaunchSpec.InitialWorkingDirectory
                  ?? Environment.CurrentDirectory;

        // Create a new session with inherited cwd
        var spec = SessionLaunchSpec.CreateDefaultPowerShell(cwd);
        var newSession = await _sessionManager.CreateSessionAsync(spec, ct);

        var newPaneId = Guid.NewGuid().ToString("N");
        _layoutStore.SplitActivePane(axis, newPaneId, newSession.SessionId);
        _paneSessions[newPaneId] = newSession;

        return newPaneId;
    }

    /// <summary>
    /// Splits the active pane and creates a browser pane (no ConPTY session).
    /// The new pane uses a sentinel session ID prefixed with "browser:".
    /// </summary>
    public Task<string> SplitActivePaneAsBrowserAsync(SplitAxis axis, CancellationToken ct = default)
    {
        var newPaneId = Guid.NewGuid().ToString("N");
        var sentinelSessionId = "browser:" + Guid.NewGuid().ToString("N");

        // Browser panes have no terminal session -- skip session creation
        _layoutStore.SplitActivePane(axis, newPaneId, sentinelSessionId, PaneKind.Browser);

        // Do NOT add to _paneSessions -- browser panes have no ConPTY session
        return Task.FromResult(newPaneId);
    }

    /// <summary>
    /// Splits the active pane horizontally.
    /// </summary>
    public Task<string> SplitActivePaneHorizontalAsync(CancellationToken ct = default)
        => SplitActivePaneAsync(SplitAxis.Horizontal, ct);

    /// <summary>
    /// Splits the active pane vertically.
    /// </summary>
    public Task<string> SplitActivePaneVerticalAsync(CancellationToken ct = default)
        => SplitActivePaneAsync(SplitAxis.Vertical, ct);

    /// <summary>
    /// Closes the active pane and tears down its session.
    /// </summary>
    public async Task CloseActivePaneAsync(CancellationToken ct = default)
    {
        var paneId = _layoutStore.ActivePaneId;
        await ClosePaneAsync(paneId, ct);
    }

    /// <summary>
    /// Closes a specific pane by ID. Tears down the session and collapses the tree.
    /// </summary>
    public async Task ClosePaneAsync(string paneId, CancellationToken ct = default)
    {
        var sessionId = _layoutStore.ClosePane(paneId);

        // Browser panes have no entry in _paneSessions (sentinel session ID
        // starting with "browser:"), so skip session teardown for them.
        if (_paneSessions.Remove(paneId, out _) && sessionId is not null)
        {
            await _sessionManager.CloseSessionAsync(sessionId, ct);
        }

        // If layout store returned sessionId but no panes remain, fire last pane closed
        if (_layoutStore.AllPaneIds.Count == 0 || _layoutStore.Root is null)
        {
            LastPaneClosed?.Invoke();
        }
    }

    /// <summary>
    /// Moves focus in the given direction.
    /// </summary>
    public void FocusDirection(Direction direction)
    {
        _layoutStore.FocusDirection(direction);
    }

    /// <summary>Keyboard shortcut: focus left.</summary>
    public void FocusLeft() => FocusDirection(Direction.Left);

    /// <summary>Keyboard shortcut: focus right.</summary>
    public void FocusRight() => FocusDirection(Direction.Right);

    /// <summary>Keyboard shortcut: focus up.</summary>
    public void FocusUp() => FocusDirection(Direction.Up);

    /// <summary>Keyboard shortcut: focus down.</summary>
    public void FocusDown() => FocusDirection(Direction.Down);

    /// <summary>
    /// Swaps the active pane's content with the neighbor in the given direction.
    /// </summary>
    public void SwapActivePane(Direction direction)
    {
        _layoutStore.SwapActivePane(direction);
    }

    /// <summary>
    /// Sets the split ratio of a specific split node by NodeId.
    /// </summary>
    public void SetSplitRatio(string nodeId, double newRatio)
    {
        _layoutStore.SetSplitRatio(nodeId, newRatio);
    }

    /// <summary>
    /// Moves source pane adjacent to target pane in the given direction.
    /// </summary>
    public void MovePane(string sourcePaneId, string targetPaneId, Direction dropSide)
    {
        _layoutStore.MovePane(sourcePaneId, targetPaneId, dropSide);
    }

    /// <summary>
    /// Resizes the active pane in the given direction.
    /// </summary>
    public void ResizeActivePane(Direction direction)
    {
        _layoutStore.ResizeActivePane(direction);
    }

    /// <summary>
    /// Sets the active pane by ID (e.g., from mouse click).
    /// </summary>
    public void SetActivePane(string paneId)
    {
        _layoutStore.SetActivePane(paneId);
    }

    /// <summary>
    /// Updates the container dimensions for pane rect computation.
    /// </summary>
    public void UpdateContainerSize(double width, double height)
    {
        _layoutStore.UpdateContainerSize(width, height);
    }

    /// <summary>
    /// Gets the session attached to a pane.
    /// </summary>
    public ISession? GetSessionForPane(string paneId)
    {
        _paneSessions.TryGetValue(paneId, out var session);
        return session;
    }

    /// <summary>
    /// Gets the session ID for a pane from the layout store.
    /// </summary>
    public string? GetSessionIdForPane(string paneId)
    {
        return _layoutStore.GetSessionId(paneId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (_, session) in _paneSessions)
        {
            try
            {
                await session.CloseAsync();
            }
            catch
            {
                // Best-effort during shutdown
            }
        }
        _paneSessions.Clear();
    }
}
