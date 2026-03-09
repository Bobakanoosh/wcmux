# Phase 4: Custom Chrome and WebView2 Foundation - Research

**Researched:** 2026-03-08
**Domain:** WinUI 3 custom title bar + WebView2 environment sharing
**Confidence:** HIGH

## Summary

This phase has two independent workstreams: (1) replacing the default Windows title bar with a custom dark chrome using `ExtendsContentIntoTitleBar` and `InputNonClientPointerSource`, and (2) refactoring WebView2 initialization to share a single `CoreWebView2Environment` across all panes.

Both are well-documented patterns with official Microsoft guidance. The title bar approach uses `ExtendsContentIntoTitleBar = true` to hide the system chrome, then places custom XAML content in the title bar area. Drag regions are handled via `InputNonClientPointerSource.SetRegionRects()` rather than `Window.SetTitleBar()`, per the locked project decision, to avoid post-drag interactive control bugs. The WebView2 sharing uses `CoreWebView2Environment.CreateAsync()` once at startup and passes the cached environment to every `WebView2.EnsureCoreWebView2Async(environment)` call. WinAppSDK 1.5+ supports this overload (the project uses 1.8).

**Primary recommendation:** Implement title bar as a Grid row in MainWindow.xaml with manual `InputNonClientPointerSource` passthrough regions; create shared `CoreWebView2Environment` as a static singleton accessible from `App.xaml.cs`; modify `WebViewTerminalController.InitializeAsync()` to accept the environment parameter.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- App icon (small) on the far left, followed by "wcmux" text, window controls (minimize, maximize, close) on the right.
- Standard height (~40-48px) for comfortable spacing -- not compact.
- Title bar and tab bar are separate rows (title bar on top, tab bar below).
- Phase 6 replaces the horizontal tab bar with a vertical sidebar, so the title bar should not depend on or merge with the tab bar.
- Use `InputNonClientPointerSource` (not `SetTitleBar`) for the custom title bar to avoid post-drag interactive control bugs.
- Windows 11 snap layouts supported -- maximize button hover shows snap layout flyout.
- Standard drag-to-move and double-click-to-maximize/restore behavior.
- Share a single `CoreWebView2Environment` across all WebView2 instances to prevent memory bloat from independent browser process groups.
- Currently each `WebViewTerminalController` calls `EnsureCoreWebView2Async()` independently -- this needs to be refactored to pass a shared environment.

### Claude's Discretion
- Exact title bar color values (should complement existing #1e1e1e background).
- Window control button styling (custom-drawn vs system caption buttons).
- Exact app icon design/placeholder.
- WebView2 user data folder location for the shared environment.
- Typography and font weight for the "wcmux" title text.

### Deferred Ideas (OUT OF SCOPE)
None.

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CHRM-01 | User sees a custom dark title bar replacing the default Windows chrome | ExtendsContentIntoTitleBar + InputNonClientPointerSource pattern; AppWindowTitleBar color APIs for caption button styling; XAML Grid row with icon + text + system caption buttons |
| CHRM-02 | App uses a shared WebView2 environment across all panes to reduce memory overhead | CoreWebView2Environment.CreateAsync() singleton pattern; EnsureCoreWebView2Async(environment) overload available since WinAppSDK 1.5; refactor WebViewTerminalController to accept environment parameter |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.WindowsAppSDK | 1.8.260209005 | WinUI 3 framework, AppWindow APIs, InputNonClientPointerSource | Already in project; provides all title bar customization APIs |
| Microsoft.Web.WebView2 | 1.0.3179.45 | WebView2 browser control | Already in project; EnsureCoreWebView2Async(environment) overload available |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.UI.Windowing | (bundled with WinAppSDK) | AppWindowTitleBar for caption button colors and height | Title bar color customization |
| Microsoft.UI.Input | (bundled with WinAppSDK) | InputNonClientPointerSource for drag/passthrough regions | Custom title bar hit testing |
| Windows.Graphics | (WinRT built-in) | RectInt32 for region rectangles | SetRegionRects parameter |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| InputNonClientPointerSource | Window.SetTitleBar() | SetTitleBar is simpler but causes post-drag interactive control bugs (REJECTED by locked decision) |
| InputNonClientPointerSource | TitleBar control (WinAppSDK 1.7+) | TitleBar control uses SetTitleBar internally (REJECTED -- conflicts with locked decision) |
| Manual XAML title bar | AppWindowTitleBar only | AppWindowTitleBar alone cannot render custom XAML content in the title bar area |

## Architecture Patterns

### Recommended Project Structure
```
src/Wcmux.App/
  MainWindow.xaml          # Add title bar Grid row above TabBarView
  MainWindow.xaml.cs       # ExtendsContentIntoTitleBar, InputNonClientPointerSource setup
  Terminal/
    WebViewTerminalController.cs   # Accept CoreWebView2Environment parameter
    WebViewEnvironmentCache.cs     # NEW: singleton environment holder
  Views/
    TerminalPaneView.xaml.cs       # Pass environment through to controller
```

### Pattern 1: Custom Title Bar with InputNonClientPointerSource
**What:** Hide system chrome with `ExtendsContentIntoTitleBar = true`, render custom XAML in title bar area, use `InputNonClientPointerSource` for drag regions and passthrough regions for interactive controls.
**When to use:** When you need full control over title bar appearance and have interactive elements in the title bar.

Key implementation steps:
1. Set `ExtendsContentIntoTitleBar = true` in MainWindow constructor (must be in code, not XAML).
2. Set `AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall` for the 48px height.
3. Add title bar XAML as Row 0 in the root Grid.
4. On title bar Loaded and SizeChanged, calculate interactive regions.
5. Call `InputNonClientPointerSource.GetForWindowId(AppWindow.Id).SetRegionRects(NonClientRegionKind.Passthrough, rects)` for any interactive elements.
6. Set caption button colors via `AppWindow.TitleBar.ButtonBackgroundColor`, etc.

```csharp
// Source: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar
public MainWindow()
{
    InitializeComponent();
    ExtendsContentIntoTitleBar = true;

    // Tall title bar for ~48px height
    AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

    // Caption button colors to match dark theme
    AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
    AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
    AppWindow.TitleBar.ButtonHoverBackgroundColor =
        Color.FromArgb(255, 50, 50, 50);
    AppWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
    AppWindow.TitleBar.ButtonPressedBackgroundColor =
        Color.FromArgb(255, 40, 40, 40);
    AppWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
    AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    AppWindow.TitleBar.ButtonInactiveForegroundColor =
        Color.FromArgb(255, 120, 120, 120);

    AppTitleBar.Loaded += AppTitleBar_Loaded;
    AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
}

private void SetRegionsForCustomTitleBar()
{
    var scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

    // Get interactive element bounds and convert to RectInt32
    var transform = InteractiveElement.TransformToVisual(null);
    var bounds = transform.TransformBounds(
        new Rect(0, 0, InteractiveElement.ActualWidth,
                 InteractiveElement.ActualHeight));

    var rect = GetRect(bounds, scaleAdjustment);
    var rectArray = new Windows.Graphics.RectInt32[] { rect };

    var nonClientInputSrc =
        InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
    nonClientInputSrc.SetRegionRects(
        NonClientRegionKind.Passthrough, rectArray);
}

private Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale)
{
    return new Windows.Graphics.RectInt32(
        _X: (int)Math.Round(bounds.X * scale),
        _Y: (int)Math.Round(bounds.Y * scale),
        _Width: (int)Math.Round(bounds.Width * scale),
        _Height: (int)Math.Round(bounds.Height * scale));
}
```

### Pattern 2: Shared CoreWebView2Environment Singleton
**What:** Create a single `CoreWebView2Environment` at app startup and pass it to every `EnsureCoreWebView2Async()` call.
**When to use:** When multiple WebView2 instances exist in the same application.

```csharp
// Source: https://weblog.west-wind.com/posts/2023/Oct/31/Caching-your-WebView-Environment-to-manage-multiple-WebView2-Controls
// and https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/environment-controller-core
public static class WebViewEnvironmentCache
{
    private static CoreWebView2Environment? _environment;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task<CoreWebView2Environment> GetOrCreateAsync()
    {
        if (_environment is not null) return _environment;

        await _lock.WaitAsync();
        try
        {
            _environment ??= await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "wcmux", "WebView2Data"));
            return _environment;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

### Pattern 3: Passing Environment Through the Pane Chain
**What:** Thread the shared environment from MainWindow through WorkspaceView, TerminalPaneView, to WebViewTerminalController.
**When to use:** Required for CHRM-02.

The call chain is:
1. `MainWindow` creates or obtains shared `CoreWebView2Environment`
2. `WorkspaceView.AttachAsync()` receives and stores the environment
3. `TerminalPaneView.AttachAsync()` receives the environment
4. `WebViewTerminalController.InitializeAsync()` calls `EnsureCoreWebView2Async(environment)`

Alternatively, the simpler approach: `WebViewTerminalController.InitializeAsync()` directly calls `WebViewEnvironmentCache.GetOrCreateAsync()` -- no parameter threading needed.

**Recommendation:** Use the static cache approach. It avoids modifying the `AttachAsync` signatures of `WorkspaceView` and `TerminalPaneView`, which would be a larger refactor. The `WebViewTerminalController` is the only consumer.

### Anti-Patterns to Avoid
- **Mixing SetTitleBar with InputNonClientPointerSource:** Microsoft docs explicitly warn: "you should not use Window.SetTitleBar along with any lower level API which also sets drag regions as it can result in unexpected behavior."
- **Creating multiple CoreWebView2Environment instances:** Even with the same user data folder, creating multiple environments with different options causes `UnauthorizedException` errors.
- **Setting ExtendsContentIntoTitleBar in XAML:** Must be set in code-behind; setting in XAML causes an error.
- **Setting PreferredHeightOption before ExtendsContentIntoTitleBar:** Throws an exception. Must set `ExtendsContentIntoTitleBar = true` first.
- **Calculating regions before Loaded:** Title bar element dimensions are not available until the element is loaded. Always calculate regions in Loaded and SizeChanged handlers.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Window caption buttons (min/max/close) | Custom drawn buttons | System caption buttons via AppWindowTitleBar | System buttons handle snap layouts, accessibility, RTL, high DPI, window states automatically |
| DPI-aware region rectangles | Manual DPI calculation | `XamlRoot.RasterizationScale` | Already accounts for per-monitor DPI changes |
| WebView2 browser process management | Custom process pooling | Shared CoreWebView2Environment | The environment class handles process grouping internally |
| Drag-to-move behavior | WM_NCHITTEST handling | InputNonClientPointerSource drag regions | Handles double-click maximize, snap assist, etc. automatically |

**Key insight:** System caption buttons should remain system-managed. They automatically support Windows 11 snap layouts (hover maximize shows snap flyout), accessibility features, and correct behavior across window states. Custom-drawing them would require reimplementing all of this.

## Common Pitfalls

### Pitfall 1: Snap Layouts Not Appearing
**What goes wrong:** Windows 11 snap layout flyout does not appear when hovering over the maximize button.
**Why it happens:** Using `SetTitleBar` or not properly configuring `ExtendsContentIntoTitleBar`. In earlier WinAppSDK versions, there was a known bug (issue #6333).
**How to avoid:** Use `ExtendsContentIntoTitleBar = true` with system caption buttons (not custom buttons). Keep caption buttons system-managed via `AppWindowTitleBar`. The project's WinAppSDK 1.8 should have this working correctly.
**Warning signs:** Maximize button hover does nothing; no snap layout grid appears.

### Pitfall 2: Interactive Controls Unresponsive in Title Bar Area
**What goes wrong:** Buttons or other interactive elements placed in the title bar area don't respond to clicks.
**Why it happens:** The entire title bar area is treated as a drag region by default. Interactive elements need explicit passthrough regions.
**How to avoid:** Register passthrough regions via `InputNonClientPointerSource.SetRegionRects(NonClientRegionKind.Passthrough, rects)`. Recalculate on every size change.
**Warning signs:** Clicking title bar elements starts a window drag instead.

### Pitfall 3: Stale Region Rectangles After Resize
**What goes wrong:** After window resize, passthrough regions are in wrong positions.
**Why it happens:** Regions are set in physical pixels (scaled by DPI). If you only calculate once at startup, the regions become stale when the window or elements resize.
**How to avoid:** Subscribe to both `Loaded` and `SizeChanged` on the title bar element. Always recalculate and re-set regions in both handlers. Use `XamlRoot.RasterizationScale` for DPI conversion.
**Warning signs:** Interactive elements work at one window size but not after resize.

### Pitfall 4: WebView2 Environment Initialization Race
**What goes wrong:** Multiple WebView2 controls try to create the environment simultaneously, causing conflicts.
**Why it happens:** `CoreWebView2Environment.CreateAsync()` is async. If two panes initialize at the same time, both may try to create the environment.
**How to avoid:** Use `SemaphoreSlim` or equivalent synchronization in the environment cache. The singleton pattern with a lock prevents concurrent creation.
**Warning signs:** `UnauthorizedException` or `E_UNEXPECTED` errors during WebView2 initialization.

### Pitfall 5: User Data Folder Conflicts
**What goes wrong:** `CoreWebView2Environment.CreateAsync()` fails with access denied.
**Why it happens:** Creating a new environment with the same user data folder but different options fails. Also, the default user data folder may conflict with other WebView2 apps.
**How to avoid:** Always use an app-specific user data folder. Ensure consistent environment options across all calls. Use the singleton pattern so there is exactly one `CreateAsync` call.
**Warning signs:** E_UNEXPECTED (0x8000FFFF) or access denied errors.

## Code Examples

### Title Bar XAML Layout
```xml
<!-- Source: Microsoft Learn title bar customization docs -->
<!-- MainWindow.xaml - add as new Row 0 -->
<Grid x:Name="AppTitleBar"
      Height="48"
      Background="#1e1e1e">
    <Grid.ColumnDefinitions>
        <!-- Icon -->
        <ColumnDefinition Width="Auto" />
        <!-- Title text -->
        <ColumnDefinition Width="Auto" />
        <!-- Drag region (fills remaining space) -->
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <!-- App icon placeholder -->
    <Image Grid.Column="0"
           Source="Assets/app-icon.png"
           Width="16" Height="16"
           Margin="16,0,0,0"
           VerticalAlignment="Center" />

    <!-- App title -->
    <TextBlock Grid.Column="1"
               Text="wcmux"
               Foreground="#cccccc"
               FontSize="12"
               FontWeight="Normal"
               VerticalAlignment="Center"
               Margin="12,0,0,0"
               IsHitTestVisible="False" />

    <!-- Remaining space is drag region (no passthrough needed) -->
</Grid>
```

### Caption Button Color Configuration
```csharp
// Source: https://learn.microsoft.com/en-us/windows/apps/develop/title-bar
// Set in MainWindow constructor after ExtendsContentIntoTitleBar = true

var titleBar = AppWindow.TitleBar;
titleBar.ButtonBackgroundColor = Colors.Transparent;
titleBar.ButtonForegroundColor = Color.FromArgb(255, 204, 204, 204);
titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 45, 45, 45);
titleBar.ButtonHoverForegroundColor = Colors.White;
titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 55, 55, 55);
titleBar.ButtonPressedForegroundColor = Colors.White;
titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 100, 100, 100);
```

### WebView2 Environment Sharing
```csharp
// WebViewTerminalController.InitializeAsync - modified
public async Task InitializeAsync(
    Action<byte[]> onInput,
    Action<int, int> onResize,
    Action? onReady = null,
    Action<string>? onCommand = null)
{
    _onInput = onInput;
    _onResize = onResize;
    _onReady = onReady;
    _onCommand = onCommand;

    // Use shared environment instead of default
    var environment = await WebViewEnvironmentCache.GetOrCreateAsync();
    await _webView.EnsureCoreWebView2Async(environment);

    _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    // ... rest unchanged
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| SetTitleBar() for drag regions | InputNonClientPointerSource for regions | Available since WinAppSDK 1.3 | Avoids post-drag control bugs |
| No custom environment support in WinUI 3 | EnsureCoreWebView2Async(environment) overload | WinAppSDK 1.5 | Enables shared environments in WinUI 3 |
| Manual title bar XAML only | TitleBar control (WinAppSDK 1.7+) | WinAppSDK 1.7 | Simplified title bar (but uses SetTitleBar internally, so not usable here) |
| Standard height only | PreferredHeightOption = Tall | WinAppSDK 1.0+ | Supports ~48px title bar |

**Deprecated/outdated:**
- **TitleBar control (WinAppSDK 1.7):** New and convenient, but uses `SetTitleBar` internally. Cannot be combined with `InputNonClientPointerSource` per locked decision.
- **Using `SetTitleBar` with `InputNonClientPointerSource`:** Microsoft warns against mixing these two approaches.

## Open Questions

1. **App icon asset**
   - What we know: The title bar needs a small icon on the far left.
   - What's unclear: Whether a placeholder (FontIcon glyph) or actual image asset is preferred for initial implementation.
   - Recommendation: Use a FontIcon glyph (e.g., terminal symbol) as placeholder. A real icon can be swapped in later.

2. **Caption button close-hover color**
   - What we know: The close button hover/pressed states always use system-defined colors (red background). This cannot be overridden via AppWindowTitleBar APIs.
   - What's unclear: Whether the red close-hover clashes with the dark theme.
   - Recommendation: Accept the system default. Users expect the red close-hover behavior.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 |
| Config file | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| Quick run command | `dotnet test tests/Wcmux.Tests --filter "Category!=Integration" -v q` |
| Full suite command | `dotnet test tests/Wcmux.Tests -v q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CHRM-01 | Custom dark title bar visible with window controls | manual-only | N/A (visual, requires window) | N/A |
| CHRM-01 | Drag to move and double-click maximize/restore | manual-only | N/A (requires window interaction) | N/A |
| CHRM-01 | Snap layouts on maximize hover | manual-only | N/A (requires Windows 11 shell) | N/A |
| CHRM-02 | Shared CoreWebView2Environment singleton | unit | `dotnet test tests/Wcmux.Tests --filter "WebViewEnvironmentCache" -v q` | No -- Wave 0 |
| CHRM-02 | WebViewTerminalController uses shared environment | unit | `dotnet test tests/Wcmux.Tests --filter "WebViewTerminalController" -v q` | No -- Wave 0 |

**Manual test justification for CHRM-01:** Title bar rendering, drag behavior, snap layouts, and caption button styling all require a running WinUI 3 window with Windows shell integration. These cannot be meaningfully unit-tested. Verification requires visual inspection and interaction.

### Sampling Rate
- **Per task commit:** `dotnet test tests/Wcmux.Tests --filter "Category!=Integration" -v q`
- **Per wave merge:** `dotnet test tests/Wcmux.Tests -v q`
- **Phase gate:** Full suite green + manual verification of title bar appearance and behavior.

### Wave 0 Gaps
- [ ] `tests/Wcmux.Tests/Terminal/WebViewEnvironmentCacheTests.cs` -- covers CHRM-02 singleton behavior (thread safety, consistent return)
- [ ] Manual test checklist document for CHRM-01 visual/interaction verification

Note: Testing the `WebViewEnvironmentCache` as a pure unit test is limited because `CoreWebView2Environment.CreateAsync()` requires a real WebView2 runtime. The test may need to verify the synchronization logic (SemaphoreSlim behavior) and the caching pattern rather than actual environment creation.

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: Title bar customization](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar) - Complete guide for ExtendsContentIntoTitleBar, InputNonClientPointerSource, AppWindowTitleBar color APIs, and PreferredHeightOption. Updated 2026-02-28.
- [Microsoft Learn: Main classes for WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/environment-controller-core) - CoreWebView2Environment grouping behavior and relationship to browser processes.
- [Microsoft Learn: TitleBar control](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/title-bar) - New TitleBar control (WinAppSDK 1.7+); uses SetTitleBar internally.
- [Microsoft Learn: WebView2 in WinUI 3](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/winui3-windows-app-sdk) - EnsureCoreWebView2Async(environment) overload available since WinAppSDK 1.5.

### Secondary (MEDIUM confidence)
- [Rick Strahl: Caching WebView Environment](https://weblog.west-wind.com/posts/2023/Oct/31/Caching-your-WebView-Environment-to-manage-multiple-WebView2-Controls) - Singleton pattern for shared environment with SemaphoreSlim synchronization.
- [GitHub: WinUI 3 WebView2 custom environment issue #6150](https://github.com/microsoft/microsoft-ui-xaml/issues/6150) - Confirmed resolved in WinAppSDK 1.5.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Project already uses WinAppSDK 1.8 and WebView2; all required APIs are documented and available
- Architecture: HIGH - Both patterns (custom title bar with InputNonClientPointerSource and shared WebView2 environment) are well-documented by Microsoft with complete code examples
- Pitfalls: HIGH - Based on official docs warnings and known GitHub issues with tracked resolutions

**Research date:** 2026-03-08
**Valid until:** 2026-04-08 (stable APIs, unlikely to change)
