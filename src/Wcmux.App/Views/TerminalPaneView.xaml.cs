using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wcmux.App.Terminal;
using Wcmux.Core.Runtime;

namespace Wcmux.App.Views;

/// <summary>
/// Native pane host that attaches a WebView2 terminal surface to a live
/// ConPTY session. Owns the WebViewTerminalController lifecycle and bridges
/// user input and session output through the controller without exposing
/// xterm.js details to the rest of the app.
/// </summary>
public sealed partial class TerminalPaneView : UserControl
{
    private WebViewTerminalController? _controller;
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
    /// Attaches this pane to a live session. Initializes the WebView2 terminal
    /// surface and begins rendering session output.
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

        // Subscribe to session output events
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
    /// Detaches from the current session and disposes the terminal controller.
    /// </summary>
    public async Task DetachAsync()
    {
        if (!_attached) return;
        _attached = false;

        if (_sessionManager is not null)
        {
            _sessionManager.SessionEventReceived -= OnSessionEvent;
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
    /// it to the ConPTY session.
    /// </summary>
    private async void OnTerminalInput(byte[] data)
    {
        if (_session is null || !_session.IsRunning) return;

        try
        {
            await _session.WriteInputAsync(data);
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
    /// Handles resize notifications from the terminal surface. Translates
    /// the new column/row dimensions to the ConPTY pseudoconsole.
    /// </summary>
    private async void OnTerminalResize(int cols, int rows)
    {
        if (_session is null || !_session.IsRunning) return;
        if (cols <= 0 || rows <= 0) return;

        try
        {
            await _session.ResizeAsync((short)cols, (short)rows);
        }
        catch (ObjectDisposedException)
        {
            // Session was closed
        }
        catch (InvalidOperationException)
        {
            // Resize failed -- session may be shutting down
        }
    }

    /// <summary>
    /// Handles session events from the session manager. Routes output
    /// to the terminal surface and handles session exit.
    /// </summary>
    private void OnSessionEvent(object? sender, SessionEvent evt)
    {
        if (evt.SessionId != _sessionId) return;

        switch (evt)
        {
            case SessionOutputEvent output:
                // Marshal to UI thread for WebView2 interaction
                DispatcherQueue?.TryEnqueue(async () =>
                {
                    if (_controller is not null && _attached)
                    {
                        await _controller.WriteOutputAsync(output.Text);
                    }
                });
                break;

            case SessionExitedEvent:
                // Session ended -- could show exit indicator in future
                DispatcherQueue?.TryEnqueue(async () =>
                {
                    if (_controller is not null && _attached)
                    {
                        await _controller.WriteOutputAsync(
                            "\r\n\x1b[90m[Session ended]\x1b[0m\r\n");
                    }
                });
                break;
        }
    }
}
