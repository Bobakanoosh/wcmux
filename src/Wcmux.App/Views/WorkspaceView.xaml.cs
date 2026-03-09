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
    private readonly Dictionary<string, TextBlock> _paneTitles = new();
    private readonly Dictionary<string, string> _paneSessionIds = new();
    private readonly Dictionary<string, DispatcherTimer> _blinkTimers = new();

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

        await RenderLayoutAsync();
    }

    /// <summary>
    /// Detaches from the view model and cleans up all pane views.
    /// </summary>
    public async Task DetachAsync()
    {
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
        _paneTitles.Clear();
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

        // Remove views for panes no longer in the tree
        var toRemove = _paneViews.Keys.Where(id => !allPaneIds.Contains(id)).ToList();
        foreach (var paneId in toRemove)
        {
            if (_paneViews.Remove(paneId, out var paneView))
            {
                await paneView.DetachAsync();
                RootContainer.Children.Remove(GetPaneContainer(paneView));
                _paneTitles.Remove(paneId);
                _paneSessionIds.Remove(paneId);
                StopBlinkTimer(paneId);
                _attentionStore?.RemovePane(paneId);
            }
        }

        // Create or update views for each pane
        foreach (var paneId in allPaneIds)
        {
            if (!_paneViews.ContainsKey(paneId))
            {
                await CreatePaneViewAsync(paneId);
            }

            // Position the pane view
            if (paneRects.TryGetValue(paneId, out var rect) && _paneViews.TryGetValue(paneId, out var view))
            {
                PositionPaneView(paneId, view, rect, paneId == activePaneId);
            }
        }
    }

    private async Task CreatePaneViewAsync(string paneId)
    {
        if (_viewModel is null) return;

        var session = _viewModel.GetSessionForPane(paneId);
        if (session is null) return;

        var paneView = new TerminalPaneView { PaneId = paneId };

        // Wrap in a border for active pane highlighting
        var border = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = _inactiveBorderBrush,
            Child = paneView,
            Tag = paneId,
        };

        // Add split affordance button — hidden by default, shown on hover
        var splitButton = new Button
        {
            Content = new SymbolIcon(Symbol.Add),
            Width = 28,
            Height = 28,
            Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 14, 4),
            Tag = paneId,
        };
        splitButton.Click += OnSplitAffordanceClick;

        // Pane title overlay showing current working directory
        var titleBlock = new TextBlock
        {
            Text = PathHelper.TruncateCwdFromLeft(session.LastKnownCwd ?? ""),
            FontSize = 11,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 200, 200, 200)),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
            Tag = paneId,
        };
        _paneTitles[paneId] = titleBlock;
        _paneSessionIds[paneId] = session.SessionId;

        var grid = new Grid { Tag = paneId };
        grid.Children.Add(border);
        grid.Children.Add(titleBlock);
        grid.Children.Add(splitButton);

        // Show/hide split button on hover
        grid.PointerEntered += (s, e) => splitButton.Opacity = 0.7;
        grid.PointerExited += (s, e) => splitButton.Opacity = 0;

        // Handle mouse click focus
        border.PointerPressed += (s, e) =>
        {
            _viewModel?.SetActivePane(paneId);
            e.Handled = false; // Let the event propagate to the terminal
        };

        // Route pane commands from the terminal surface to the view model.
        // Focus/resize commands operate on the current active pane (which may
        // differ from the source pane if WebView2 focus hasn't caught up yet).
        // Split/close commands activate the source pane first so they affect
        // the pane the user is typing in.
        paneView.CommandReceived += cmd => DispatcherQueue?.TryEnqueue(async () =>
        {
            if (IsPaneSpecificCommand(cmd) && paneView.PaneId is not null)
            {
                _viewModel?.SetActivePane(paneView.PaneId);
            }
            await HandlePaneCommandAsync(cmd);
        });

        RootContainer.Children.Add(grid);
        _paneViews[paneId] = paneView;

        // Attach the session after adding to visual tree
        await paneView.AttachAsync(_viewModel.SessionManager, session);
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
    }

    private void OnSessionEvent(object? sender, SessionEvent evt)
    {
        if (evt is SessionCwdChangedEvent cwdEvent)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                // Find the pane associated with this session
                foreach (var (paneId, sessionId) in _paneSessionIds)
                {
                    if (sessionId == cwdEvent.SessionId && _paneTitles.TryGetValue(paneId, out var titleBlock))
                    {
                        titleBlock.Text = PathHelper.TruncateCwdFromLeft(cwdEvent.WorkingDirectory);
                        break;
                    }
                }
            });
        }
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
        foreach (var (paneId, view) in _paneViews)
        {
            UpdatePaneVisualState(paneId);
        }
    }

    private void UpdatePaneVisualState(string paneId)
    {
        if (_viewModel is null) return;
        if (!_paneViews.TryGetValue(paneId, out var view)) return;

        var container = GetPaneContainer(view);
        if (container is not Grid grid || grid.Children.Count == 0 || grid.Children[0] is not Border border)
            return;

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

    private Grid? GetPaneContainer(TerminalPaneView view)
    {
        foreach (var child in RootContainer.Children)
        {
            if (child is Grid grid && grid.Children.Count > 0)
            {
                if (grid.Children[0] is Border border && border.Child == view)
                    return grid;
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

    private async void OnSplitAffordanceClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        if (sender is Button button && button.Tag is string paneId)
        {
            _viewModel.SetActivePane(paneId);
            await _viewModel.SplitActivePaneVerticalAsync();
        }
    }
}
