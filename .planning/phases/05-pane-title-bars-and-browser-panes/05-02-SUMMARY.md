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
  patterns: [pane-kind-dispatch-in-view, sentinel-session-id-for-browser-panes, preview-key-down-shortcut-interception]

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
  - "Used PreviewKeyDown instead of AcceleratorKeyPressed for shortcut interception (WinUI3 WebView2 does not expose AcceleratorKeyPressed)"
  - "Browser panes use sentinel session ID prefixed with 'browser:' to avoid ConPTY session creation"
  - "Browser title bar shows static 'browser' label with only close button (no split buttons)"

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
- **Tasks:** 1 (of 2 total; Task 2 is human-verify checkpoint)
- **Files modified:** 6

## Accomplishments
- BrowserPaneView UserControl with address bar (back, forward, reload, URL box, go) and WebView2 content area
- Browser panes use shared WebViewEnvironmentCache -- no separate browser process groups
- Globe button in pane title bar opens browser pane via vertical split
- PaneKind-aware pane creation and rendering in WorkspaceView
- App-level keyboard shortcuts intercepted via PreviewKeyDown before WebView2 consumes them
- Browser panes cleanly close via title bar X button with no session teardown needed

## Task Commits

Each task was committed atomically:

1. **Task 1: BrowserPaneView, browser split flow, and PaneKind-aware rendering** - `f117751` (feat)

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

**1. [Rule 1 - Bug] Replaced AcceleratorKeyPressed with PreviewKeyDown**
- **Found during:** Task 1 (build verification)
- **Issue:** WinUI3 WebView2 control does not expose AcceleratorKeyPressed event
- **Fix:** Used PreviewKeyDown on the UserControl which fires in tunneling phase before WebView2 processes keys
- **Files modified:** src/Wcmux.App/Views/BrowserPaneView.xaml.cs
- **Verification:** Build succeeds, shortcut pattern matches existing terminal pane approach
- **Committed in:** f117751

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** API surface difference in WinUI3 WebView2. PreviewKeyDown achieves same result. No scope creep.

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
