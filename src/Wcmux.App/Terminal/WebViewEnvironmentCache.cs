using Microsoft.Web.WebView2.Core;

namespace Wcmux.App.Terminal;

/// <summary>
/// Provides a shared <see cref="CoreWebView2Environment"/> singleton so all
/// terminal panes share a single browser process group instead of each pane
/// spawning independent ones. Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
public static class WebViewEnvironmentCache
{
    private static CoreWebView2Environment? _environment;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Returns the shared environment, creating it on first call.
    /// Multiple concurrent callers are serialized by the semaphore;
    /// only the first caller triggers <see cref="CoreWebView2Environment.CreateAsync"/>.
    /// </summary>
    public static async Task<CoreWebView2Environment> GetOrCreateAsync()
    {
        if (_environment is not null) return _environment;

        await _lock.WaitAsync();
        try
        {
            if (_environment is null)
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "wcmux", "WebView2Data");
                var options = new CoreWebView2EnvironmentOptions();
                _environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    string.Empty, userDataFolder, options);
            }
            return _environment;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Resets the cached environment. For testing only.
    /// </summary>
    internal static void Reset()
    {
        _environment = null;
    }
}
