using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Wcmux.App.ViewModels;

namespace Wcmux.App.Views;

/// <summary>
/// Tab bar UI with tab items, close buttons, add button, and inline rename.
/// Renders from TabStore state and delegates commands to TabViewModel.
/// </summary>
public sealed partial class TabBarView : UserControl
{
    private TabViewModel? _viewModel;

    /// <summary>Fired when the user clicks the [+] button to request a new tab.</summary>
    public event Func<Task>? NewTabRequested;

    public TabBarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Attaches to a TabViewModel and subscribes to tab events.
    /// </summary>
    public void Attach(TabViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.TabStore.TabsChanged += OnTabsChanged;
        _viewModel.TabStore.ActiveTabChanged += OnActiveTabChanged;
        RenderTabs();
    }

    private void OnTabsChanged() => DispatcherQueue?.TryEnqueue(RenderTabs);

    private void OnActiveTabChanged(string _) => DispatcherQueue?.TryEnqueue(RenderTabs);

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

        TabStrip.Children.Clear();

        var activeTabId = _viewModel.TabStore.ActiveTabId;

        foreach (var tabId in _viewModel.TabStore.TabOrder)
        {
            var tab = _viewModel.TabStore.GetTab(tabId);
            if (tab is null) continue;

            var isActive = tabId == activeTabId;
            var tabItem = CreateTabItem(tabId, tab.Label, isActive);
            TabStrip.Children.Add(tabItem);
        }
    }

    private Grid CreateTabItem(string tabId, string label, bool isActive)
    {
        var grid = new Grid
        {
            Height = 32,
            MinWidth = 80,
            Background = new SolidColorBrush(isActive
                ? Windows.UI.Color.FromArgb(255, 60, 60, 60)
                : Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            Padding = new Thickness(0),
            Tag = tabId,
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204)),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 0, 4, 0),
            Tag = tabId,
        };
        Grid.SetColumn(labelBlock, 0);

        // Double-click to rename
        labelBlock.DoubleTapped += (s, e) => StartInlineRename(grid, tabId, label);

        var closeButton = new Button
        {
            Content = "X",
            FontSize = 10,
            Width = 20,
            Height = 20,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 136, 136, 136)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(0),
            Tag = tabId,
        };
        Grid.SetColumn(closeButton, 1);

        closeButton.Click += (s, e) =>
        {
            if (_viewModel is null) return;
            _ = _viewModel.CloseTabAsync(tabId);
        };

        // Tab click to switch (only if not clicking close button)
        grid.PointerPressed += (s, e) =>
        {
            _viewModel?.SwitchTab(tabId);
        };

        grid.Children.Add(labelBlock);
        grid.Children.Add(closeButton);

        return grid;
    }

    private void StartInlineRename(Grid tabItemGrid, string tabId, string currentLabel)
    {
        if (_viewModel is null) return;

        // Replace tab item content with a TextBox
        tabItemGrid.Children.Clear();

        var textBox = new TextBox
        {
            Text = currentLabel,
            FontSize = 12,
            Height = 28,
            MinWidth = 60,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            SelectionStart = 0,
        };
        textBox.SelectAll();

        Grid.SetColumnSpan(textBox, 2);
        tabItemGrid.Children.Add(textBox);

        void CommitRename()
        {
            var newLabel = textBox.Text?.Trim();
            if (!string.IsNullOrEmpty(newLabel) && newLabel != currentLabel)
            {
                _viewModel?.RenameTab(tabId, newLabel);
            }
            else
            {
                // Re-render to restore original display
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
