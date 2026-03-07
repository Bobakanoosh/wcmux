using Microsoft.UI.Xaml;
using Wcmux.App.Commands;
using Wcmux.App.ViewModels;
using Wcmux.Core.Runtime;

namespace Wcmux.App;

public sealed partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;
    private WorkspaceViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _sessionManager = new SessionManager();
        Activated += OnActivated;
        Closed += OnClosed;
    }

    /// <summary>Exposed for testing.</summary>
    internal WorkspaceViewModel? ViewModel => _viewModel;

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        await CreateInitialSessionAsync();
    }

    internal async Task CreateInitialSessionAsync()
    {
        try
        {
            var spec = SessionLaunchSpec.CreateDefaultPowerShell(
                Environment.CurrentDirectory);
            var session = await _sessionManager.CreateSessionAsync(spec);

            var initialPaneId = Guid.NewGuid().ToString("N");
            _viewModel = new WorkspaceViewModel(_sessionManager, initialPaneId, session);
            _viewModel.LastPaneClosed += () =>
            {
                DispatcherQueue?.TryEnqueue(Close);
            };

            // Attach keyboard bindings to the window content
            if (Content is UIElement rootElement)
            {
                PaneCommandBindings.Attach(rootElement, _viewModel);
            }

            // Attach workspace view
            await Workspace.AttachAsync(_viewModel);
        }
        catch (Exception ex)
        {
            Title = $"wcmux - Session failed: {ex.Message}";
        }
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        await Workspace.DetachAsync();

        if (_viewModel is not null)
        {
            await _viewModel.DisposeAsync();
        }

        await _sessionManager.DisposeAsync();
    }
}
