using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Wcmux.App.ViewModels;
using Wcmux.Core.Runtime;

namespace Wcmux.App.Views;

/// <summary>
/// Vertical tab sidebar showing tab title, cwd, 2-line output preview,
/// and attention indicators. Replaces the horizontal TabBarView.
/// </summary>
public sealed partial class TabSidebarView : UserControl
{
    private TabViewModel? _viewModel;
    private AttentionStore? _attentionStore;
    private Dictionary<string, WorkspaceView>? _tabViews;
    private readonly SolidColorBrush _attentionForeground = new(Windows.UI.Color.FromArgb(255, 50, 130, 240));
    private readonly SolidColorBrush _defaultForeground = new(Windows.UI.Color.FromArgb(255, 204, 204, 204));
    private readonly SolidColorBrush _cwdForeground = new(Windows.UI.Color.FromArgb(255, 128, 128, 128));
    private readonly SolidColorBrush _activeBg = new(Windows.UI.Color.FromArgb(255, 45, 45, 45));
    private readonly SolidColorBrush _transparentBg = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    private readonly SolidColorBrush _closeButtonFg = new(Windows.UI.Color.FromArgb(255, 136, 136, 136));
    private readonly Dictionary<string, DispatcherTimer> _tabBlinkTimers = new();
    private DispatcherTimer? _refreshTimer;

    /// <summary>Fired when the user clicks the [+] button to request a new tab.</summary>
    public event Func<Task>? NewTabRequested;

    public TabSidebarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Attaches to a TabViewModel and subscribes to tab events.
    /// </summary>
    public void Attach(TabViewModel viewModel, AttentionStore? attentionStore, Dictionary<string, WorkspaceView> tabViews)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _attentionStore = attentionStore;
        _tabViews = tabViews;

        _viewModel.TabStore.TabsChanged += OnTabsChanged;
        _viewModel.TabStore.ActiveTabChanged += OnActiveTabChanged;

        if (_attentionStore is not null)
        {
            _attentionStore.AttentionChanged += OnAttentionChanged;
        }

        // Start 2-second preview refresh timer
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();

        RenderTabs();
    }

    /// <summary>
    /// Detaches from the view model and cleans up timers and subscriptions.
    /// </summary>
    public void Detach()
    {
        if (_refreshTimer is not null)
        {
            _refreshTimer.Stop();
            _refreshTimer.Tick -= OnRefreshTimerTick;
            _refreshTimer = null;
        }

        if (_viewModel is not null)
        {
            _viewModel.TabStore.TabsChanged -= OnTabsChanged;
            _viewModel.TabStore.ActiveTabChanged -= OnActiveTabChanged;
        }

        if (_attentionStore is not null)
        {
            _attentionStore.AttentionChanged -= OnAttentionChanged;
        }

        foreach (var timer in _tabBlinkTimers.Values)
        {
            timer.Stop();
        }
        _tabBlinkTimers.Clear();
    }

    private void OnTabsChanged() => DispatcherQueue?.TryEnqueue(RenderTabs);

    private void OnActiveTabChanged(string _) => DispatcherQueue?.TryEnqueue(RenderTabs);

    private void OnAttentionChanged(string _) => DispatcherQueue?.TryEnqueue(RenderTabs);

    private void OnRefreshTimerTick(object? sender, object e) => RenderTabs();

    private void OnAddTabClick(object sender, RoutedEventArgs e)
    {
        if (NewTabRequested is not null)
        {
            _ = NewTabRequested();
        }
    }

    private void RenderTabs()
    {
        if (_viewModel is null) return;

        // Stop all existing blink timers before re-rendering
        foreach (var timer in _tabBlinkTimers.Values)
        {
            timer.Stop();
        }
        _tabBlinkTimers.Clear();

        TabList.Children.Clear();

        var activeTabId = _viewModel.TabStore.ActiveTabId;

        foreach (var tabId in _viewModel.TabStore.TabOrder)
        {
            var tab = _viewModel.TabStore.GetTab(tabId);
            if (tab is null) continue;

            var isActive = tabId == activeTabId;

            // Check if this tab has any panes with attention
            var hasAttention = false;
            if (!isActive && _attentionStore is not null)
            {
                var workspace = _viewModel.GetWorkspace(tabId);
                if (workspace is not null)
                {
                    var paneIds = workspace.LayoutStore.AllPaneIds;
                    hasAttention = _attentionStore.TabHasAttention(paneIds);
                }
            }

            // Get cwd from the active pane
            var cwd = "";
            var workspace2 = _viewModel.GetWorkspace(tabId);
            if (workspace2 is not null)
            {
                var activePaneId = workspace2.LayoutStore.ActivePaneId;
                var session = workspace2.GetSessionForPane(activePaneId);
                cwd = session?.LastKnownCwd ?? "";
            }

            var entry = CreateSidebarTabEntry(tabId, tab.Label, cwd, isActive, hasAttention);
            TabList.Children.Add(entry);
        }
    }

    private Grid CreateSidebarTabEntry(string tabId, string label, string cwd, bool isActive, bool hasAttention)
    {
        var grid = new Grid
        {
            Background = isActive ? _activeBg : _transparentBg,
            Padding = new Thickness(12, 8, 8, 8),
            Tag = tabId,
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Text content in column 0
        var textStack = new StackPanel { Orientation = Orientation.Vertical };
        Grid.SetColumn(textStack, 0);

        // Row 1: Title with optional blue dot
        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
        TextBlock? dotBlock = null;

        if (hasAttention)
        {
            dotBlock = new TextBlock
            {
                Text = "\u25CF ",
                FontSize = 12,
                Foreground = _attentionForeground,
                VerticalAlignment = VerticalAlignment.Center,
            };
            titlePanel.Children.Add(dotBlock);
        }

        var titleForeground = hasAttention ? _attentionForeground : _defaultForeground;
        var titleBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = titleForeground,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titlePanel.Children.Add(titleBlock);

        // Start blink animation for attention tabs
        if (hasAttention)
        {
            StartTabBlinkAnimation(tabId, titleBlock, dotBlock);
        }

        textStack.Children.Add(titlePanel);

        // Row 2: CWD
        if (!string.IsNullOrEmpty(cwd))
        {
            var cwdBlock = new TextBlock
            {
                Text = cwd,
                FontSize = 10,
                Foreground = _cwdForeground,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 2, 0, 0),
            };
            textStack.Children.Add(cwdBlock);
        }

        // Preview text removed per user preference

        grid.Children.Add(textStack);

        // Close button in column 1 (visible on hover only)
        var closeButton = new Button
        {
            Content = "X",
            FontSize = 10,
            Width = 20,
            Height = 20,
            MinWidth = 0,
            MinHeight = 0,
            Background = _transparentBg,
            Foreground = _closeButtonFg,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
            Visibility = Visibility.Collapsed,
            Tag = tabId,
        };
        Grid.SetColumn(closeButton, 1);

        closeButton.Click += (s, e) =>
        {
            if (_viewModel is null) return;
            _ = _viewModel.CloseTabAsync(tabId);
        };

        // Prevent close button pointer events from bubbling to the grid
        closeButton.PointerPressed += (s, e) => e.Handled = true;

        grid.Children.Add(closeButton);

        // Hover: show/hide close button
        grid.PointerEntered += (s, e) => closeButton.Visibility = Visibility.Visible;
        grid.PointerExited += (s, e) => closeButton.Visibility = Visibility.Collapsed;

        // Click to switch tab
        grid.PointerPressed += (s, e) =>
        {
            _viewModel?.SwitchTab(tabId);
        };

        // Right-click context menu for rename
        var menuFlyout = new MenuFlyout();
        var renameItem = new MenuFlyoutItem { Text = "Rename" };
        renameItem.Click += (s, e) => StartInlineRename(textStack, tabId, label);
        menuFlyout.Items.Add(renameItem);
        grid.ContextFlyout = menuFlyout;

        return grid;
    }

    private void StartTabBlinkAnimation(string tabId, TextBlock titleBlock, TextBlock? dotBlock)
    {
        int toggleCount = 0;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (s, e) =>
        {
            toggleCount++;
            if (toggleCount >= 8) // 4 full blinks
            {
                titleBlock.Foreground = _attentionForeground;
                if (dotBlock is not null) dotBlock.Foreground = _attentionForeground;
                timer.Stop();
                _tabBlinkTimers.Remove(tabId);
                return;
            }

            var brush = (toggleCount % 2 == 0) ? _attentionForeground : _defaultForeground;
            titleBlock.Foreground = brush;
            if (dotBlock is not null) dotBlock.Foreground = brush;
        };

        _tabBlinkTimers[tabId] = timer;
        timer.Start();
    }

    private void StartInlineRename(StackPanel textStack, string tabId, string currentLabel)
    {
        if (_viewModel is null) return;

        // Replace text content with a TextBox
        textStack.Children.Clear();

        var textBox = new TextBox
        {
            Text = currentLabel,
            FontSize = 12,
            Height = 28,
            MinWidth = 60,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0),
            SelectionStart = 0,
        };
        textBox.SelectAll();

        textStack.Children.Add(textBox);

        void CommitRename()
        {
            var newLabel = textBox.Text?.Trim();
            if (!string.IsNullOrEmpty(newLabel) && newLabel != currentLabel)
            {
                _viewModel?.RenameTab(tabId, newLabel);
            }
            else
            {
                RenderTabs();
            }
        }

        void CancelRename()
        {
            RenderTabs();
        }

        textBox.KeyDown += (s, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                CommitRename();
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                e.Handled = true;
                CancelRename();
            }
        };

        textBox.LostFocus += (s, e) => CommitRename();

        // Focus the textbox after it's in the visual tree
        DispatcherQueue?.TryEnqueue(() => textBox.Focus(FocusState.Programmatic));
    }
}
