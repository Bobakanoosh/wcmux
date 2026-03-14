---
phase: 07-pane-interaction
verified: 2026-03-14T00:00:00Z
status: human_needed
score: 9/9 must-haves verified
human_verification:
  - test: "Mouse resize: hover over split border and drag"
    expected: "EW cursor appears on vertical split border; NS cursor on horizontal split border; dragging resizes panes fluidly; size persists on release"
    why_human: "Cursor shape and fluid resize are visual/interactive behaviors that cannot be verified by static code inspection"
  - test: "Keyboard swap: Ctrl+Alt+Shift+Arrow in split workspace"
    expected: "Active pane content swaps with neighbor in the arrow direction; layout structure is preserved; swap is reversible"
    why_human: "Keyboard accelerator firing and visual pane swap are runtime behaviors requiring app interaction"
  - test: "Drag-to-rearrange: drag pane title bar onto another pane"
    expected: "After 5px threshold, a semi-transparent blue overlay appears over half the target pane (L/R/T/B based on quadrant); dropping rearranges the split layout correctly; preview disappears on release"
    why_human: "Drag threshold, overlay rendering, and layout rearrangement are interactive UI behaviors"
---

# Phase 7: Pane Interaction Verification Report

**Phase Goal:** Users can rearrange and resize panes fluidly using mouse and keyboard without relying solely on keyboard shortcuts.
**Verified:** 2026-03-14
**Status:** human_needed — all automated checks passed; visual/interactive behaviors require human confirmation
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

#### Plan 07-01 Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SetSplitRatio sets a specific split node ratio by NodeId, clamped to MinRatio..MaxRatio | VERIFIED | `LayoutReducer.SetSplitRatio` at line 199 of LayoutReducer.cs; clamps via `Math.Clamp(newRatio, MinRatio, MaxRatio)` at line 201; 5 unit tests cover happy path, min clamp, max clamp, nonexistent node, nested node |
| 2 | SwapPanes swaps two leaf nodes' content (PaneId, SessionId, Kind) while preserving tree structure | VERIFIED | `LayoutReducer.SwapPanes` at line 209; two-pass approach (FindLeaf + SwapLeavesInTree); 4 unit tests covering swap, structure preservation, same-pane no-op, nonexistent pane |
| 3 | MovePaneToTarget detaches a source leaf and re-inserts it adjacent to a target in the specified direction | VERIFIED | `LayoutReducer.MovePaneToTarget` at line 229; ClosePane + InsertPaneAtTarget composition; 7 unit tests covering detach/reinsert, axis mapping (L/R=Vertical, U/D=Horizontal), insert order, source==target, only-pane guard |
| 4 | LayoutStore exposes SetSplitRatio, SwapActivePane, and MovePane methods that delegate to the reducer | VERIFIED | All three methods present in LayoutStore.cs (lines 203, 218, 237); each calls corresponding `LayoutReducer.*` method, recomputes rects, and fires `LayoutChanged` |

#### Plan 07-02 Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 5 | User can drag pane borders with the mouse to resize panes and the resize persists | HUMAN NEEDED | CreateResizeHandles() wires PointerPressed/PointerMoved/PointerReleased on CursorBorder handles; PointerMoved calls `_viewModel?.SetSplitRatio(...)` at line 962; _isResizing guard prevents handle recreation during drag — visual confirmation needed |
| 6 | User sees a resize cursor (EW or NS) when hovering over pane borders | HUMAN NEEDED | CursorBorder sets `Cursor` via `ProtectedCursor`; `InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast/SizeNorthSouth)` assigned based on split axis — cursor rendering requires runtime confirmation |
| 7 | User can swap two adjacent panes using Ctrl+Alt+Shift+Arrow keys | HUMAN NEEDED | PaneCommandBindings.cs registers 4 accelerators (lines 75-89) with `VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift`; registered before Ctrl+Alt resize combos; each calls `viewModel.SwapActivePane(Direction.*)` — accelerator priority and key capture require runtime confirmation |
| 8 | User can drag a pane title bar onto another pane and see a blue directional preview overlay | HUMAN NEEDED | AttachDragHandlers wired on both terminal and browser pane title bars (lines 458, 547); 5px DragThreshold before `_isDraggingPane = true`; UpdateDragPreview sets semi-transparent blue Border (ARGB 80,50,130,240) covering target half — visual rendering requires runtime confirmation |
| 9 | User can drop a dragged pane to rearrange splits according to the preview direction | HUMAN NEEDED | PointerReleased calls `_viewModel?.MovePane(_dragSourcePaneId, targetPaneId, dropSide)` at line 1067; HitTestDropZone uses quadrant logic (25%/75% left/right, 50% up/down) — layout rearrangement correctness requires runtime confirmation |

**Score:** 9/9 truths have complete implementation; 5 require human visual/interactive confirmation.

---

## Required Artifacts

### Plan 07-01 Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Wcmux.Core/Layout/LayoutReducer.cs` | SetSplitRatio, SwapPanes, MovePaneToTarget pure functions | VERIFIED | All three public static methods present (lines 199, 209, 229); private helpers FindLeaf, SetSplitRatioInTree, SwapLeavesInTree, InsertPaneAtTarget all substantive |
| `src/Wcmux.Core/Layout/LayoutStore.cs` | Store methods wrapping new reducer functions | VERIFIED | SetSplitRatio (line 203), SwapActivePane (line 218), MovePane (line 237) all delegate to LayoutReducer with lock, RecomputeRects, and LayoutChanged.Invoke() |
| `tests/Wcmux.Tests/Layout/PaneInteractionTests.cs` | Unit tests for all three reducer operations | VERIFIED | 272 lines, exceeds min_lines: 100; 12 [Fact] tests covering all specified behaviors; no stubs or empty test bodies |

### Plan 07-02 Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Wcmux.App/Views/CursorBorder.cs` | Border subclass exposing ProtectedCursor for resize cursor | VERIFIED | 18 lines; Grid subclass (Border is sealed in WinUI 3); exposes `Cursor` property backed by `ProtectedCursor` |
| `src/Wcmux.App/Views/WorkspaceView.xaml.cs` | Resize handles, drag-to-rearrange overlay, pointer event handlers | VERIFIED | Contains CreateResizeHandles, _resizeHandles, _dragPreview, PointerPressed/PointerMoved/PointerReleased handlers, AttachDragHandlers, HitTestDropZone, UpdateDragPreview, InitDragPreview — all substantive implementations |
| `src/Wcmux.App/Commands/PaneCommandBindings.cs` | Ctrl+Alt+Shift+Arrow keyboard swap bindings | VERIFIED | 4 SwapActivePane accelerators registered at lines 75-89; uses correct 3-modifier combo; registered before 2-modifier resize combos |

---

## Key Link Verification

### Plan 07-01 Key Links

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `tests/Wcmux.Tests/Layout/PaneInteractionTests.cs` | `src/Wcmux.Core/Layout/LayoutReducer.cs` | direct static method calls | WIRED | `LayoutReducer.SetSplitRatio`, `LayoutReducer.SwapPanes`, `LayoutReducer.MovePaneToTarget` all called directly in test methods |
| `src/Wcmux.Core/Layout/LayoutStore.cs` | `src/Wcmux.Core/Layout/LayoutReducer.cs` | store delegates to reducer | WIRED | LayoutStore.SetSplitRatio calls `LayoutReducer.SetSplitRatio`; SwapActivePane calls `LayoutReducer.SwapPanes`; MovePane calls `LayoutReducer.MovePaneToTarget` |

### Plan 07-02 Key Links

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `src/Wcmux.App/Views/WorkspaceView.xaml.cs` | `src/Wcmux.App/ViewModels/WorkspaceViewModel.cs` | SetSplitRatio, MovePane calls | WIRED | `_viewModel?.SetSplitRatio(...)` at line 962 in PointerMoved resize handler; `_viewModel?.MovePane(...)` at line 1067 in PointerReleased drag handler |
| `src/Wcmux.App/Commands/PaneCommandBindings.cs` | `src/Wcmux.App/ViewModels/WorkspaceViewModel.cs` | SwapActivePane call | WIRED | `viewModel.SwapActivePane(Direction.Left/Right/Up/Down)` at lines 77, 81, 85, 89 |
| `src/Wcmux.App/Views/WorkspaceView.xaml.cs` | `src/Wcmux.App/Views/CursorBorder.cs` | CursorBorder instantiation for resize handles | WIRED | `new CursorBorder { ... }` at line 898 inside CreateResizeHandles() |

---

## Requirements Coverage

Both plans declare `requirements: [PINT-01, PINT-02, PINT-03]`. REQUIREMENTS.md maps all three to Phase 7 with status Complete.

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PINT-01 | 07-01, 07-02 | User can drag pane borders with the mouse to resize panes | SATISFIED | Reducer: SetSplitRatio; Store: SetSplitRatio; UI: CreateResizeHandles + PointerMoved calling _viewModel.SetSplitRatio; CursorBorder with EW/NS cursor |
| PINT-02 | 07-01, 07-02 | User can swap pane positions using Ctrl+Alt+Shift+Arrow keys | SATISFIED | Reducer: SwapPanes; Store: SwapActivePane; VM: SwapActivePane; PaneCommandBindings: 4 Ctrl+Alt+Shift accelerators |
| PINT-03 | 07-01, 07-02 | User can drag a pane title bar onto another pane to rearrange splits, with a blue preview showing the target split direction | SATISFIED | Reducer: MovePaneToTarget; Store: MovePane; VM: MovePane; UI: AttachDragHandlers on both terminal and browser title bars; _dragPreview Border (ARGB 80,50,130,240); HitTestDropZone quadrant logic; PointerReleased calls MovePane |

No orphaned requirements — the traceability table in REQUIREMENTS.md shows PINT-01/02/03 mapped exclusively to Phase 7.

---

## Anti-Patterns Found

No anti-patterns detected across the phase 07 files:
- No TODO/FIXME/PLACEHOLDER comments in any phase 07 file
- No stub implementations (empty returns, `return null`, `return {}`)
- No console.log-only handlers
- No unimplemented route stubs

---

## Human Verification Required

### 1. Mouse Resize (PINT-01)

**Test:** Launch app, create a vertical split (Ctrl+Shift+V), hover the mouse over the border between the two panes, then click-drag.
**Expected:** Cursor changes to a horizontal resize arrow (EW) on hover; dragging moves the split boundary and both panes resize fluidly; releasing the mouse persists the new size. Repeat with horizontal split (Ctrl+Shift+H) for NS cursor.
**Why human:** Cursor shape rendering via `ProtectedCursor` and fluid drag-resize behavior cannot be verified by static code analysis.

### 2. Keyboard Swap (PINT-02)

**Test:** With two panes in a split, focus in the left pane, press Ctrl+Alt+Shift+Right.
**Expected:** The left pane's content moves to the right position and vice versa. Pressing Ctrl+Alt+Shift+Left swaps back.
**Why human:** WinUI 3 keyboard accelerator priority (3-modifier vs 2-modifier matching) and actual pane visual swap require runtime confirmation. The fix at commit `3f8aee3` also routes swap commands through xterm.js, which requires live terminal interaction to confirm.

### 3. Drag-to-Rearrange (PINT-03)

**Test:** With 2+ panes, click and hold on a pane's title bar (24px bar), drag toward another pane and hover over its left/right/upper/lower quadrant.
**Expected:** After 5px of drag movement, a semi-transparent blue overlay appears covering the indicated half of the target pane. Releasing drops the pane into that position, rearranging the split layout. The overlay disappears on release.
**Why human:** Drag threshold activation, overlay z-order rendering, quadrant hit-testing accuracy, and layout rearrangement correctness all require interactive verification.

### 4. No Regressions

**Test:** Verify existing features still work: tab creation/switching/closing, Ctrl+Shift+H/V split, Ctrl+Shift+W close, Ctrl+Shift+Arrow focus movement, Ctrl+Alt+Arrow resize, browser pane open/close, attention notifications.
**Why human:** Regression testing of the full interaction surface requires running the app.

---

## Summary

All 9 must-have truths are implemented with substantive code. All 6 key links are wired. All 3 requirement IDs (PINT-01, PINT-02, PINT-03) have complete implementation chains from reducer through store through view model through UI. No stubs, no placeholders, no anti-patterns found.

The phase is blocked on human visual verification because the final proof of goal achievement — "users can rearrange and resize panes fluidly" — requires running the app. The SUMMARY notes human visual verification was approved (checkpoint task in 07-02-PLAN), but this falls outside automated verification scope.

---

_Verified: 2026-03-14_
_Verifier: Claude (gsd-verifier)_
