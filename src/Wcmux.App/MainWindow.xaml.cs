using Microsoft.UI.Xaml;
using Wcmux.App.Commands;
using Wcmux.App.ViewModels;
using Wcmux.App.Views;
using Wcmux.Core.Runtime;

namespace Wcmux.App;

public sealed partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;
    private TabViewModel? _tabViewModel;
    private readonly Dictionary<string, WorkspaceView> _tabViews = new();
    private string? _currentVisibleTabId;

    public MainWindow()
    {
        InitializeComponent();
        _sessionManager = new SessionManager();
        Activated += OnActivated;
        Closed += OnClosed;
    }

    /// <summary>Exposed for testing.</summary>
    internal TabViewModel? TabViewModel => _tabViewModel;

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        await CreateInitialTabAsync();
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

            // Attach tab bar
            TabBar.Attach(_tabViewModel);
            TabBar.NewTabRequested += () => CreateNewTabWithViewAsync();

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
        _tabViews[tabId] = view;
        TabContentArea.Children.Add(view);

        // Only show the active tab's view
        if (tabId == _tabViewModel.TabStore.ActiveTabId)
        {
            view.Visibility = Visibility.Visible;
            _currentVisibleTabId = tabId;
            await view.AttachAsync(workspace);
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

    private async void OnClosed(object sender, WindowEventArgs args)
    {
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
