---
phase: 05-pane-title-bars-and-browser-panes
plan: 02
subsystem: ui
tags: [winui3, webview2, browser-pane, address-bar, pane-kind]

requires:
  - phase: 05-pane-title-bars-and-browser-panes
    plan: 01
    provides: PaneKind enum, pane title bar infrastructure, process detection
  - phase: 04-custom-chrome-and-webview2-foundation
    provides: WebViewEnvironmentCache shared environment
provides:
  - BrowserPaneView UserControl with address bar and WebView2 content area
  - Browser pane creation flow via SplitActivePaneAsBrowserAsync
  - PaneKind-aware pane rendering in WorkspaceView
  - Globe button in pane title bar for opening browser panes
affects: [phase-06-sidebar]

tech-stack:
  added: []
  patterns: [pane-kind-dispatch-in-view, sentinel-session-id-for-browser-panes, js-key-interception-via-web-message]

key-files:
  created:
    - src/Wcmux.App/Views/BrowserPaneView.xaml
    - src/Wcmux.App/Views/BrowserPaneView.xaml.cs
  modified:
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs
    - src/Wcmux.App/ViewModels/WorkspaceViewModel.cs
    - src/Wcmux.Core/Layout/LayoutStore.cs
    - src/Wcmux.Core/Layout/LayoutReducer.cs

key-decisions:
  - "Used JS injection via AddScriptToExecuteOnDocumentCreatedAsync + WebMessageReceived for shortcut interception inside WebView2 content"
  - "Browser panes use sentinel session ID prefixed with 'browser:' to avoid ConPTY session creation"
  - "Browser title bar has full button set (split-h, split-v, browser, close) matching terminal title bars"
  - "Browser pane defaults to google.com instead of about:blank"
  - "Used GotFocus instead of PointerPressed for browser pane focus detection (WebView2 swallows pointer events)"

patterns-established:
  - "Browser pane pattern: sentinel sessionId, no _paneSessions entry, PaneKind.Browser in LeafNode"
  - "PaneKind dispatch: CreatePaneViewAsync checks LeafNode.Kind to create terminal vs browser view"

requirements-completed: [PBAR-04]

duration: 5min
completed: 2026-03-09
---

# Phase 05 Plan 02: Browser Pane Hosting Summary

**BrowserPaneView with address bar, navigation controls, and WebView2 content using shared environment, opened via globe button in pane title bars**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-09T03:11:13Z
- **Completed:** 2026-03-09T03:16:00Z
- **Tasks:** 2/2 (Task 2 was human-verify checkpoint — approved)
- **Files modified:** 6

## Accomplishments
- BrowserPaneView UserControl with address bar (back, forward, reload, URL box, go) and WebView2 content area
- Browser panes use shared WebViewEnvironmentCache -- no separate browser process groups
- Globe button in pane title bar opens browser pane via vertical split
- PaneKind-aware pane creation and rendering in WorkspaceView
- App-level keyboard shortcuts intercepted via JS injection + WebMessageReceived and PreviewKeyDown fallback
- Browser panes cleanly close via title bar X button with no session teardown needed

## Task Commits

Each task was committed atomically:

1. **Task 1: BrowserPaneView, browser split flow, and PaneKind-aware rendering** - `f117751` (feat)
2. **Task 2: Human verification checkpoint** - `acaf202` (fix — addressed 5 feedback items)

## Files Created/Modified
- `src/Wcmux.App/Views/BrowserPaneView.xaml` - XAML layout with address bar grid and WebView2
- `src/Wcmux.App/Views/BrowserPaneView.xaml.cs` - Code-behind with navigation, URL handling, shortcut interception
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - PaneKind-aware pane creation, globe button, browser view lifecycle
- `src/Wcmux.App/ViewModels/WorkspaceViewModel.cs` - SplitActivePaneAsBrowserAsync, browser-safe ClosePaneAsync
- `src/Wcmux.Core/Layout/LayoutStore.cs` - PaneKind parameter on SplitActivePane, GetLeafNode public method
- `src/Wcmux.Core/Layout/LayoutReducer.cs` - PaneKind parameter on SplitPane

## Decisions Made
- Used PreviewKeyDown instead of AcceleratorKeyPressed for shortcut interception (WinUI3 WebView2 does not expose AcceleratorKeyPressed directly)
- Browser panes use sentinel session ID prefixed with "browser:" to distinguish from terminal panes without ConPTY sessions
- Browser title bar shows static "browser" label with only close button (no additional split buttons to keep UI clean)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Replaced AcceleratorKeyPressed with JS injection + WebMessageReceived**
- **Found during:** Task 1 (build verification)
- **Issue:** WinUI3 WebView2 control does not expose CoreWebView2Controller.AcceleratorKeyPressed
- **Fix:** Used AddScriptToExecuteOnDocumentCreatedAsync to inject JS key interceptor that posts messages via window.chrome.webview.postMessage, plus PreviewKeyDown as fallback
- **Files modified:** src/Wcmux.App/Views/BrowserPaneView.xaml.cs
- **Committed in:** f117751, acaf202

### Checkpoint Feedback Fixes

**2. [Checkpoint] Browser title bar missing split buttons**
- **Fix:** Added split-h, split-v, and browser buttons to CreateBrowserPaneTitleBar
- **Committed in:** acaf202

**3. [Checkpoint] Address bar background turns white on focus**
- **Fix:** Overrode TextControlBackgroundFocused/PointerOver WinUI3 resources
- **Committed in:** acaf202

**4. [Checkpoint] Browser pane opacity doesn't change on click**
- **Fix:** Replaced PointerPressed with GotFocus (WebView2 swallows pointer events)
- **Committed in:** acaf202

**5. [Checkpoint] Default to google.com instead of about:blank**
- **Committed in:** acaf202

**6. [Checkpoint] Ctrl+Shift+Arrow not working in browser pane**
- **Fix:** Injected JavaScript key interceptor via AddScriptToExecuteOnDocumentCreatedAsync
- **Committed in:** acaf202

---

**Total deviations:** 1 auto-fixed, 5 checkpoint feedback fixes
**Impact on plan:** Better UX than planned. JS injection approach more robust than PreviewKeyDown alone.

## Issues Encountered
- 2 pre-existing WebViewEnvironmentCacheTests failures unrelated to this plan (already in phase 04 deferred items)

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Browser pane hosting complete, ready for phase 06 sidebar
- PaneKind model established for any future pane types
- Globe button provides clean entry point for browser pane creation

## Self-Check: PASSED

All 6 created/modified files verified on disk. Commit hash f117751 verified in git log.

---
*Phase: 05-pane-title-bars-and-browser-panes*
*Completed: 2026-03-09*
