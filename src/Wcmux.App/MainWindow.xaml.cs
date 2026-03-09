using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Wcmux.App.Commands;
using Wcmux.App.Notifications;
using Wcmux.App.ViewModels;
using Wcmux.App.Views;
using Wcmux.Core.Runtime;
using Windows.Graphics;
using Windows.UI;

namespace Wcmux.App;

public sealed partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;
    private readonly AttentionStore _attentionStore = new();
    private TabViewModel? _tabViewModel;
    private readonly Dictionary<string, WorkspaceView> _tabViews = new();
    private string? _currentVisibleTabId;

    private bool _isWindowFocused = true;
    private NotificationService? _notificationService;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();

        // Custom title bar setup -- order matters: ExtendsContentIntoTitleBar MUST be set before PreferredHeightOption
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;

        // Caption button dark-theme colors
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(255, 204, 204, 204);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 45, 45, 45);
        AppWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 55, 55, 55);
        AppWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 100, 100, 100);

        // Subscribe to title bar region recalculation events
        AppTitleBar.Loaded += (_, _) => SetRegionsForCustomTitleBar();
        AppTitleBar.SizeChanged += (_, _) => SetRegionsForCustomTitleBar();

        _sessionManager = new SessionManager();
        _sessionManager.SessionEventReceived += OnGlobalSessionEvent;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    /// <summary>Exposed for testing.</summary>
    internal TabViewModel? TabViewModel => _tabViewModel;

    /// <summary>
    /// Configures InputNonClientPointerSource passthrough regions for the custom title bar.
    /// Currently no interactive elements exist in the title bar, so no passthrough regions
    /// are needed. This scaffold is ready for future phases to register passthrough regions
    /// for buttons or controls added to the title bar.
    /// </summary>
    private void SetRegionsForCustomTitleBar()
    {
        if (AppTitleBar.XamlRoot is null) return;

        var scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

        var nonClientSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        nonClientSource.SetRegionRects(NonClientRegionKind.Passthrough, Array.Empty<RectInt32>());
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // One-time initialization on first activation
        if (!_initialized)
        {
            _initialized = true;
            await InitializeAsync();
        }

        // Track window focus state for notification gating
        var wasFocused = _isWindowFocused;
        _isWindowFocused = args.WindowActivationState != WindowActivationState.Deactivated;
        System.Diagnostics.Debug.WriteLine(
            $"[wcmux] Window activation: state={args.WindowActivationState}, focused={_isWindowFocused}");

        // On regain focus (transition from unfocused to focused): dismiss pending toasts
        if (_isWindowFocused && !wasFocused && _notificationService is not null)
        {
            _ = _notificationService.DismissAllAsync();
        }
    }

    private async Task InitializeAsync()
    {
        // Create NotificationService with the window handle for FlashWindowEx
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _notificationService = new NotificationService(hwnd);

        // Subscribe to toast activation deep-link callback
        App.OnNotificationActivated += OnToastActivated;

        // Subscribe to attention events for toast/flash when window is unfocused
        _attentionStore.AttentionChanged += OnAttentionChangedForNotification;

        await CreateInitialTabAsync();
    }

    private void OnAttentionChangedForNotification(string paneId)
    {
        var hasAtt = _attentionStore.HasAttention(paneId);
        System.Diagnostics.Debug.WriteLine(
            $"[wcmux] AttentionChanged: pane={paneId}, hasAttention={hasAtt}, windowFocused={_isWindowFocused}");

        // Notifications are now fired directly from OnGlobalSessionEvent to
        // cover both active-pane and inactive-pane bells when the window is
        // unfocused. This handler only logs for diagnostics.
    }

    private void OnToastActivated(string tabId, string paneId)
    {
        // Marshal to UI thread -- NotificationInvoked fires from background thread
        DispatcherQueue?.TryEnqueue(async () =>
        {
            if (_tabViewModel is null) return;

            // Activate the window (bring to foreground)
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetForegroundWindowHelper(hwnd);
            Activate();

            // Switch to the target tab
            _tabViewModel.SwitchTab(tabId);

            // Focus the target pane
            if (_tabViews.TryGetValue(tabId, out var workspaceView))
            {
                var workspace = _tabViewModel.GetWorkspace(tabId);
                workspace?.SetActivePane(paneId);
                await workspaceView.FocusPaneAsync(paneId);
            }
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(
        System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static void SetForegroundWindowHelper(IntPtr hwnd)
    {
        SetForegroundWindow(hwnd);
    }

    internal async Task CreateInitialTabAsync()
    {
        try
        {
            _tabViewModel = new TabViewModel(_sessionManager);

            _tabViewModel.LastTabClosed += () =>
            {
                DispatcherQueue?.TryEnqueue(Close);
            };

            _tabViewModel.TabStore.ActiveTabChanged += OnActiveTabChanged;
            _tabViewModel.TabStore.TabsChanged += OnTabsChanged;

            // Attach tab sidebar with attention store and tab views
            TabSidebar.Attach(_tabViewModel, _attentionStore, _tabViews);
            TabSidebar.NewTabRequested += () => CreateNewTabWithViewAsync();

            // Attach tab command bindings (persistent, not re-attached on tab switch)
            if (Content is UIElement rootElement)
            {
                TabCommandBindings.Attach(rootElement, _tabViewModel,
                    () => CreateNewTabWithViewAsync());
            }

            // Create first tab
            var tabId = await _tabViewModel.CreateNewTabAsync();
            await CreateWorkspaceViewForTabAsync(tabId);

            // Attach pane command bindings for the initial tab
            if (Content is UIElement root && _tabViewModel.ActiveWorkspace is not null)
            {
                PaneCommandBindings.Attach(root, _tabViewModel.ActiveWorkspace);
            }
        }
        catch (Exception ex)
        {
            Title = $"wcmux - Session failed: {ex.Message}";
        }
    }

    private async Task CreateNewTabWithViewAsync()
    {
        if (_tabViewModel is null) return;
        var tabId = await _tabViewModel.CreateNewTabAsync();
        await CreateWorkspaceViewForTabAsync(tabId);
    }

    private async Task CreateWorkspaceViewForTabAsync(string tabId)
    {
        if (_tabViewModel is null) return;

        var workspace = _tabViewModel.GetWorkspace(tabId);
        if (workspace is null) return;

        var view = new WorkspaceView();
        view.TabCommandReceived += async cmd =>
        {
            if (_tabViewModel is null) return;
            switch (cmd)
            {
                case "new-tab":
                    await CreateNewTabWithViewAsync();
                    break;
                case "next-tab":
                    SwitchToRelativeTab(1);
                    break;
                case "prev-tab":
                    SwitchToRelativeTab(-1);
                    break;
                default:
                    // tab-1 through tab-9
                    if (cmd.StartsWith("tab-") && int.TryParse(cmd[4..], out var index))
                        SwitchToTabByIndex(index);
                    break;
            }
        };
        _tabViews[tabId] = view;
        TabContentArea.Children.Add(view);

        // Wire attention clearing when pane gets focus
        workspace.ActivePaneChanged += paneId =>
        {
            _attentionStore.ClearAttention(paneId);
        };

        // Only show the active tab's view
        if (tabId == _tabViewModel.TabStore.ActiveTabId)
        {
            view.Visibility = Visibility.Visible;
            _currentVisibleTabId = tabId;
            await view.AttachAsync(workspace, _attentionStore);
        }
        else
        {
            view.Visibility = Visibility.Collapsed;
        }
    }

    private void OnTabsChanged()
    {
        DispatcherQueue?.TryEnqueue(async () =>
        {
            if (_tabViewModel is null) return;

            // Remove WorkspaceViews for tabs that no longer exist
            var existingTabIds = _tabViewModel.TabStore.TabOrder.ToHashSet();
            var staleTabIds = _tabViews.Keys.Where(id => !existingTabIds.Contains(id)).ToList();

            foreach (var tabId in staleTabIds)
            {
                if (_tabViews.Remove(tabId, out var view))
                {
                    await view.DetachAsync();
                    TabContentArea.Children.Remove(view);
                }
            }
        });
    }

    private void OnActiveTabChanged(string newTabId)
    {
        DispatcherQueue?.TryEnqueue(async () =>
        {
            // Hide current tab view
            if (_currentVisibleTabId is not null && _tabViews.TryGetValue(_currentVisibleTabId, out var oldView))
            {
                oldView.Visibility = Visibility.Collapsed;
            }

            // Show new tab view
            if (_tabViews.TryGetValue(newTabId, out var newView))
            {
                newView.Visibility = Visibility.Visible;
                _currentVisibleTabId = newTabId;

                // Re-attach pane command bindings for the new active workspace
                if (Content is UIElement root && _tabViewModel?.ActiveWorkspace is not null)
                {
                    PaneCommandBindings.Detach(root);
                    PaneCommandBindings.Attach(root, _tabViewModel.ActiveWorkspace);
                    // Re-attach tab command bindings that were cleared by Detach
                    TabCommandBindings.Attach(root, _tabViewModel,
                        () => CreateNewTabWithViewAsync());
                }

                // Update container size for the newly visible workspace
                _tabViewModel?.ActiveWorkspace?.UpdateContainerSize(
                    newView.ActualWidth, newView.ActualHeight);

                // Focus the active pane's terminal
                var activePaneId = _tabViewModel?.ActiveWorkspace?.LayoutStore.ActivePaneId;
                if (activePaneId is not null)
                {
                    await newView.FocusPaneAsync(activePaneId);
                }
            }
        });
    }

    private void SwitchToRelativeTab(int offset)
    {
        if (_tabViewModel is null) return;
        var order = _tabViewModel.TabStore.TabOrder;
        if (order.Count <= 1) return;
        var activeId = _tabViewModel.TabStore.ActiveTabId;
        if (activeId is null) return;
        var idx = ((IList<string>)order).IndexOf(activeId);
        if (idx < 0) return;
        var next = (idx + offset + order.Count) % order.Count;
        _tabViewModel.SwitchTab(order[next]);
    }

    private void SwitchToTabByIndex(int oneBasedIndex)
    {
        if (_tabViewModel is null) return;
        var order = _tabViewModel.TabStore.TabOrder;
        if (order.Count == 0) return;
        if (oneBasedIndex == 9)
        {
            _tabViewModel.SwitchTab(order[^1]);
            return;
        }
        var idx = oneBasedIndex - 1;
        if (idx < order.Count)
            _tabViewModel.SwitchTab(order[idx]);
    }

    private void OnGlobalSessionEvent(object? sender, SessionEvent evt)
    {
        if (evt is SessionBellEvent bellEvt && _tabViewModel is not null)
        {
            // Find the pane that owns this session
            var activeWorkspace = _tabViewModel.ActiveWorkspace;
            var activePaneId = activeWorkspace?.LayoutStore.ActivePaneId ?? "";

            // Search all workspaces for the pane owning this session
            foreach (var tabId in _tabViewModel.TabStore.TabOrder)
            {
                var workspace = _tabViewModel.GetWorkspace(tabId);
                if (workspace is null) continue;

                foreach (var paneId in workspace.LayoutStore.AllPaneIds)
                {
                    if (workspace.GetSessionIdForPane(paneId) == bellEvt.SessionId)
                    {
                        // RaiseBell handles per-pane attention visuals (suppresses
                        // for the focused pane, which is correct for in-app indicators).
                        _attentionStore.RaiseBell(paneId, activePaneId, DateTimeOffset.UtcNow);

                        // Window-level notifications should fire even for the active
                        // pane when the window itself is unfocused (e.g., Claude Code
                        // waiting for input while the user is in another app).
                        if (!_isWindowFocused && _notificationService is not null)
                        {
                            var tab = _tabViewModel.TabStore.GetTab(tabId);
                            var tabLabel = tab?.Label ?? "Unknown";
                            var session = workspace.GetSessionForPane(paneId);
                            var paneTitle = session?.LastKnownCwd ?? "";

                            _notificationService.ShowAttentionToast(tabId, tabLabel, paneId, paneTitle);
                            _notificationService.FlashTaskbar();
                        }
                        return;
                    }
                }
            }
        }
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        // Unsubscribe from notification activation
        App.OnNotificationActivated -= OnToastActivated;
        _attentionStore.AttentionChanged -= OnAttentionChangedForNotification;

        // Unregister notifications
        App.UnregisterNotifications();

        foreach (var view in _tabViews.Values)
        {
            await view.DetachAsync();
        }
        _tabViews.Clear();

        if (_tabViewModel is not null)
        {
            await _tabViewModel.DisposeAsync();
        }

        await _sessionManager.DisposeAsync();
    }
}
