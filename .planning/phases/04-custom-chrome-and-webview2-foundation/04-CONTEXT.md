# Phase 4: Custom Chrome and WebView2 Foundation - Context

**Gathered:** 2026-03-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace the default Windows title bar with a custom dark chrome and establish a shared WebView2 environment across all panes. The title bar provides window controls and drag region. The shared WebView2 environment reduces memory overhead from independent browser processes per pane. Pane title bars, vertical tab sidebar, browser panes, and pane interaction are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Title bar content
- App icon (small) on the far left, followed by "wcmux" text, window controls (minimize, maximize, close) on the right.
- Standard height (~40-48px) for comfortable spacing — not compact.
- Title bar and tab bar are separate rows (title bar on top, tab bar below).
- Note: Phase 6 replaces the horizontal tab bar with a vertical sidebar, so the title bar should not depend on or merge with the tab bar.

### Title bar behavior
- Use `InputNonClientPointerSource` (not `SetTitleBar`) for the custom title bar to avoid post-drag interactive control bugs (roadmap decision).
- Windows 11 snap layouts supported — maximize button hover shows snap layout flyout.
- Standard drag-to-move and double-click-to-maximize/restore behavior.

### WebView2 sharing
- Share a single `CoreWebView2Environment` across all WebView2 instances to prevent memory bloat from independent browser process groups (roadmap decision).
- Currently each `WebViewTerminalController` calls `EnsureCoreWebView2Async()` independently — this needs to be refactored to pass a shared environment.

### Claude's Discretion
- Exact title bar color values (should complement existing #1e1e1e background).
- Window control button styling (custom-drawn vs system caption buttons).
- Exact app icon design/placeholder.
- WebView2 user data folder location for the shared environment.
- Typography and font weight for the "wcmux" title text.

</decisions>

<specifics>
## Specific Ideas

- Title bar should match the dark terminal aesthetic — not feel like a bolted-on Windows chrome.
- The tab bar will become a vertical sidebar in Phase 6, so the horizontal tab bar row is temporary. Title bar design should not couple to it.
- Snap layouts are a native Windows 11 convenience that should work as expected.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainWindow.xaml`: Already has a Grid with Row 0 (TabBarView) and Row 1 (TabContentArea). Title bar becomes a new Row 0, pushing others down.
- `MainWindow.xaml.cs`: Window handle retrieval via `WinRT.Interop.WindowNative.GetWindowHandle(this)` already in use — needed for `InputNonClientPointerSource`.
- `WebViewTerminalController`: Currently calls `EnsureCoreWebView2Async()` without a shared environment — the refactor point for CHRM-02.
- `App.xaml.cs`: App startup and window creation — may need to create the shared `CoreWebView2Environment` here before any panes initialize.

### Established Patterns
- Dark background (#1e1e1e) already set on MainWindow's root Grid.
- WinUI 3 XAML layout with code-behind event handling.
- P/Invoke for Win32 interop (`SetForegroundWindow` already used).
- Async initialization pattern in `MainWindow.OnActivated` / `InitializeAsync`.

### Integration Points
- `MainWindow.xaml`: Title bar XAML goes above the existing TabBarView row.
- `WebViewTerminalController.InitializeAsync()`: Needs to accept a `CoreWebView2Environment` parameter instead of calling `EnsureCoreWebView2Async()` with no args.
- `TerminalPaneView.AttachAsync()`: Needs to receive and pass the shared environment to `WebViewTerminalController`.
- `App.xaml.cs` or `MainWindow`: Shared environment creation point.

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 04-custom-chrome-and-webview2-foundation*
*Context gathered: 2026-03-08*
