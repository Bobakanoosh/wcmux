using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace Wcmux.App;

public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Callback invoked when a toast notification is activated (clicked).
    /// Passes (tabId, paneId) for deep-link navigation.
    /// </summary>
    public static Action<string, string>? OnNotificationActivated;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // CRITICAL: Subscribe before Register() per Windows App SDK requirements.
        // If Register() is called first, toast activation spawns a new process.
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        AppNotificationManager.Default.Register();

        _window = new MainWindow();
        _window.Activate();
    }

    private void OnNotificationInvoked(AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
        // Extract deep-link arguments from the toast
        if (args.Arguments.TryGetValue("tabId", out var tabId)
            && args.Arguments.TryGetValue("paneId", out var paneId))
        {
            OnNotificationActivated?.Invoke(tabId, paneId);
        }
    }

    /// <summary>
    /// Unregisters notification manager on app exit to clean up COM registration.
    /// Called from MainWindow.OnClosed.
    /// </summary>
    public static void UnregisterNotifications()
    {
        AppNotificationManager.Default.Unregister();
    }
}
