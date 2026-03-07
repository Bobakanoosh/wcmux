using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wcmux.App.Terminal;
using Wcmux.Core.Runtime;
using Wcmux.Core.Terminal;

namespace Wcmux.App.Views;

/// <summary>
/// Native pane host that attaches a WebView2 terminal surface to a live
/// ConPTY session. Uses a <see cref="TerminalSurfaceBridge"/> for output
/// batching, input routing, resize debounce, and cwd tracking. Keeps
/// xterm.js details out of the runtime core.
/// </summary>
public sealed partial class TerminalPaneView : UserControl
{
    private WebViewTerminalController? _controller;
    private TerminalSurfaceBridge? _bridge;
    private ISession? _session;
    private SessionManager? _sessionManager;
    private string? _sessionId;
    private bool _attached;

    public TerminalPaneView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// The session ID this pane is attached to, if any.
    /// </summary>
    public string? SessionId => _sessionId;

    /// <summary>
    /// The bridge instance, if attached. Exposed for testing/inspection.
    /// </summary>
    internal TerminalSurfaceBridge? Bridge => _bridge;

    /// <summary>
    /// Attaches this pane to a live session. Initializes the WebView2 terminal
    /// surface and the bridge, then begins rendering session output.
    /// </summary>
    public async Task AttachAsync(SessionManager sessionManager, ISession session)
    {
        if (_attached) return;

        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _sessionId = session.SessionId;

        _controller = new WebViewTerminalController(TerminalWebView);

        var readyTcs = new TaskCompletionSource();

        await _controller.InitializeAsync(
            onInput: OnTerminalInput,
            onResize: OnTerminalResize,
            onReady: () => readyTcs.TrySetResult());

        // Create the bridge -- output writes go through the controller
        // which must be called on the UI thread
        _bridge = new TerminalSurfaceBridge(
            _session,
            writeToSurface: async text =>
            {
                // Marshal the batched output to the UI thread
                var tcs = new TaskCompletionSource();
                DispatcherQueue?.TryEnqueue(async () =>
                {
                    try
                    {
                        if (_controller is not null && _attached)
                        {
                            await _controller.WriteOutputAsync(text);
                        }
                    }
                    finally
                    {
                        tcs.TrySetResult();
                    }
                });
                await tcs.Task;
            },
            initialCols: session.LaunchSpec.InitialColumns,
            initialRows: session.LaunchSpec.InitialRows);

        // Subscribe to session output events and route through bridge
        _sessionManager.SessionEventReceived += OnSessionEvent;

        // Wait for the terminal surface to signal ready, with a timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await readyTcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Proceed anyway -- the surface may still work
        }

        _attached = true;

        // Focus the terminal after attach
        await _controller.FocusAsync();
    }

    /// <summary>
    /// Detaches from the current session and disposes the bridge and controller.
    /// </summary>
    public async Task DetachAsync()
    {
        if (!_attached) return;
        _attached = false;

        if (_sessionManager is not null)
        {
            _sessionManager.SessionEventReceived -= OnSessionEvent;
        }

        if (_bridge is not null)
        {
            await _bridge.DisposeAsync();
            _bridge = null;
        }

        if (_controller is not null)
        {
            await _controller.DisposeAsync();
            _controller = null;
        }

        _session = null;
        _sessionId = null;
        _sessionManager = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // View loaded -- controller initialization happens in AttachAsync
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        await DetachAsync();
    }

    /// <summary>
    /// Handles keyboard/paste input from the terminal surface and routes
    /// it through the bridge to the ConPTY session.
    /// </summary>
    private async void OnTerminalInput(byte[] data)
    {
        if (_bridge is null) return;

        try
        {
            await _bridge.HandleInputAsync(data);
        }
        catch (ObjectDisposedException)
        {
            // Session was closed
        }
        catch (IOException)
        {
            // Pipe broken -- session is dead
        }
    }

    /// <summary>
    /// Handles resize notifications from the terminal surface. Routes
    /// through the bridge for debounce and redundancy suppression.
    /// </summary>
    private void OnTerminalResize(int cols, int rows)
    {
        _bridge?.RequestResize(cols, rows);
    }

    /// <summary>
    /// Handles session events from the session manager. Routes output
    /// through the bridge for batching, and tracks cwd changes.
    /// </summary>
    private void OnSessionEvent(object? sender, SessionEvent evt)
    {
        if (evt.SessionId != _sessionId) return;

        switch (evt)
        {
            case SessionOutputEvent output:
                // Enqueue to bridge for batched delivery
                _bridge?.EnqueueOutput(output.Text);
                break;

            case SessionCwdChangedEvent cwdEvt:
                _bridge?.HandleCwdChanged(cwdEvt.WorkingDirectory);
                break;

            case SessionExitedEvent:
                _bridge?.EnqueueOutput("\r\n\x1b[90m[Session ended]\x1b[0m\r\n");
                break;
        }
    }
}
