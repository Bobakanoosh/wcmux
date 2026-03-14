---
phase: 07-pane-interaction
plan: 02
subsystem: ui
tags: [winui3, winappsdk, pointer-events, keyboard-accelerators, drag-drop, resize]

# Dependency graph
requires:
  - phase: 07-01
    provides: SetSplitRatio, SwapActivePane, MovePane on LayoutStore and WorkspaceViewModel
provides:
  - Mouse resize handles at all split boundaries with EW/NS cursor feedback
  - Ctrl+Alt+Shift+Arrow keyboard swap accelerators
  - Drag-to-rearrange title bar drag with blue directional preview overlay
affects: [future-pane-phases]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - CursorBorder (Grid subclass) exposes ProtectedCursor for custom pointer shapes
    - CollectSplitBoundaries recursive tree walk mirrors ComputeRectsRecursive pattern
    - Drag threshold (5px) before committing to drag mode prevents accidental drags
    - Resize handles skipped during active drag (_isResizing guard) to preserve pointer capture
    - Drag preview Border kept last in RootContainer.Children for correct z-order

key-files:
  created:
    - src/Wcmux.App/Views/CursorBorder.cs
  modified:
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs
    - src/Wcmux.App/Commands/PaneCommandBindings.cs

key-decisions:
  - "Border is sealed in WinUI 3 - used Grid subclass for CursorBorder to expose ProtectedCursor"
  - "Swap accelerators registered before resize in PaneCommandBindings so 3-modifier Ctrl+Alt+Shift matches before 2-modifier Ctrl+Alt"
  - "_isResizing guard prevents CreateResizeHandles() from running during active resize drag"

patterns-established:
  - "CursorBorder: Grid subclass pattern for custom cursors on interactive regions"
  - "Drag preview: last child in RootContainer.Children ensures correct z-order above all panes and handles"
  - "Pointer capture: CapturePointer on PointerPressed + ReleasePointerCapture on PointerReleased/CaptureLost"

requirements-completed: [PINT-01, PINT-02, PINT-03]

# Metrics
duration: 15min
completed: 2026-03-14
---

# Phase 7 Plan 02: Pane Interaction UI Summary

**Mouse resize handles, Ctrl+Alt+Shift+Arrow keyboard swap, and drag-to-rearrange with blue directional preview connecting LayoutReducer to visual interactions**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-14T00:00:00Z
- **Completed:** 2026-03-14T00:15:00Z
- **Tasks:** 2 auto + 1 checkpoint (human-verify pending)
- **Files modified:** 3

## Accomplishments
- CursorBorder Grid subclass exposes ProtectedCursor for EW/NS resize cursors on split boundary handles
- Mouse resize handles at every split boundary using CollectSplitBoundaries tree walk, with pointer capture and ratio clamping (0.1-0.9)
- Ctrl+Alt+Shift+Arrow keyboard accelerators for swapping the active pane with its neighbor in any direction
- Drag-to-rearrange: title bar drag with 5px threshold, HitTestDropZone with quadrant logic (25%/75% left-right, 50% up-down), blue semi-transparent overlay showing drop target half
- Both browser pane and terminal pane title bars support drag-to-rearrange

## Task Commits

Each task was committed atomically:

1. **Task 1: CursorBorder, mouse resize handles, and keyboard swap bindings** - `afcd2c4` (feat)
2. **Task 2: Drag-to-rearrange with blue directional preview overlay** - `b6fd1f0` (feat)
3. **Fix: Reorder swap accelerators and add isResizing guard** - `a370dba` (fix)

## Files Created/Modified
- `src/Wcmux.App/Views/CursorBorder.cs` - Grid subclass exposing ProtectedCursor for resize cursor shapes
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - Resize handles (CollectSplitBoundaries, CreateResizeHandles), drag-to-rearrange (AttachDragHandlers, HitTestDropZone, UpdateDragPreview, InitDragPreview, ResetDragState)
- `src/Wcmux.App/Commands/PaneCommandBindings.cs` - Ctrl+Alt+Shift+Arrow swap accelerators (registered before Ctrl+Alt resize for correct priority)

## Decisions Made
- Used Grid subclass for CursorBorder because Border is sealed in WinUI 3; Grid inherits ProtectedCursor from UIElement
- Swap accelerators must be registered before resize (Ctrl+Alt) accelerators in WinUI's accelerator list so the more-specific 3-modifier combo matches first
- `_isResizing` guard added to `RenderLayoutAsync()` prevents `CreateResizeHandles()` from destroying pointer capture mid-drag

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Reordered swap accelerators before resize accelerators**
- **Found during:** Post-commit review (prior to this plan execution)
- **Issue:** Swap accelerators (Ctrl+Alt+Shift) were registered after resize (Ctrl+Alt), causing the 2-modifier combo to match before the 3-modifier, making swap unreachable
- **Fix:** Moved Ctrl+Alt+Shift accelerators to be registered before Ctrl+Alt resize accelerators with comment explaining the ordering requirement
- **Files modified:** src/Wcmux.App/Commands/PaneCommandBindings.cs
- **Verification:** Build passes, ordering is logically correct
- **Committed in:** `a370dba`

**2. [Rule 1 - Bug] Added `!_isResizing` guard to protect resize drag pointer capture**
- **Found during:** Post-commit review
- **Issue:** `RenderLayoutAsync()` always called `CreateResizeHandles()`, which removes and recreates all handle UIElements. During an active mouse drag, this destroyed the element that held pointer capture, breaking drag-to-resize
- **Fix:** Added `if (!_isResizing)` guard before `CreateResizeHandles()` call
- **Files modified:** src/Wcmux.App/Views/WorkspaceView.xaml.cs
- **Verification:** Build passes; guard is logically necessary
- **Committed in:** `a370dba`

---

**Total deviations:** 2 auto-fixed (both Rule 1 - bugs)
**Impact on plan:** Both fixes necessary for correct operation. No scope creep.

## Issues Encountered
- 2 pre-existing test failures in `WebViewEnvironmentCacheTests` (require display context, unrelated to this plan)

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three PINT requirements implemented: mouse resize (PINT-01), keyboard swap (PINT-02), drag-to-rearrange (PINT-03)
- Awaiting Task 3 human visual verification checkpoint before marking phase complete
- No blockers

---
*Phase: 07-pane-interaction*
*Completed: 2026-03-14*
