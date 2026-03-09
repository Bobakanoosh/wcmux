using System.Runtime.InteropServices;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Wcmux.App.Notifications;

/// <summary>
/// Manages Windows toast notifications and taskbar flashing for attention events.
/// Creates toasts with deep-link arguments for tab/pane navigation on click.
/// </summary>
public sealed class NotificationService
{
    private readonly IntPtr _hwnd;

    // FlashWindowEx P/Invoke
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 3;          // Flash caption + taskbar
    private const uint FLASHW_TIMERNOFG = 12;   // Flash until window comes to foreground

    public NotificationService(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>
    /// Shows a toast notification with deep-link arguments for the specified tab and pane.
    /// Tag and Group are set for targeted removal.
    /// </summary>
    public void ShowAttentionToast(string tabId, string tabLabel, string paneId, string paneTitle)
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddArgument("action", "focusPane")
                .AddArgument("tabId", tabId)
                .AddArgument("paneId", paneId)
                .AddText($"wcmux -- Tab: {tabLabel}")
                .AddText(paneTitle)
                .BuildNotification();

            notification.Tag = paneId;
            notification.Group = tabId;

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[wcmux] Toast notification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Flashes the taskbar icon until the window receives foreground focus.
    /// Safe to call from any thread.
    /// </summary>
    public void FlashTaskbar()
    {
        var fi = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = _hwnd,
            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount = 0,
            dwTimeout = 0,
        };
        FlashWindowEx(ref fi);
    }

    /// <summary>
    /// Dismisses all pending toast notifications from Action Center.
    /// </summary>
    public async Task DismissAllAsync()
    {
        try
        {
            await AppNotificationManager.Default.RemoveAllAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[wcmux] Toast dismiss failed: {ex.Message}");
        }
    }
}
