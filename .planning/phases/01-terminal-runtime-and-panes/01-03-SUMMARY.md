---
phase: 01-terminal-runtime-and-panes
plan: 03
subsystem: layout
tags: [split-tree, reducer, pane-focus, resize, winui, xterm]

# Dependency graph
requires:
  - phase: 01-terminal-runtime-and-panes-01
    provides: ConPTY session runtime and SessionManager
  - phase: 01-terminal-runtime-and-panes-02
    provides: WebView2 terminal surface and TerminalSurfaceBridge
provides:
  - Binary split-tree layout model with pure reducer transitions
  - LayoutStore with observable state, focus history, and pane rect tracking
  - WorkspaceViewModel for split, close, focus, and resize orchestration
  - Keyboard bindings for all pane commands
  - Mouse focus and split affordance per pane
  - 51 layout tests covering reducer math, split commands, focus, and resize
affects: [02-tab-workspace, pane-persistence, tab-drag-drop]

# Tech tracking
tech-stack:
  added: []
  patterns: [reducer-owned-layout, immutable-split-tree, geometric-focus]

key-files:
  created:
    - src/Wcmux.Core/Layout/LayoutNode.cs
    - src/Wcmux.Core/Layout/LayoutReducer.cs
    - src/Wcmux.Core/Layout/LayoutStore.cs
    - src/Wcmux.App/ViewModels/WorkspaceViewModel.cs
    - src/Wcmux.App/Views/WorkspaceView.xaml
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs
    - src/Wcmux.App/Commands/PaneCommandBindings.cs
    - tests/Wcmux.Tests/Layout/LayoutReducerTests.cs
    - tests/Wcmux.Tests/Layout/SplitCommandsTests.cs
    - tests/Wcmux.Tests/Layout/PaneFocusAndResizeTests.cs
  modified:
    - src/Wcmux.App/MainWindow.xaml
    - src/Wcmux.App/MainWindow.xaml.cs

key-decisions:
  - "Immutable record-based split tree with pure reducer transitions for deterministic layout behavior"
  - "Geometric directional focus using computed pane rectangles rather than tree order"
  - "Ratio-based resize on ancestor split nodes with min/max clamping (0.1-0.9)"

patterns-established:
  - "Reducer-style layout: pure state transitions produce new tree instances; UI renders but never defines layout"
  - "Focus history stack: bounded list tracks pane visits for close-restore behavior"
  - "Keyboard command routing: PaneCommandBindings attaches accelerators that route through ViewModel to LayoutStore"

requirements-completed: [LAYT-01, LAYT-02, LAYT-03]

# Metrics
duration: 7min
completed: 2026-03-07
---

# Phase 1 Plan 3: Split Tree, Pane Commands, Focus, And Resize Summary

**Reducer-style binary split tree with directional keyboard focus, ratio-clamped resize, and deterministic close behavior across uneven pane layouts**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-07T03:07:40Z
- **Completed:** 2026-03-07T03:14:41Z
- **Tasks:** 3
- **Files modified:** 12

## Accomplishments
- Pure split-tree layout model with immutable transitions for split, close, focus, and resize
- Observable LayoutStore as single source of truth for pane state, with UI widgets as renderers only
- WorkspaceViewModel orchestrating session creation with cwd inheritance and pane lifecycle
- Full keyboard binding set: Ctrl+Shift+H/V split, Ctrl+Shift+arrows focus, Ctrl+Alt+arrows resize, Ctrl+Shift+W close
- Mouse click focus and top-right split affordance on each pane
- 51 layout-specific tests plus 94 total tests passing across the full suite

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement the split-tree reducer and layout store** - `82c6f52` (feat)
2. **Task 2: Wire pane commands through the app shell** - `9d4c78e` (feat)
3. **Task 3: Add layout and pane interaction coverage** - `63b6ee2` (test)

## Files Created/Modified
- `src/Wcmux.Core/Layout/LayoutNode.cs` - Binary split-tree node types (LeafNode, SplitNode, PaneRect)
- `src/Wcmux.Core/Layout/LayoutReducer.cs` - Pure state transitions for split, close, focus, resize
- `src/Wcmux.Core/Layout/LayoutStore.cs` - Observable layout state with focus history and pane rect tracking
- `src/Wcmux.App/ViewModels/WorkspaceViewModel.cs` - App-shell orchestration for pane commands and session creation
- `src/Wcmux.App/Views/WorkspaceView.xaml` - Split tree renderer container
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - Dynamic pane positioning from computed rects
- `src/Wcmux.App/Commands/PaneCommandBindings.cs` - Keyboard accelerators for all pane commands
- `src/Wcmux.App/MainWindow.xaml` - Updated to host WorkspaceView instead of single TerminalPaneView
- `src/Wcmux.App/MainWindow.xaml.cs` - Updated to use WorkspaceViewModel and PaneCommandBindings
- `tests/Wcmux.Tests/Layout/LayoutReducerTests.cs` - 21 unit tests for reducer transitions
- `tests/Wcmux.Tests/Layout/SplitCommandsTests.cs` - 9 tests for split command semantics
- `tests/Wcmux.Tests/Layout/PaneFocusAndResizeTests.cs` - 21 integration tests for focus and resize

## Decisions Made
- Used immutable record types for split-tree nodes to ensure deterministic transitions
- Geometric directional focus using pane rectangles (not tree order) for correct behavior in uneven layouts
- Resize operates on ancestor split ratios with 0.1-0.9 clamping, not on leaf panes directly
- Focus history bounded at 50 entries for close-restore fallback
- Cell-based minimum sizes (20 columns, 6 rows) enforced in reducer, not in pixels

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 1 complete: ConPTY runtime, terminal surface, and pane layout all landed
- Layout model is cleanly separated from UI, ready for Phase 2 tab support
- 94 total tests passing across all three plans
- LayoutStore provides the foundation for tab-level workspace management

## Self-Check: PASSED

---
*Phase: 01-terminal-runtime-and-panes*
*Completed: 2026-03-07*
