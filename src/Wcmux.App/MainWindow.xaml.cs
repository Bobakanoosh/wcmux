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
        try
        {
            var spec = SessionLaunchSpec.CreateDefaultPowerShell(
                Environment.CurrentDirectory);
            var session = await _sessionManager.CreateSessionAsync(spec);

            // Attach the root pane to the new session
            await RootPane.AttachAsync(_sessionManager, session);
        }
        catch (Exception ex)
        {
            // Show error in the window title if session creation fails
            Title = $"wcmux - Session failed: {ex.Message}";
        }
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        await RootPane.DetachAsync();
        await _sessionManager.DisposeAsync();
    }
}
