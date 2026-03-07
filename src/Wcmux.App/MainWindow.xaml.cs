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
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        await CreateInitialSessionAsync();
    }

    internal async Task CreateInitialSessionAsync()
    {
        SessionStatusText.Text = "Initial terminal session requested.";
        await _sessionManager.CreateSessionAsync(SessionLaunchSpec.CreateDefaultPowerShell(Environment.CurrentDirectory));
    }
}
