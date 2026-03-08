---
phase: 02-tabbed-multiplexer-shell
plan: 02
subsystem: ui
tags: [tabs, tab-bar, winui, keyboard-shortcuts, pane-titles, cwd-tracking]

# Dependency graph
requires:
  - phase: 02-tabbed-multiplexer-shell
    plan: 01
    provides: TabStore tab collection state, PathHelper path display truncation
  - phase: 01-terminal-runtime-and-panes
    provides: WorkspaceViewModel, WorkspaceView, PaneCommandBindings, SessionManager, TerminalSurfaceBridge
provides:
  - TabViewModel: tab lifecycle orchestration with per-tab WorkspaceViewModel
  - TabBarView: tab bar UI with close buttons, add button, inline rename
  - TabCommandBindings: keyboard shortcuts for tab operations
  - Pane border titles: dynamic cwd display in pane borders
affects: [02-tabbed-multiplexer-shell]

# Tech tracking
tech-stack:
  added: []
  patterns: [tab-owns-workspace-viewmodel, visibility-toggled-tab-switching, pane-cwd-overlay]

key-files:
  created:
    - src/Wcmux.App/ViewModels/TabViewModel.cs
    - src/Wcmux.App/Views/TabBarView.xaml
    - src/Wcmux.App/Views/TabBarView.xaml.cs
    - src/Wcmux.App/Commands/TabCommandBindings.cs
  modified:
    - src/Wcmux.App/MainWindow.xaml
    - src/Wcmux.App/MainWindow.xaml.cs
    - src/Wcmux.App/Commands/PaneCommandBindings.cs
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs

key-decisions:
  - "Visibility-toggled WorkspaceViews for tab switching instead of creating/destroying"
  - "PaneCommandBindings detach/re-attach on tab switch to route to active workspace"
  - "Pane cwd tracked via SessionManager.SessionEventReceived rather than per-bridge subscription"
  - "Tab bar add button fires NewTabRequested event for MainWindow to handle view creation"

patterns-established:
  - "Tab-owns-WorkspaceViewModel: each tab has independent WorkspaceViewModel with its own pane sessions"
  - "Visibility toggle: tab switching hides/shows WorkspaceViews rather than recreating"
  - "Detach/re-attach: keyboard bindings re-bound to active tab's view model on switch"

requirements-completed: [TABS-01, TABS-02, TABS-03, SESS-03]

# Metrics
duration: 6min
completed: 2026-03-08
---

# Phase 2 Plan 2: Tab Shell Wiring Summary

**TabViewModel with per-tab WorkspaceView lifecycle, tab bar UI with close/add/rename, keyboard shortcuts, and pane cwd title overlays**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-08T03:09:23Z
- **Completed:** 2026-03-08T03:15:04Z
- **Tasks:** 3
- **Files modified:** 8

## Accomplishments
- TabViewModel orchestrates tab lifecycle with per-tab WorkspaceViewModel instances
- MainWindow refactored from single-workspace to multi-tab with visibility-toggled WorkspaceViews
- Tab bar renders tabs with labels, close buttons (X), add button (+), and inline rename on double-click
- Keyboard shortcuts: Ctrl+Shift+T (new), Ctrl+Tab/Ctrl+Shift+Tab (cycle), Ctrl+1-9 (index)
- Pane border titles show truncated cwd, updating dynamically via SessionCwdChangedEvent
- PaneCommandBindings gains Detach method for tab-switch re-binding
- All 139 existing tests pass, build succeeds

## Task Commits

Each task was committed atomically:

1. **Task 1: Create TabViewModel and refactor MainWindow for multi-tab** - `9e5b5e7` (feat)
2. **Task 2: Clean up tab bar view** - `f5a53bf` (refactor)
3. **Task 3: Add pane border titles with dynamic cwd tracking** - `fc39dc4` (feat)

## Files Created/Modified
- `src/Wcmux.App/ViewModels/TabViewModel.cs` - Tab lifecycle: create/switch/close/rename with per-tab WorkspaceViewModel
- `src/Wcmux.App/Views/TabBarView.xaml` - Tab bar layout with ScrollViewer, StackPanel, and add button
- `src/Wcmux.App/Views/TabBarView.xaml.cs` - Tab bar rendering, inline rename, close/switch handling
- `src/Wcmux.App/Commands/TabCommandBindings.cs` - Ctrl+Shift+T, Ctrl+Tab, Ctrl+1-9 keyboard shortcuts
- `src/Wcmux.App/MainWindow.xaml` - Grid with TabBarView (row 0) + TabContentArea (row 1)
- `src/Wcmux.App/MainWindow.xaml.cs` - Multi-tab initialization, tab switching, WorkspaceView lifecycle
- `src/Wcmux.App/Commands/PaneCommandBindings.cs` - Added Detach method for tab-switch re-binding
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - Pane cwd title overlay, FocusPaneAsync public method

## Decisions Made
- Visibility-toggled WorkspaceViews for tab switching preserves inactive tab state without re-creation overhead
- PaneCommandBindings detach/re-attach approach chosen over per-tab binding sets to keep a single accelerator collection on the root element
- SessionManager.SessionEventReceived used for cwd tracking (approach b from plan) since WorkspaceView already accesses SessionManager through the view model
- Tab bar add button fires event to MainWindow rather than creating views directly, keeping view creation centralized

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] IReadOnlyList lacks IndexOf method**
- **Found during:** Task 1 (TabCommandBindings compilation)
- **Issue:** TabStore.TabOrder returns IReadOnlyList<string> which doesn't have IndexOf
- **Fix:** Added static FindIndex helper method using loop iteration
- **Files modified:** src/Wcmux.App/Commands/TabCommandBindings.cs
- **Verification:** Build succeeds
- **Committed in:** 9e5b5e7 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Minor API compatibility fix, no scope change.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Full tabbed multiplexer shell operational with tab bar, keyboard shortcuts, and pane titles
- Ready for session persistence, notification, or further tab polish features
- All 139 tests green, no regressions

## Self-Check: PASSED

- All 4 created files exist
- All 3 commits verified in git log
- Line counts meet plan minimums (TabViewModel: 114>=80, TabBarView.cs: 189>=60, TabCommandBindings: 128>=30)
- Must-have contains verified (class TabViewModel, class TabBarView, class TabCommandBindings)
- 139 tests green, 0 failures

---
*Phase: 02-tabbed-multiplexer-shell*
*Completed: 2026-03-08*
