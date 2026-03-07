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
    private readonly Dictionary<string, TerminalPaneView> _paneViews = new();
    private readonly SolidColorBrush _activeBorderBrush = new(Windows.UI.Color.FromArgb(255, 0, 122, 204));
    private readonly SolidColorBrush _inactiveBorderBrush = new(Windows.UI.Color.FromArgb(255, 60, 60, 60));

    public WorkspaceView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Attaches a workspace view model and renders the initial layout.
    /// </summary>
    public async Task AttachAsync(WorkspaceViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _viewModel.LayoutChanged += OnLayoutChanged;
        _viewModel.ActivePaneChanged += OnActivePaneChanged;

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
        }

        foreach (var paneView in _paneViews.Values)
        {
            await paneView.DetachAsync();
        }
        _paneViews.Clear();
        RootContainer.Children.Clear();
        _viewModel = null;
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
        DispatcherQueue?.TryEnqueue(() =>
        {
            UpdateActivePaneHighlight(paneId);
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

        var paneView = new TerminalPaneView();

        // Wrap in a border for active pane highlighting
        var border = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = _inactiveBorderBrush,
            Child = paneView,
            Tag = paneId,
        };

        // Add split affordance button (top-right corner)
        var splitButton = new Button
        {
            Content = new SymbolIcon(Symbol.Add),
            Width = 28,
            Height = 28,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 4, 0),
            Tag = paneId,
        };
        splitButton.Click += OnSplitAffordanceClick;

        var grid = new Grid { Tag = paneId };
        grid.Children.Add(border);
        grid.Children.Add(splitButton);

        // Handle mouse click focus
        border.PointerPressed += (s, e) =>
        {
            _viewModel?.SetActivePane(paneId);
            e.Handled = false; // Let the event propagate to the terminal
        };

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

        // Update border highlight
        if (container is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Border border)
        {
            border.BorderBrush = isActive ? _activeBorderBrush : _inactiveBorderBrush;
        }
    }

    private void UpdateActivePaneHighlight(string activePaneId)
    {
        foreach (var (paneId, view) in _paneViews)
        {
            var container = GetPaneContainer(view);
            if (container is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Border border)
            {
                border.BorderBrush = paneId == activePaneId
                    ? _activeBorderBrush
                    : _inactiveBorderBrush;
            }
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
