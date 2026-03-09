using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Wcmux.App.ViewModels;
using Wcmux.Core.Layout;
using Wcmux.Core.Runtime;

namespace Wcmux.App.Views;

/// <summary>
/// Renders the split tree by creating TerminalPaneView instances for each
/// leaf and positioning them according to the computed pane rectangles.
/// The view never defines layout state -- it renders the reducer-owned tree.
/// </summary>
public sealed partial class WorkspaceView : UserControl
{
    private WorkspaceViewModel? _viewModel;
    private AttentionStore? _attentionStore;
    private readonly Dictionary<string, TerminalPaneView> _paneViews = new();
    private readonly SolidColorBrush _activeBorderBrush = new(Windows.UI.Color.FromArgb(255, 60, 60, 60));
    private readonly SolidColorBrush _inactiveBorderBrush = new(Windows.UI.Color.FromArgb(255, 60, 60, 60));
    private readonly SolidColorBrush _attentionBorderBrush = new(Windows.UI.Color.FromArgb(255, 50, 130, 240));
    private readonly Dictionary<string, BrowserPaneView> _browserPaneViews = new();
    private readonly Dictionary<string, TextBlock> _paneProcessNames = new();
    private readonly Dictionary<string, string> _paneSessionIds = new();
    private readonly Dictionary<string, DispatcherTimer> _blinkTimers = new();
    private DispatcherTimer? _processNameTimer;

    /// <summary>Fired when a terminal requests a tab-level command (e.g., new-tab).</summary>
    public event Func<string, Task>? TabCommandReceived;

    public WorkspaceView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Attaches a workspace view model and renders the initial layout.
    /// </summary>
    public async Task AttachAsync(WorkspaceViewModel viewModel, AttentionStore? attentionStore = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _attentionStore = attentionStore;

        _viewModel.LayoutChanged += OnLayoutChanged;
        _viewModel.ActivePaneChanged += OnActivePaneChanged;
        _viewModel.SessionManager.SessionEventReceived += OnSessionEvent;

        if (_attentionStore is not null)
        {
            _attentionStore.AttentionChanged += OnAttentionChanged;
        }

        // Set initial container size
        if (RootContainer.ActualWidth > 0 && RootContainer.ActualHeight > 0)
        {
            _viewModel.UpdateContainerSize(RootContainer.ActualWidth, RootContainer.ActualHeight);
        }

        // Start the shared process name polling timer
        _processNameTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _processNameTimer.Tick += OnProcessNameTimerTick;
        _processNameTimer.Start();

        await RenderLayoutAsync();
    }

    /// <summary>
    /// Detaches from the view model and cleans up all pane views.
    /// </summary>
    public async Task DetachAsync()
    {
        // Stop the process name timer
        if (_processNameTimer is not null)
        {
            _processNameTimer.Stop();
            _processNameTimer.Tick -= OnProcessNameTimerTick;
            _processNameTimer = null;
        }

        if (_viewModel is not null)
        {
            _viewModel.LayoutChanged -= OnLayoutChanged;
            _viewModel.ActivePaneChanged -= OnActivePaneChanged;
            _viewModel.SessionManager.SessionEventReceived -= OnSessionEvent;
        }

        if (_attentionStore is not null)
        {
            _attentionStore.AttentionChanged -= OnAttentionChanged;
        }

        // Stop all blink timers
        foreach (var timer in _blinkTimers.Values)
        {
            timer.Stop();
        }
        _blinkTimers.Clear();

        foreach (var paneView in _paneViews.Values)
        {
            await paneView.DetachAsync();
        }
        _paneViews.Clear();

        foreach (var browserView in _browserPaneViews.Values)
        {
            await browserView.DetachAsync();
        }
        _browserPaneViews.Clear();

        _paneProcessNames.Clear();
        _paneSessionIds.Clear();
        RootContainer.Children.Clear();
        _viewModel = null;
        _attentionStore = null;
    }

    private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel?.UpdateContainerSize(e.NewSize.Width, e.NewSize.Height);
    }

    private void OnLayoutChanged()
    {
        if (DispatcherQueue is null) return;

        DispatcherQueue.TryEnqueue(async () =>
        {
            await RenderLayoutAsync();
        });
    }

    private void OnActivePaneChanged(string paneId)
    {
        DispatcherQueue?.TryEnqueue(async () =>
        {
            UpdateActivePaneHighlight(paneId);
            await FocusPaneAsync(paneId);
        });
    }

    /// <summary>
    /// Re-renders the split tree by reconciling existing pane views with
    /// the current layout state. Creates new views for new panes, removes
    /// views for closed panes, and repositions all views.
    /// </summary>
    private async Task RenderLayoutAsync()
    {
        if (_viewModel is null) return;

        var allPaneIds = _viewModel.LayoutStore.AllPaneIds;
        var paneRects = _viewModel.LayoutStore.PaneRects;
        var activePaneId = _viewModel.LayoutStore.ActivePaneId;

        // Remove terminal views for panes no longer in the tree
        var toRemove = _paneViews.Keys.Where(id => !allPaneIds.Contains(id)).ToList();
        foreach (var paneId in toRemove)
        {
            if (_paneViews.Remove(paneId, out var paneView))
            {
                await paneView.DetachAsync();
                RootContainer.Children.Remove(GetPaneContainer(paneView));
                _paneProcessNames.Remove(paneId);
                _paneSessionIds.Remove(paneId);
                StopBlinkTimer(paneId);
                _attentionStore?.RemovePane(paneId);
            }
        }

        // Remove browser views for panes no longer in the tree
        var browserToRemove = _browserPaneViews.Keys.Where(id => !allPaneIds.Contains(id)).ToList();
        foreach (var paneId in browserToRemove)
        {
            if (_browserPaneViews.Remove(paneId, out var browserView))
            {
                await browserView.DetachAsync();
                RootContainer.Children.Remove(GetBrowserPaneContainer(browserView));
                _paneProcessNames.Remove(paneId);
                StopBlinkTimer(paneId);
            }
        }

        // Create or update views for each pane
        foreach (var paneId in allPaneIds)
        {
            if (!_paneViews.ContainsKey(paneId) && !_browserPaneViews.ContainsKey(paneId))
            {
                await CreatePaneViewAsync(paneId);
            }

            // Position the pane view (terminal or browser)
            if (paneRects.TryGetValue(paneId, out var rect))
            {
                if (_paneViews.TryGetValue(paneId, out var view))
                {
                    PositionPaneView(paneId, view, rect, paneId == activePaneId);
                }
                else if (_browserPaneViews.TryGetValue(paneId, out var browserView))
                {
                    PositionBrowserPaneView(paneId, browserView, rect, paneId == activePaneId);
                }
            }
        }
    }

    private async Task CreatePaneViewAsync(string paneId)
    {
        if (_viewModel is null) return;

        // Check PaneKind to decide terminal vs browser
        var leafNode = _viewModel.LayoutStore.GetLeafNode(paneId);
        if (leafNode is null) return;

        if (leafNode.Kind == PaneKind.Browser)
        {
            await CreateBrowserPaneViewAsync(paneId);
        }
        else
        {
            await CreateTerminalPaneViewAsync(paneId);
        }
    }

    private async Task CreateTerminalPaneViewAsync(string paneId)
    {
        if (_viewModel is null) return;

        var session = _viewModel.GetSessionForPane(paneId);
        if (session is null) return;

        var paneView = new TerminalPaneView { PaneId = paneId };

        // Build outer grid with title bar row (24px) + content row (star)
        var outerGrid = new Grid { Tag = paneId };
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Title bar
        var titleBar = CreatePaneTitleBar(paneId, session);
        Grid.SetRow(titleBar, 0);

        // Content: border + terminal view
        var border = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = _inactiveBorderBrush,
            Child = paneView,
            Tag = paneId,
        };
        Grid.SetRow(border, 1);

        outerGrid.Children.Add(titleBar);
        outerGrid.Children.Add(border);

        // Handle mouse click focus on the content area
        border.PointerPressed += (s, e) =>
        {
            _viewModel?.SetActivePane(paneId);
            e.Handled = false; // Let the event propagate to the terminal
        };

        // Route pane commands from the terminal surface to the view model.
        paneView.CommandReceived += cmd => DispatcherQueue?.TryEnqueue(async () =>
        {
            if (IsPaneSpecificCommand(cmd) && paneView.PaneId is not null)
            {
                _viewModel?.SetActivePane(paneView.PaneId);
            }
            await HandlePaneCommandAsync(cmd);
        });

        _paneSessionIds[paneId] = session.SessionId;
        RootContainer.Children.Add(outerGrid);
        _paneViews[paneId] = paneView;

        // Attach the session after adding to visual tree
        await paneView.AttachAsync(_viewModel.SessionManager, session);
    }

    private async Task CreateBrowserPaneViewAsync(string paneId)
    {
        if (_viewModel is null) return;

        var browserView = new BrowserPaneView { PaneId = paneId };

        // Build outer grid with title bar row (24px) + content row (star)
        var outerGrid = new Grid { Tag = paneId };
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Title bar for browser pane (shows "browser" as process name)
        var titleBar = CreateBrowserPaneTitleBar(paneId);
        Grid.SetRow(titleBar, 0);

        // Content: border + browser view
        var border = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = _inactiveBorderBrush,
            Child = browserView,
            Tag = paneId,
        };
        Grid.SetRow(border, 1);

        outerGrid.Children.Add(titleBar);
        outerGrid.Children.Add(border);

        // Handle focus on the title bar click
        titleBar.PointerPressed += (s, e) =>
        {
            _viewModel?.SetActivePane(paneId);
            e.Handled = false;
        };

        // WebView2 swallows pointer events, so use GotFocus to detect when the browser pane gets focus
        browserView.GotFocus += (s, e) =>
        {
            _viewModel?.SetActivePane(paneId);
        };

        // Route app commands from the browser pane
        browserView.CommandReceived += cmd => DispatcherQueue?.TryEnqueue(async () =>
        {
            if (IsPaneSpecificCommand(cmd) && browserView.PaneId is not null)
            {
                _viewModel?.SetActivePane(browserView.PaneId);
            }
            await HandlePaneCommandAsync(cmd);
        });

        RootContainer.Children.Add(outerGrid);
        _browserPaneViews[paneId] = browserView;

        // Initialize the WebView2 after adding to visual tree
        await browserView.InitializeWebViewAsync();
    }

    /// <summary>
    /// Creates the title bar Grid for a pane with process name, split buttons, and close button.
    /// </summary>
    private Grid CreatePaneTitleBar(string paneId, ISession session)
    {
        var titleBarBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 37, 37)); // #252525
        var textBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204)); // #CCCCCC
        var buttonBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204));
        var transparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        var titleBar = new Grid
        {
            Background = titleBarBrush,
            Padding = new Thickness(8, 0, 4, 0),
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Process name text (left side)
        var initialName = Path.GetFileNameWithoutExtension(session.LaunchSpec.ExecutablePath);
        var processNameBlock = new TextBlock
        {
            Text = initialName ?? "shell",
            FontSize = 12,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false,
        };
        Grid.SetColumn(processNameBlock, 0);
        _paneProcessNames[paneId] = processNameBlock;

        // Button panel (right side)
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
        };
        Grid.SetColumn(buttonPanel, 1);

        // Split Horizontal button
        var splitHButton = CreateTitleBarButton("\uE745", buttonBrush, transparentBrush);
        splitHButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            _viewModel.SetActivePane(paneId);
            await _viewModel.SplitActivePaneHorizontalAsync();
        };

        // Split Vertical button
        var splitVButton = CreateTitleBarButton("\uE746", buttonBrush, transparentBrush);
        splitVButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            _viewModel.SetActivePane(paneId);
            await _viewModel.SplitActivePaneVerticalAsync();
        };

        // Close button
        var closeButton = CreateTitleBarButton("\uE711", buttonBrush, transparentBrush);
        closeButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            await _viewModel.ClosePaneAsync(paneId);
        };

        // Browser button (globe icon)
        var browserButton = CreateTitleBarButton("\uE774", buttonBrush, transparentBrush);
        browserButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            _viewModel.SetActivePane(paneId);
            await _viewModel.SplitActivePaneAsBrowserAsync(SplitAxis.Vertical);
        };

        buttonPanel.Children.Add(splitHButton);
        buttonPanel.Children.Add(splitVButton);
        buttonPanel.Children.Add(browserButton);
        buttonPanel.Children.Add(closeButton);

        titleBar.Children.Add(processNameBlock);
        titleBar.Children.Add(buttonPanel);

        return titleBar;
    }

    /// <summary>
    /// Creates the title bar Grid for a browser pane showing "browser" as process name.
    /// </summary>
    private Grid CreateBrowserPaneTitleBar(string paneId)
    {
        var titleBarBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 37, 37)); // #252525
        var textBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204)); // #CCCCCC
        var buttonBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204));
        var transparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        var titleBar = new Grid
        {
            Background = titleBarBrush,
            Padding = new Thickness(8, 0, 4, 0),
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Process name text (shows "browser" for browser panes)
        var processNameBlock = new TextBlock
        {
            Text = "browser",
            FontSize = 12,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false,
        };
        Grid.SetColumn(processNameBlock, 0);
        _paneProcessNames[paneId] = processNameBlock;

        // Button panel (right side)
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
        };
        Grid.SetColumn(buttonPanel, 1);

        // Split Horizontal button
        var splitHButton = CreateTitleBarButton("\uE745", buttonBrush, transparentBrush);
        splitHButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            _viewModel.SetActivePane(paneId);
            await _viewModel.SplitActivePaneHorizontalAsync();
        };

        // Split Vertical button
        var splitVButton = CreateTitleBarButton("\uE746", buttonBrush, transparentBrush);
        splitVButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            _viewModel.SetActivePane(paneId);
            await _viewModel.SplitActivePaneVerticalAsync();
        };

        // Browser button (globe icon)
        var browserButton = CreateTitleBarButton("\uE774", buttonBrush, transparentBrush);
        browserButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            _viewModel.SetActivePane(paneId);
            await _viewModel.SplitActivePaneAsBrowserAsync(SplitAxis.Vertical);
        };

        // Close button
        var closeButton = CreateTitleBarButton("\uE711", buttonBrush, transparentBrush);
        closeButton.Click += async (s, e) =>
        {
            if (_viewModel is null) return;
            await _viewModel.ClosePaneAsync(paneId);
        };

        buttonPanel.Children.Add(splitHButton);
        buttonPanel.Children.Add(splitVButton);
        buttonPanel.Children.Add(browserButton);
        buttonPanel.Children.Add(closeButton);

        titleBar.Children.Add(processNameBlock);
        titleBar.Children.Add(buttonPanel);

        return titleBar;
    }

    /// <summary>
    /// Creates a small icon button for the title bar.
    /// </summary>
    private static Button CreateTitleBarButton(string glyph, SolidColorBrush foreground, SolidColorBrush background)
    {
        return new Button
        {
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = 12,
                Foreground = foreground,
            },
            Width = 24,
            Height = 20,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Background = background,
            BorderThickness = new Thickness(0),
        };
    }

    /// <summary>
    /// Single shared timer that polls foreground process names for all panes.
    /// </summary>
    private void OnProcessNameTimerTick(object? sender, object e)
    {
        if (_viewModel is null) return;

        foreach (var (paneId, processNameBlock) in _paneProcessNames)
        {
            // Skip browser panes -- they show a static "browser" label
            if (_browserPaneViews.ContainsKey(paneId)) continue;

            var session = _viewModel.GetSessionForPane(paneId);
            if (session is null || !session.IsRunning) continue;

            var name = ForegroundProcessDetector.GetForegroundProcessName(session.ProcessId);
            if (name is null)
            {
                // Fall back to the executable name from launch spec
                name = Path.GetFileNameWithoutExtension(session.LaunchSpec.ExecutablePath);
            }

            if (name is not null)
            {
                processNameBlock.Text = name;
            }
        }
    }

    private void PositionPaneView(string paneId, TerminalPaneView view, PaneRect rect, bool isActive)
    {
        var container = GetPaneContainer(view);
        if (container is null) return;

        // Use Canvas-style positioning via Grid margin
        container.Margin = new Thickness(rect.X, rect.Y, 0, 0);
        container.Width = Math.Max(0, rect.Width);
        container.Height = Math.Max(0, rect.Height);
        container.HorizontalAlignment = HorizontalAlignment.Left;
        container.VerticalAlignment = VerticalAlignment.Top;

        // Update visual state (border, opacity, attention)
        UpdatePaneVisualState(paneId);
    }

    private void PositionBrowserPaneView(string paneId, BrowserPaneView view, PaneRect rect, bool isActive)
    {
        var container = GetBrowserPaneContainer(view);
        if (container is null) return;

        container.Margin = new Thickness(rect.X, rect.Y, 0, 0);
        container.Width = Math.Max(0, rect.Width);
        container.Height = Math.Max(0, rect.Height);
        container.HorizontalAlignment = HorizontalAlignment.Left;
        container.VerticalAlignment = VerticalAlignment.Top;

        // Update visual state (border, opacity)
        UpdateBrowserPaneVisualState(paneId);
    }

    /// <summary>
    /// Finds the outer Grid container for a browser pane view.
    /// </summary>
    private Grid? GetBrowserPaneContainer(BrowserPaneView view)
    {
        foreach (var child in RootContainer.Children)
        {
            if (child is Grid grid)
            {
                foreach (var gridChild in grid.Children)
                {
                    if (gridChild is Border border && border.Child == view)
                        return grid;
                }
            }
        }
        return null;
    }

    private void UpdateBrowserPaneVisualState(string paneId)
    {
        if (_viewModel is null) return;
        if (!_browserPaneViews.TryGetValue(paneId, out var view)) return;

        var container = GetBrowserPaneContainer(view);
        if (container is not Grid outerGrid || outerGrid.Children.Count < 2) return;

        Border? border = null;
        foreach (var child in outerGrid.Children)
        {
            if (child is Border b && Grid.GetRow(b) == 1)
            {
                border = b;
                break;
            }
        }
        if (border is null) return;

        var isActive = paneId == _viewModel.LayoutStore.ActivePaneId;

        if (isActive)
        {
            container.Opacity = 1.0;
            border.BorderBrush = _activeBorderBrush;
        }
        else
        {
            container.Opacity = 0.5;
            border.BorderBrush = _inactiveBorderBrush;
        }
    }

    /// <summary>
    /// Returns the most recent plain-text output lines for a specific pane,
    /// used by the sidebar for preview text display.
    /// </summary>
    public string[] GetPreviewText(string paneId, int lineCount)
    {
        if (_paneViews.TryGetValue(paneId, out var paneView) && paneView.Bridge is not null)
        {
            return paneView.Bridge.GetRecentLines(lineCount);
        }
        return Array.Empty<string>();
    }

    /// <summary>
    /// Focuses the terminal WebView for the specified pane. Called externally
    /// during tab switching to restore focus to the active pane.
    /// </summary>
    public async Task FocusPaneAsync(string paneId)
    {
        if (_paneViews.TryGetValue(paneId, out var paneView))
        {
            await paneView.FocusTerminalAsync();
        }
        else if (_browserPaneViews.TryGetValue(paneId, out var browserView))
        {
            browserView.FocusBrowser();
            await Task.CompletedTask;
        }
    }

    private void OnSessionEvent(object? sender, SessionEvent evt)
    {
        // Process name updates are handled by the shared timer now.
        // SessionCwdChangedEvent no longer drives the title display.
    }

    private void OnAttentionChanged(string paneId)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (_viewModel is null || _attentionStore is null) return;
            UpdatePaneVisualState(paneId);
        });
    }

    private void UpdateActivePaneHighlight(string activePaneId)
    {
        foreach (var (paneId, _) in _paneViews)
        {
            UpdatePaneVisualState(paneId);
        }
        foreach (var (paneId, _) in _browserPaneViews)
        {
            UpdateBrowserPaneVisualState(paneId);
        }
    }

    private void UpdatePaneVisualState(string paneId)
    {
        if (_viewModel is null) return;
        if (!_paneViews.TryGetValue(paneId, out var view)) return;

        var container = GetPaneContainer(view);
        if (container is not Grid outerGrid || outerGrid.Children.Count < 2)
            return;

        // Border is in row 1 (second child added)
        Border? border = null;
        foreach (var child in outerGrid.Children)
        {
            if (child is Border b && Grid.GetRow(b) == 1)
            {
                border = b;
                break;
            }
        }
        if (border is null) return;

        var isActive = paneId == _viewModel.LayoutStore.ActivePaneId;
        var hasAttention = _attentionStore?.HasAttention(paneId) ?? false;

        if (isActive)
        {
            // Active pane: full opacity, active border, stop blink
            container.Opacity = 1.0;
            border.BorderBrush = _activeBorderBrush;
            StopBlinkTimer(paneId);
        }
        else if (hasAttention)
        {
            // Inactive pane with attention: dimmed, blinking blue border
            container.Opacity = 0.5;
            StartBlinkAnimation(paneId, border);
        }
        else
        {
            // Inactive pane without attention: dimmed, gray border
            container.Opacity = 0.5;
            border.BorderBrush = _inactiveBorderBrush;
            StopBlinkTimer(paneId);
        }
    }

    private void StartBlinkAnimation(string paneId, Border border)
    {
        // Don't restart if already blinking
        if (_blinkTimers.ContainsKey(paneId)) return;

        var transparent = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        int toggleCount = 0;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (s, e) =>
        {
            toggleCount++;
            if (toggleCount >= 8) // 4 full blinks (on/off pairs)
            {
                // Settle to steady attention color
                border.BorderBrush = _attentionBorderBrush;
                timer.Stop();
                _blinkTimers.Remove(paneId);
                return;
            }

            border.BorderBrush = (toggleCount % 2 == 0)
                ? _attentionBorderBrush
                : transparent;
        };

        // Start with attention color visible
        border.BorderBrush = _attentionBorderBrush;
        _blinkTimers[paneId] = timer;
        timer.Start();
    }

    private void StopBlinkTimer(string paneId)
    {
        if (_blinkTimers.Remove(paneId, out var timer))
        {
            timer.Stop();
        }
    }

    /// <summary>
    /// Finds the outer Grid container for a pane view.
    /// The structure is: outerGrid { Row0: titleBar, Row1: Border { TerminalPaneView } }
    /// </summary>
    private Grid? GetPaneContainer(TerminalPaneView view)
    {
        foreach (var child in RootContainer.Children)
        {
            if (child is Grid grid)
            {
                // Find the Border in row 1 that contains the pane view
                foreach (var gridChild in grid.Children)
                {
                    if (gridChild is Border border && border.Child == view)
                        return grid;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Returns true for commands that should activate the source pane first
    /// (split, close). Focus/resize commands operate on the current active pane.
    /// </summary>
    private static bool IsPaneSpecificCommand(string command)
        => command is "split-horizontal" or "split-vertical" or "close-pane" or "focus-pane";

    private async Task HandlePaneCommandAsync(string command)
    {
        if (_viewModel is null) return;

        switch (command)
        {
            case "split-horizontal":
                await _viewModel.SplitActivePaneHorizontalAsync();
                break;
            case "split-vertical":
                await _viewModel.SplitActivePaneVerticalAsync();
                break;
            case "close-pane":
                await _viewModel.CloseActivePaneAsync();
                break;
            case "focus-left":
                _viewModel.FocusLeft();
                break;
            case "focus-right":
                _viewModel.FocusRight();
                break;
            case "focus-up":
                _viewModel.FocusUp();
                break;
            case "focus-down":
                _viewModel.FocusDown();
                break;
            case "resize-left":
                _viewModel.ResizeActivePane(Direction.Left);
                break;
            case "resize-right":
                _viewModel.ResizeActivePane(Direction.Right);
                break;
            case "resize-up":
                _viewModel.ResizeActivePane(Direction.Up);
                break;
            case "resize-down":
                _viewModel.ResizeActivePane(Direction.Down);
                break;
            default:
                // Tab-level commands (new-tab, next-tab, prev-tab, tab-N)
                if (command.StartsWith("new-tab") || command.StartsWith("next-tab")
                    || command.StartsWith("prev-tab") || command.StartsWith("tab-"))
                {
                    if (TabCommandReceived is not null)
                        await TabCommandReceived(command);
                }
                break;
        }
    }
}
