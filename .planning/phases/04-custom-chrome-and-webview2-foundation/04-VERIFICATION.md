---
phase: 04-custom-chrome-and-webview2-foundation
verified: 2026-03-08T22:00:00Z
status: passed
score: 3/3 success criteria verified
human_verification:
  - test: "Launch app and verify dark custom title bar appearance"
    expected: "Dark (#1e1e1e) title bar with terminal icon, 'wcmux' text, and dark-themed caption buttons replacing default white Windows chrome"
    why_human: "Visual appearance and theme coherence cannot be verified programmatically"
  - test: "Drag window by title bar and double-click to maximize/restore"
    expected: "Window moves smoothly when dragged; double-click toggles maximize/restore"
    why_human: "Runtime drag and window management behavior requires interactive testing"
  - test: "Hover maximize button on Windows 11"
    expected: "Snap layout flyout appears"
    why_human: "OS-level snap layout integration requires interactive testing on Windows 11"
  - test: "Open multiple terminal panes and check Task Manager"
    expected: "Single msedgewebview2.exe process group instead of one per pane"
    why_human: "Process group sharing must be verified at runtime via Task Manager or Process Explorer"
---

# Phase 4: Custom Chrome and WebView2 Foundation Verification Report

**Phase Goal:** Users see a polished dark app shell instead of default Windows chrome, and WebView2 resources are shared efficiently across panes.
**Verified:** 2026-03-08T22:00:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User sees a dark custom title bar with window controls (minimize, maximize, close) that matches the app aesthetic instead of default white Windows chrome | VERIFIED | MainWindow.xaml: AppTitleBar Grid (line 18) with #1e1e1e background, FontIcon glyph E756, "wcmux" TextBlock. MainWindow.xaml.cs: ExtendsContentIntoTitleBar=true (line 32), 8 caption button color properties configured (lines 36-43) |
| 2 | User can drag the window by the custom title bar and double-click to maximize/restore without glitches | VERIFIED | ExtendsContentIntoTitleBar=true makes entire content area draggable by default; InputNonClientPointerSource.GetForWindowId (line 70) with SetRegionRects establishes the non-client region system; Loaded/SizeChanged handlers (lines 46-47) recalculate regions |
| 3 | All existing terminal panes share a single WebView2 browser process group instead of spawning independent ones | VERIFIED | WebViewEnvironmentCache.cs: static singleton with SemaphoreSlim (lines 13, 24), CreateWithOptionsAsync (line 34). WebViewTerminalController.cs line 49: `var environment = await WebViewEnvironmentCache.GetOrCreateAsync()` passed to EnsureCoreWebView2Async(environment) on line 50 |

**Score:** 3/3 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Wcmux.App/MainWindow.xaml` | Title bar Grid with app icon and title text above TabBarView | VERIFIED | AppTitleBar Grid at Row 0 (line 18), FontIcon + TextBlock, TabBarView at Row 1, TabContentArea at Row 2. 46 lines. |
| `src/Wcmux.App/MainWindow.xaml.cs` | ExtendsContentIntoTitleBar, InputNonClientPointerSource, caption button colors | VERIFIED | ExtendsContentIntoTitleBar (line 32), PreferredHeightOption.Standard (line 33), 8 button color properties (lines 36-43), SetRegionsForCustomTitleBar method (line 64), InputNonClientPointerSource (line 70). 409 lines. |
| `src/Wcmux.App/Terminal/WebViewEnvironmentCache.cs` | Singleton CoreWebView2Environment with thread-safe lazy initialization | VERIFIED | Static class, SemaphoreSlim lock, GetOrCreateAsync with double-check pattern, app-specific userDataFolder, Reset for testing. 52 lines (>15 min_lines). |
| `src/Wcmux.App/Terminal/WebViewTerminalController.cs` | Refactored InitializeAsync using shared environment | VERIFIED | Lines 49-50: `var environment = await WebViewEnvironmentCache.GetOrCreateAsync(); await _webView.EnsureCoreWebView2Async(environment);`. 185 lines. |
| `tests/Wcmux.Tests/Terminal/WebViewEnvironmentCacheTests.cs` | Unit tests for cache singleton behavior | VERIFIED | 5 tests: GetOrCreateAsync_ReturnsTask, Reset_MethodExists, Class_IsStaticSingleton (structural), plus 2 integration tests for concurrent singleton and reset. 94 lines (>20 min_lines). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| MainWindow.xaml.cs | AppWindow.TitleBar | ExtendsContentIntoTitleBar = true and caption button color APIs | WIRED | Line 32: `ExtendsContentIntoTitleBar = true;` Lines 36-43: 8 button color properties set |
| MainWindow.xaml.cs | InputNonClientPointerSource | SetRegionRects for passthrough regions | WIRED | Line 70: `InputNonClientPointerSource.GetForWindowId(AppWindow.Id)` with SetRegionRects call |
| WebViewTerminalController.cs | WebViewEnvironmentCache.cs | GetOrCreateAsync call in InitializeAsync | WIRED | Line 49: `var environment = await WebViewEnvironmentCache.GetOrCreateAsync();` Line 50: passed to `EnsureCoreWebView2Async(environment)` |
| WebViewEnvironmentCache.cs | CoreWebView2Environment.CreateAsync | SemaphoreSlim-guarded singleton creation | WIRED | Line 34: `CoreWebView2Environment.CreateWithOptionsAsync(string.Empty, userDataFolder, options)` (adapted from CreateAsync per WinRT API) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| CHRM-01 | 04-01-PLAN | User sees a custom dark title bar replacing the default Windows chrome | SATISFIED | AppTitleBar XAML element, ExtendsContentIntoTitleBar=true, caption button dark theme colors, InputNonClientPointerSource scaffold |
| CHRM-02 | 04-02-PLAN | App uses a shared WebView2 environment across all panes to reduce memory overhead | SATISFIED | WebViewEnvironmentCache singleton with SemaphoreSlim, WebViewTerminalController refactored to use GetOrCreateAsync, app-specific userDataFolder |

No orphaned requirements found. REQUIREMENTS.md maps exactly CHRM-01 and CHRM-02 to Phase 4, and both plans claim them.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | - | - | - | No TODO, FIXME, PLACEHOLDER, stub returns, or empty implementations found in any modified file |

### Human Verification Required

### 1. Visual Title Bar Appearance

**Test:** Launch the app with `dotnet run --project src/Wcmux.App` and inspect the title bar.
**Expected:** Dark (#1e1e1e) custom title bar with terminal icon on left, "wcmux" text, and dark-themed system caption buttons (minimize, maximize, close) on the right. No default white Windows chrome visible.
**Why human:** Visual appearance, color matching, and theme coherence cannot be verified programmatically.

### 2. Window Drag and Maximize

**Test:** Click and drag the title bar area; double-click the title bar.
**Expected:** Window moves smoothly when dragged. Double-click toggles between maximized and restored state.
**Why human:** Runtime window management behavior requires interactive testing.

### 3. Windows 11 Snap Layout Flyout

**Test:** Hover over the maximize caption button on Windows 11.
**Expected:** Snap layout flyout appears showing layout options.
**Why human:** OS-level snap layout integration is Windows 11-specific and requires interactive testing.

### 4. Shared WebView2 Process Group

**Test:** Open 3+ terminal panes, then check Task Manager for msedgewebview2.exe processes.
**Expected:** A single WebView2 process group (one main + renderer processes) rather than separate groups per pane.
**Why human:** Process group sharing must be verified at runtime via Task Manager or Process Explorer.

### Gaps Summary

No gaps found. All must-have truths are verified at the code level. All artifacts exist, are substantive (not stubs), and are properly wired. Both requirements (CHRM-01, CHRM-02) are satisfied. No anti-patterns detected. Four items flagged for human verification are runtime/visual checks that cannot be automated.

All four commits are verified in git history:
- `5385dbf` feat(04-01): implement custom dark title bar with InputNonClientPointerSource
- `0b3d76f` fix(04-01): reduce title bar height from 48px to 32px
- `7da1cd9` feat(04-02): add WebViewEnvironmentCache singleton for shared WebView2 environment
- `7dc1efe` refactor(04-02): use shared WebView2 environment in WebViewTerminalController

---

_Verified: 2026-03-08T22:00:00Z_
_Verifier: Claude (gsd-verifier)_
