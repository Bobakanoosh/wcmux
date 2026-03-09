---
phase: 07-pane-interaction
plan: 01
subsystem: layout
tags: [binary-tree, reducer, immutable, pane-swap, pane-move, split-ratio]

requires:
  - phase: 04-custom-chrome-and-webview2-foundation
    provides: LayoutReducer with SplitPane, ClosePane, ResizePane, FindDirectionalFocus
provides:
  - SetSplitRatio pure reducer function with clamping
  - SwapPanes pure reducer function preserving tree structure
  - MovePaneToTarget pure reducer function (detach + reinsert)
  - LayoutStore.SetSplitRatio, SwapActivePane, MovePane store wrappers
  - WorkspaceViewModel.SetSplitRatio, SwapActivePane, MovePane delegates
affects: [07-pane-interaction]

tech-stack:
  added: []
  patterns: [FindLeaf helper reuse, ClosePane+InsertPaneAtTarget composition for move]

key-files:
  created:
    - tests/Wcmux.Tests/Layout/PaneInteractionTests.cs
  modified:
    - src/Wcmux.Core/Layout/LayoutReducer.cs
    - src/Wcmux.Core/Layout/LayoutStore.cs
    - src/Wcmux.App/ViewModels/WorkspaceViewModel.cs

key-decisions:
  - "MovePaneToTarget uses ClosePane + InsertPaneAtTarget composition rather than in-place tree surgery"
  - "FindLeaf private helper shared between SwapPanes and MovePaneToTarget to avoid duplication"
  - "SwapPanes uses two-pass approach: find both leaves, then walk tree replacing content"

patterns-established:
  - "InsertPaneAtTarget helper: wraps target leaf in new SplitNode with source as sibling"
  - "Direction-to-axis mapping: Left/Right=Vertical, Up/Down=Horizontal (consistent with ResizePane)"

requirements-completed: [PINT-01, PINT-02, PINT-03]

duration: 5min
completed: 2026-03-09
---

# Phase 7 Plan 1: Pane Interaction Reducer Functions Summary

**TDD-driven SetSplitRatio, SwapPanes, and MovePaneToTarget reducer functions with LayoutStore/ViewModel wrappers**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-09T22:34:11Z
- **Completed:** 2026-03-09T22:39:28Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Three pure reducer functions (SetSplitRatio, SwapPanes, MovePaneToTarget) with full test coverage
- 16 unit tests covering happy paths, clamping, edge cases (same pane, non-existent, only pane)
- LayoutStore and WorkspaceViewModel wrappers ready for UI wiring in plan 07-02

## Task Commits

Each task was committed atomically:

1. **Task 1: TDD SetSplitRatio, SwapPanes, MovePaneToTarget** - `e27c44d` (test: RED), `cd2f3de` (feat: GREEN)
2. **Task 2: LayoutStore and WorkspaceViewModel wrappers** - `8748b86` (feat)

## Files Created/Modified
- `tests/Wcmux.Tests/Layout/PaneInteractionTests.cs` - 16 xUnit tests for all three reducer operations
- `src/Wcmux.Core/Layout/LayoutReducer.cs` - SetSplitRatio, SwapPanes, MovePaneToTarget + FindLeaf, InsertPaneAtTarget helpers
- `src/Wcmux.Core/Layout/LayoutStore.cs` - SetSplitRatio, SwapActivePane, MovePane store methods
- `src/Wcmux.App/ViewModels/WorkspaceViewModel.cs` - SwapActivePane, SetSplitRatio, MovePane delegates

## Decisions Made
- MovePaneToTarget uses ClosePane + InsertPaneAtTarget composition rather than in-place tree surgery for simplicity and reuse of existing ClosePane logic
- FindLeaf private helper shared between SwapPanes and MovePaneToTarget to avoid leaf-finding duplication
- SwapPanes uses two-pass approach: find both leaves first, then walk tree replacing content (avoids mid-walk confusion)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Running Wcmux.App instance locks Wcmux.Core.dll preventing App project rebuild; worked around by building Core independently and running tests with BuildProjectReferences=false
- 2 pre-existing WebViewEnvironmentCache test failures (require WebView2 runtime context) unrelated to changes

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All three reducer functions tested and working, ready for UI wiring in plan 07-02
- LayoutStore and WorkspaceViewModel expose the new operations for keyboard shortcuts and drag-drop

---
*Phase: 07-pane-interaction*
*Completed: 2026-03-09*
