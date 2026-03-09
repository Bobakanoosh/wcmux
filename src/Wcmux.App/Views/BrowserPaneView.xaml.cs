using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Wcmux.App.Terminal;
using Windows.System;

namespace Wcmux.App.Views;

/// <summary>
/// Browser pane hosting a WebView2 control with address bar and navigation
/// controls. Uses the shared <see cref="WebViewEnvironmentCache"/> environment
/// so all browser panes share a single browser process group.
/// </summary>
public sealed partial class BrowserPaneView : UserControl
{
    private bool _initialized;

    /// <summary>The pane ID this view represents in the layout tree.</summary>
    public string? PaneId { get; set; }

    /// <summary>
    /// Fired when an app-level keyboard shortcut is detected inside the
    /// browser pane. The string is the command name (same pattern as
    /// <see cref="TerminalPaneView.CommandReceived"/>).
    /// </summary>
    public event Action<string>? CommandReceived;

    public BrowserPaneView()
    {
        InitializeComponent();

        // Intercept app-level shortcuts before WebView2 swallows them
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// Initializes the WebView2 control using the shared environment cache.
    /// Opens to about:blank with the address bar focused.
    /// </summary>
    public async Task InitializeWebViewAsync()
    {
        if (_initialized) return;

        var environment = await WebViewEnvironmentCache.GetOrCreateAsync();
        await BrowserWebView.EnsureCoreWebView2Async(environment);

        // Wire up navigation events
        BrowserWebView.CoreWebView2.SourceChanged += OnSourceChanged;
        BrowserWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

        // Disable browser accelerator keys so our app shortcuts take precedence
        BrowserWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

        _initialized = true;

        // Navigate to about:blank and focus address bar
        BrowserWebView.CoreWebView2.Navigate("about:blank");

        // Focus the address bar so users can type immediately
        AddressBox.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Focuses the browser pane. If initialized, focuses the WebView2 control.
    /// </summary>
    public void FocusBrowser()
    {
        if (_initialized)
        {
            BrowserWebView.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>
    /// Cleans up the WebView2 control and detaches event handlers.
    /// </summary>
    public async Task DetachAsync()
    {
        if (!_initialized) return;
        _initialized = false;

        PreviewKeyDown -= OnPreviewKeyDown;

        if (BrowserWebView.CoreWebView2 is not null)
        {
            BrowserWebView.CoreWebView2.SourceChanged -= OnSourceChanged;
            BrowserWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }

        BrowserWebView.Close();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Intercepts Ctrl+Shift shortcuts (split, close, focus, tab) before
    /// the WebView2 control processes them. Uses PreviewKeyDown which fires
    /// in the tunneling phase before the WebView2 handles the key.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                       .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (!ctrl || !shift) return;

        string? command = e.Key switch
        {
            VirtualKey.H => "split-horizontal",
            VirtualKey.V => "split-vertical",
            VirtualKey.W => "close-pane",
            VirtualKey.Left => "focus-left",
            VirtualKey.Right => "focus-right",
            VirtualKey.Up => "focus-up",
            VirtualKey.Down => "focus-down",
            VirtualKey.T => "new-tab",
            VirtualKey.Tab => "next-tab",
            _ => null,
        };

        if (command is not null)
        {
            e.Handled = true;
            CommandReceived?.Invoke(command);
        }
    }

    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            AddressBox.Text = sender.Source;
        });
    }

    private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            BackButton.IsEnabled = sender.CanGoBack;
            ForwardButton.IsEnabled = sender.CanGoForward;
        });
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (BrowserWebView.CoreWebView2?.CanGoBack == true)
            BrowserWebView.CoreWebView2.GoBack();
    }

    private void OnForwardClick(object sender, RoutedEventArgs e)
    {
        if (BrowserWebView.CoreWebView2?.CanGoForward == true)
            BrowserWebView.CoreWebView2.GoForward();
    }

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        BrowserWebView.CoreWebView2?.Reload();
    }

    private void OnGoClick(object sender, RoutedEventArgs e)
    {
        NavigateToAddressBar();
    }

    private void OnAddressBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            NavigateToAddressBar();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Navigates to the URL in the address bar. Prepends https:// if no
    /// scheme is present.
    /// </summary>
    private void NavigateToAddressBar()
    {
        var url = AddressBox.Text?.Trim();
        if (string.IsNullOrEmpty(url)) return;

        // Prepend https:// if no scheme
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "about"))
        {
            url = "https://" + url;
        }

        BrowserWebView.CoreWebView2?.Navigate(url);
        BrowserWebView.Focus(FocusState.Programmatic);
    }
}
