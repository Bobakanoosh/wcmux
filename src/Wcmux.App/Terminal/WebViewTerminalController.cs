using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Wcmux.App.Terminal;

/// <summary>
/// Controls a WebView2-hosted xterm.js terminal surface. Owns the WebView2
/// lifecycle, provides methods to write output and resize, and forwards
/// user input to a callback. This is the narrow adapter between the native
/// WinUI shell and the hosted terminal renderer.
/// </summary>
public sealed class WebViewTerminalController : IAsyncDisposable
{
    private readonly WebView2 _webView;
    private Action<byte[]>? _onInput;
    private Action<int, int>? _onResize;
    private Action<string>? _onCommand;
    private Action? _onReady;
    private bool _disposed;
    private bool _initialized;

    public WebViewTerminalController(WebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    /// <summary>
    /// Whether the hosted terminal surface is initialized and ready.
    /// </summary>
    public bool IsReady => _initialized;

    /// <summary>
    /// Initializes the WebView2 environment and navigates to the terminal
    /// host page. Call this once after the WebView2 control is loaded.
    /// </summary>
    public async Task InitializeAsync(
        Action<byte[]> onInput,
        Action<int, int> onResize,
        Action? onReady = null,
        Action<string>? onCommand = null)
    {
        _onInput = onInput ?? throw new ArgumentNullException(nameof(onInput));
        _onResize = onResize ?? throw new ArgumentNullException(nameof(onResize));
        _onReady = onReady;
        _onCommand = onCommand;

        // Initialize WebView2 environment
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        // Disable default context menu and dev tools in release
#if !DEBUG
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

        // Navigate to the terminal host page
        var appDir = AppContext.BaseDirectory;
        var htmlPath = Path.Combine(appDir, "TerminalWeb", "index.html");
        _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
    }

    /// <summary>
    /// Writes VT output data to the hosted terminal surface.
    /// The data is base64-encoded for safe transport across the WebView2 boundary.
    /// </summary>
    public async Task WriteOutputAsync(string text)
    {
        if (_disposed || !_initialized) return;

        var base64 = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(text));
        await _webView.CoreWebView2.ExecuteScriptAsync(
            $"writeOutput('{base64}')");
    }

    /// <summary>
    /// Triggers a resize/fit of the terminal surface and returns the
    /// new dimensions.
    /// </summary>
    public async Task ResizeAsync()
    {
        if (_disposed || !_initialized) return;
        await _webView.CoreWebView2.ExecuteScriptAsync("resize()");
    }

    /// <summary>
    /// Focuses the terminal surface. Moves both WinUI keyboard focus to the
    /// WebView2 control and xterm.js focus within the hosted page.
    /// </summary>
    public async Task FocusAsync()
    {
        if (_disposed || !_initialized) return;
        _webView.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        await _webView.CoreWebView2.ExecuteScriptAsync("focus()");
    }

    /// <summary>
    /// Gets the measured cell size from the terminal renderer.
    /// </summary>
    public async Task<(double Width, double Height)> GetCellSizeAsync()
    {
        if (_disposed || !_initialized) return (8.4, 17.0);

        var json = await _webView.CoreWebView2.ExecuteScriptAsync("getCellSize()");
        // ExecuteScriptAsync returns a JSON-encoded string, so we need to unwrap it
        var unquoted = JsonSerializer.Deserialize<string>(json) ?? "{}";
        using var doc = JsonDocument.Parse(unquoted);
        var width = doc.RootElement.GetProperty("width").GetDouble();
        var height = doc.RootElement.GetProperty("height").GetDouble();
        return (width, height);
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var json = args.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "ready":
                    _initialized = true;
                    _onReady?.Invoke();
                    break;

                case "input":
                    var base64 = root.GetProperty("data").GetString();
                    if (base64 is not null)
                    {
                        // Decode the base64 input back to raw bytes
                        var bytes = Convert.FromBase64String(base64);
                        _onInput?.Invoke(bytes);
                    }
                    break;

                case "resize":
                    var cols = root.GetProperty("cols").GetInt32();
                    var rows = root.GetProperty("rows").GetInt32();
                    _onResize?.Invoke(cols, rows);
                    break;

                case "command":
                    var command = root.GetProperty("command").GetString();
                    if (command is not null)
                    {
                        _onCommand?.Invoke(command);
                    }
                    break;
            }
        }
        catch
        {
            // Malformed message -- ignore
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_initialized)
        {
            try
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                await _webView.CoreWebView2.ExecuteScriptAsync("dispose()");
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
