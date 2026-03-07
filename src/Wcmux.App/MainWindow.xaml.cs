using Microsoft.UI.Xaml;
using Wcmux.Core.Runtime;

namespace Wcmux.App;

public sealed partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;

    public MainWindow()
    {
        InitializeComponent();
        _sessionManager = new SessionManager();
        _sessionManager.SessionEventReceived += OnSessionEvent;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        await CreateInitialSessionAsync();
    }

    internal async Task CreateInitialSessionAsync()
    {
        SessionStatusText.Text = "Launching terminal session...";
        try
        {
            var spec = SessionLaunchSpec.CreateDefaultPowerShell(
                Environment.CurrentDirectory);
            var session = await _sessionManager.CreateSessionAsync(spec);
            SessionStatusText.Text = $"Session {session.SessionId[..8]} ready.";
        }
        catch (Exception ex)
        {
            SessionStatusText.Text = $"Session failed: {ex.Message}";
        }
    }

    private void OnSessionEvent(object? sender, SessionEvent evt)
    {
        // Marshal to UI thread for status updates
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (evt)
            {
                case SessionReadyEvent ready:
                    SessionStatusText.Text = $"Session {ready.SessionId[..8]} ready.";
                    break;
                case SessionExitedEvent exited:
                    SessionStatusText.Text = $"Session {exited.SessionId[..8]} exited (code {exited.ExitCode}).";
                    break;
            }
        });
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        _sessionManager.SessionEventReceived -= OnSessionEvent;
        await _sessionManager.DisposeAsync();
    }
}
