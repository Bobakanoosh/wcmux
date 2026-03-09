# Phase 7: Pane Interaction - Research

**Researched:** 2026-03-09
**Domain:** WinUI 3 pointer events, drag-and-drop, layout tree manipulation
**Confidence:** HIGH

## Summary

Phase 7 adds three distinct pane interaction features to the existing binary split-tree layout: (1) mouse-driven border resize via pointer capture, (2) keyboard pane swapping via Ctrl+Alt+Shift+Arrow, and (3) drag-to-rearrange with a directional preview overlay. All three features build on the existing immutable `LayoutNode` tree, `LayoutReducer` pure functions, and `LayoutStore` observable state. The architecture already has `ComputePaneRects`, `ResizePane`, and directional focus -- new operations (swap, move/rearrange) follow the same reducer pattern.

The key challenge is that pane views use a `Grid` with margin-based absolute positioning (not a Canvas), and panes contain WebView2 instances that swallow pointer events. Drag resize handles and drop overlays must be added as sibling elements in `RootContainer` that sit above the pane content layer. WinUI 3's `CapturePointer` API is the correct mechanism for drag operations; the built-in `AllowDrop`/`DragOver`/`Drop` API is NOT appropriate here because we are rearranging internal layout elements, not handling external data transfer.

**Primary recommendation:** Implement all three features using pointer events (PointerPressed/PointerMoved/PointerReleased + CapturePointer) on invisible hit-test elements overlaid at split boundaries and pane title bars. Add `SwapPanes` and `MovePaneToTarget` reducer functions to LayoutReducer. Use a semi-transparent blue Canvas overlay for the drag-to-rearrange directional preview.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PINT-01 | User can drag pane borders with the mouse to resize panes | Pointer capture pattern on invisible Border elements at split boundaries; updates SplitNode.Ratio via existing ResizePane or direct ratio computation |
| PINT-02 | User can swap pane positions using Ctrl+Alt+Shift+Arrow keys | New `SwapPanes` reducer function + keyboard accelerator in PaneCommandBindings; swaps LeafNode positions in the tree |
| PINT-03 | User can drag a pane title bar onto another pane to rearrange splits, with a blue preview showing the target split direction | Pointer capture on title bar for drag initiation; overlay Canvas for directional preview; new `MovePaneToTarget` reducer function to detach and re-insert a leaf |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| WinUI 3 / WinAppSDK | 1.5+ | UI framework | Already in use; provides PointerPressed/Moved/Released, CapturePointer, Canvas overlay |
| Microsoft.UI.Xaml.Input | (bundled) | Pointer events, KeyboardAccelerator | Already used for all keyboard shortcuts and pointer handling |
| Microsoft.UI.Input | (bundled) | InputSystemCursor for resize cursors | ProtectedCursor API for cursor shape changes |

### Supporting
No additional libraries needed. All features are implementable with existing WinUI 3 APIs.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom pointer-based resize | Telerik RadGridSplitter | Adds third-party dependency for something achievable with ~100 lines of pointer handling |
| Custom drag-to-rearrange | WinUI DragDrop API (AllowDrop) | DragDrop API is designed for data transfer between apps, not internal layout rearrangement; produces wrong UX (drag adorner, clipboard semantics) |
| Pointer events | ManipulationDelta events | ManipulationDelta adds complexity (inertia, gesture recognition) without benefit for simple drag operations |

## Architecture Patterns

### Recommended Project Structure
```
src/
  Wcmux.Core/
    Layout/
      LayoutReducer.cs    # Add SwapPanes, MovePaneToTarget
      LayoutStore.cs      # Add SwapActivePane, MovePane methods
      LayoutNode.cs       # No changes needed
  Wcmux.App/
    Views/
      WorkspaceView.xaml.cs  # Add resize handles, drag-to-rearrange overlay, pointer handlers
    Commands/
      PaneCommandBindings.cs # Add Ctrl+Alt+Shift+Arrow swap bindings

tests/
  Wcmux.Tests/
    Layout/
      LayoutReducerTests.cs  # Add SwapPanes, MovePaneToTarget tests
      PaneSwapTests.cs       # New: dedicated swap test file
```

### Pattern 1: Invisible Resize Handles via Pointer Capture
**What:** Place thin (4-6px) transparent Border elements at computed split boundaries. On PointerPressed, capture the pointer and track delta movement. On PointerMoved, compute new ratio from mouse position relative to parent split dimensions. On PointerReleased, finalize ratio.
**When to use:** PINT-01 (mouse border resize)
**Example:**
```csharp
// Source: Microsoft Learn - Handle pointer input + CapturePointer docs
// Create invisible hit-test strip at each split boundary
private Border CreateResizeHandle(SplitNode split, PaneRect firstRect, PaneRect secondRect)
{
    bool isVertical = split.Axis == SplitAxis.Vertical;
    var handle = new CursorBorder // subclass to access ProtectedCursor
    {
        Width = isVertical ? 6 : firstRect.Width,
        Height = isVertical ? firstRect.Height : 6,
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
        // Must be non-null background for hit testing
        Tag = split.NodeId,
    };
    handle.Cursor = InputSystemCursor.Create(
        isVertical ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);

    double startPos = 0;
    handle.PointerPressed += (s, e) =>
    {
        handle.CapturePointer(e.Pointer);
        var pt = e.GetCurrentPoint(RootContainer);
        startPos = isVertical ? pt.Position.X : pt.Position.Y;
        e.Handled = true;
    };
    handle.PointerMoved += (s, e) =>
    {
        if (!e.GetCurrentPoint(RootContainer).Properties.IsLeftButtonPressed) return;
        var pt = e.GetCurrentPoint(RootContainer);
        double currentPos = isVertical ? pt.Position.X : pt.Position.Y;
        // Compute new ratio from absolute position
        double containerStart = isVertical ? firstRect.X : firstRect.Y;
        double containerSize = isVertical
            ? firstRect.Width + secondRect.Width
            : firstRect.Height + secondRect.Height;
        double newRatio = Math.Clamp(
            (currentPos - containerStart) / containerSize,
            LayoutReducer.MinRatio, LayoutReducer.MaxRatio);
        _viewModel.SetSplitRatio(split.NodeId, newRatio);
        e.Handled = true;
    };
    handle.PointerReleased += (s, e) =>
    {
        handle.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    };
    return handle;
}
```

### Pattern 2: Immutable Tree Swap in Reducer
**What:** A pure function that swaps two leaf nodes in the tree by replacing their PaneId/SessionId/Kind while preserving tree structure.
**When to use:** PINT-02 (keyboard swap)
**Example:**
```csharp
// In LayoutReducer.cs -- pure state transition
public static LayoutNode SwapPanes(LayoutNode root, string paneIdA, string paneIdB)
{
    // Find both leaf nodes, then produce new tree with their data swapped
    // This preserves tree structure (all SplitNodes stay the same)
    // but swaps which pane occupies which position
    return SwapLeavesInTree(root, paneIdA, paneIdB);
}

private static LayoutNode SwapLeavesInTree(LayoutNode node, string idA, string idB)
{
    if (node is LeafNode leaf)
    {
        if (leaf.PaneId == idA)
            return leaf with { PaneId = idB, SessionId = FindSessionId(root, idB), Kind = FindKind(root, idB) };
        if (leaf.PaneId == idB)
            return leaf with { PaneId = idA, SessionId = FindSessionId(root, idA), Kind = FindKind(root, idA) };
        return leaf;
    }
    if (node is SplitNode split)
    {
        return split with
        {
            First = SwapLeavesInTree(split.First, idA, idB),
            Second = SwapLeavesInTree(split.Second, idA, idB),
        };
    }
    return node;
}
```

### Pattern 3: Drag-to-Rearrange with Directional Preview Overlay
**What:** When user drags a pane title bar, show a semi-transparent blue overlay on the target pane indicating where the dragged pane will land (left/right/top/bottom half). On drop, execute a tree operation that removes the source leaf and re-inserts it adjacent to the target.
**When to use:** PINT-03 (drag-to-rearrange)
**Example:**
```csharp
// Directional preview overlay -- add to RootContainer during drag
private Border _dragPreview = new Border
{
    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 50, 130, 240)), // semi-transparent blue
    IsHitTestVisible = false, // overlay only, doesn't intercept pointer
};

// During PointerMoved while dragging, determine drop zone:
private (string targetPaneId, Direction dropSide) HitTestDropZone(Windows.Foundation.Point pos)
{
    var paneRects = _viewModel.LayoutStore.PaneRects;
    foreach (var (paneId, rect) in paneRects)
    {
        if (pos.X >= rect.X && pos.X <= rect.X + rect.Width &&
            pos.Y >= rect.Y && pos.Y <= rect.Y + rect.Height)
        {
            // Determine quadrant within the pane
            double relX = (pos.X - rect.X) / rect.Width;
            double relY = (pos.Y - rect.Y) / rect.Height;
            // Use diagonal test: which edge is closest
            Direction side;
            if (relX < 0.25) side = Direction.Left;
            else if (relX > 0.75) side = Direction.Right;
            else if (relY < 0.5) side = Direction.Up;
            else side = Direction.Down;
            return (paneId, side);
        }
    }
    return (null, Direction.Left);
}
```

### Anti-Patterns to Avoid
- **Mutating LayoutNode directly:** All tree mutations MUST go through LayoutReducer pure functions. Never modify SplitNode.Ratio or swap leaves outside the reducer.
- **Using AllowDrop/DragDrop API for internal rearrange:** WinUI DragDrop is for inter-app data transfer. Using it for internal pane rearrangement creates a poor UX (system drag adorner, clipboard involvement) and complicates the pointer state machine.
- **Placing resize handles inside pane containers:** Resize handles must be siblings in RootContainer positioned at split boundaries, NOT children of pane grids. Pane grids get recreated on layout changes; handles tied to them would leak or ghost.
- **Forgetting to handle PointerCaptureLost:** If capture is lost unexpectedly (e.g., another window activated), the drag state must reset cleanly. Always listen for PointerCaptureLost alongside PointerReleased.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Resize cursor feedback | Custom cursor image loading | `InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast)` via ProtectedCursor subclass | System cursors match OS theme, DPI-aware, zero maintenance |
| Pointer capture state machine | Manual bool flags for tracking drag state | `CapturePointer` / `ReleasePointerCapture` + `PointerCaptureLost` event | System handles edge cases (multi-touch, pen interruption, window deactivation) |
| Tree diff for re-rendering | Custom diffing of old vs new tree | Existing `RenderLayoutAsync` reconciliation in WorkspaceView | Already handles add/remove/reposition of pane views |

**Key insight:** The existing architecture already does the hard part (immutable tree, pure reducer, computed rects, reconciled rendering). Phase 7 only needs to add new reducer operations and pointer event wiring.

## Common Pitfalls

### Pitfall 1: WebView2 Swallows Pointer Events
**What goes wrong:** WebView2 captures all pointer events within its bounds. Resize handles placed behind or inside WebView2 panes will never receive PointerEntered/PointerPressed.
**Why it happens:** WebView2 is a hosted browser control that manages its own input pipeline.
**How to avoid:** Place resize handle elements ABOVE (higher z-index) the pane containers in RootContainer.Children. Since RootContainer is a Grid, elements added later are rendered on top. Add resize handles after pane containers in `RenderLayoutAsync`.
**Warning signs:** Resize cursor never appears, PointerPressed never fires on handles.

### Pitfall 2: Resize Handles Accumulate on Re-render
**What goes wrong:** Each call to `RenderLayoutAsync` adds new resize handles without removing old ones, causing ghost handles and pointer event conflicts.
**Why it happens:** The existing reconciliation only tracks `_paneViews` and `_browserPaneViews` dictionaries. Resize handles need their own tracking.
**How to avoid:** Track resize handles in a `List<UIElement> _resizeHandles` field. At the start of each render, remove all existing handles from RootContainer and clear the list before recreating them from the current tree.
**Warning signs:** Multiple overlapping resize cursors, erratic resize behavior.

### Pitfall 3: Drag Preview Covers Drop Target Hit-Testing
**What goes wrong:** The blue directional preview overlay intercepts pointer events, preventing accurate hit-testing of the underlying pane for drop zone calculation.
**Why it happens:** Default hit-testing routes to the topmost visible element.
**How to avoid:** Set `IsHitTestVisible = false` on the preview overlay Border. Hit-test against `LayoutStore.PaneRects` mathematically using the pointer position, not via XAML element hit-testing.
**Warning signs:** Drop zone detection stops working when preview is visible.

### Pitfall 4: Swap Operation Breaks Session-to-Pane Mapping
**What goes wrong:** After swapping, the `_paneSessions` dictionary in WorkspaceViewModel still maps old pane IDs to old sessions, but the leaf nodes now have swapped IDs.
**Why it happens:** The swap changes which PaneId is in which tree position, but the ViewModel's `_paneSessions` dictionary is keyed by PaneId.
**How to avoid:** Swap approach should swap the content of leaf nodes (PaneId, SessionId, Kind) rather than the tree structure. This way the pane IDs stay at their positions and only the visual content moves. Alternatively, swap the `_paneSessions` entries simultaneously.
**Warning signs:** After swap, terminals show wrong content or crash on resize.

### Pitfall 5: ProtectedCursor Requires Subclassing
**What goes wrong:** Attempting to set cursor on a standard Border or Grid fails because ProtectedCursor is a protected property.
**Why it happens:** WinUI 3 design decision -- ProtectedCursor is not publicly settable.
**How to avoid:** Create a minimal subclass: `class CursorBorder : Border { public InputCursor Cursor { get => ProtectedCursor; set => ProtectedCursor = value; } }`. This is the standard community pattern.
**Warning signs:** Compile error "cannot access protected member ProtectedCursor".

## Code Examples

### CursorBorder Subclass for Resize Handles
```csharp
// Minimal subclass to expose ProtectedCursor for cursor changes
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Wcmux.App.Views;

internal sealed class CursorBorder : Border
{
    public InputCursor Cursor
    {
        get => ProtectedCursor;
        set => ProtectedCursor = value;
    }
}
```

### Keyboard Swap Accelerator Registration
```csharp
// In PaneCommandBindings.cs -- add swap bindings alongside existing commands
// Swap: Ctrl+Alt+Shift+Arrow
AddAccelerator(target, VirtualKey.Left,
    VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift,
    () => { viewModel.SwapActivePane(Direction.Left); return Task.CompletedTask; });

AddAccelerator(target, VirtualKey.Right,
    VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift,
    () => { viewModel.SwapActivePane(Direction.Right); return Task.CompletedTask; });

AddAccelerator(target, VirtualKey.Up,
    VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift,
    () => { viewModel.SwapActivePane(Direction.Up); return Task.CompletedTask; });

AddAccelerator(target, VirtualKey.Down,
    VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift,
    () => { viewModel.SwapActivePane(Direction.Down); return Task.CompletedTask; });
```

### SetSplitRatio Reducer Function (for Mouse Resize)
```csharp
// In LayoutReducer.cs -- set ratio on a specific split node by NodeId
public static LayoutNode SetSplitRatio(LayoutNode root, string nodeId, double newRatio)
{
    if (root is SplitNode split)
    {
        if (split.NodeId == nodeId)
            return split with { Ratio = Math.Clamp(newRatio, MinRatio, MaxRatio) };

        var newFirst = SetSplitRatio(split.First, nodeId, newRatio);
        if (!ReferenceEquals(newFirst, split.First))
            return split with { First = newFirst };

        var newSecond = SetSplitRatio(split.Second, nodeId, newRatio);
        if (!ReferenceEquals(newSecond, split.Second))
            return split with { Second = newSecond };
    }
    return root;
}
```

### MovePaneToTarget Reducer Function (for Drag-to-Rearrange)
```csharp
// In LayoutReducer.cs -- detach a leaf and re-insert it adjacent to a target
public static LayoutNode? MovePaneToTarget(
    LayoutNode root, string sourcePaneId, string targetPaneId, Direction dropSide)
{
    // 1. Find the source leaf data
    // 2. Remove the source from the tree (like ClosePane)
    // 3. Split the target pane and insert source as the new sibling
    // This is a composition of ClosePane + SplitPane
    var sourceLeaf = FindLeaf(root, sourcePaneId);
    if (sourceLeaf is null) return root;

    var treeWithoutSource = ClosePane(root, sourcePaneId);
    if (treeWithoutSource is null) return root; // was the only pane

    var axis = dropSide is Direction.Left or Direction.Right
        ? SplitAxis.Vertical
        : SplitAxis.Horizontal;

    // Insert source before or after target depending on direction
    bool insertFirst = dropSide is Direction.Left or Direction.Up;
    return InsertPaneAtTarget(treeWithoutSource, targetPaneId, sourceLeaf, axis, insertFirst);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| WPF GridSplitter control | WinUI 3 has no built-in GridSplitter; use pointer events | WinUI 3 launch (2021) | Must implement custom resize handles |
| UWP Cursor property on FrameworkElement | ProtectedCursor on UIElement (WinAppSDK 1.0+) | WinAppSDK 1.0 | Requires subclassing to expose cursor |
| System DragDrop for all drag operations | Pointer capture for internal layout ops | Best practice | DragDrop API reserved for inter-app data transfer |

**Deprecated/outdated:**
- `CoreCursor` / `Window.Current.CoreWindow.PointerCursor`: UWP-era cursor API, not available in WinUI 3 desktop apps. Use `ProtectedCursor` with `InputSystemCursor.Create()` instead.

## Open Questions

1. **Title bar drag initiation threshold**
   - What we know: Need to distinguish between a click (to focus pane) and a drag (to start rearrange)
   - What's unclear: Exact pixel threshold before initiating drag
   - Recommendation: Use a 5px movement threshold (standard Windows drag threshold) -- start drag only after pointer moves 5px from initial press point

2. **Resize handle z-order with drag preview**
   - What we know: Both resize handles and drag preview overlay need to be above pane content
   - What's unclear: Whether resize handles should remain active during a drag-to-rearrange operation
   - Recommendation: Hide resize handles during active drag-to-rearrange to avoid visual clutter; they are irrelevant during that operation

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 |
| Config file | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| Quick run command | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~Layout" --no-build -v q` |
| Full suite command | `dotnet test tests/Wcmux.Tests -v q` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PINT-01 | SetSplitRatio sets ratio correctly, clamps to min/max | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~SetSplitRatio" -v q` | No - Wave 0 |
| PINT-01 | SetSplitRatio finds correct node by NodeId | unit | (same filter) | No - Wave 0 |
| PINT-02 | SwapPanes swaps two leaf positions in tree | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~SwapPanes" -v q` | No - Wave 0 |
| PINT-02 | SwapPanes with non-adjacent panes returns unchanged tree | unit | (same filter) | No - Wave 0 |
| PINT-02 | SwapActivePane integrates directional focus + swap | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~SwapActive" -v q` | No - Wave 0 |
| PINT-03 | MovePaneToTarget removes source and inserts at target | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~MovePaneToTarget" -v q` | No - Wave 0 |
| PINT-03 | MovePaneToTarget respects drop direction (L/R/U/D) | unit | (same filter) | No - Wave 0 |
| PINT-03 | MovePaneToTarget with source == target returns unchanged | unit | (same filter) | No - Wave 0 |
| PINT-01 | Mouse resize visual behavior | manual-only | Visual verification: drag border, observe resize | N/A |
| PINT-03 | Blue preview overlay appears during drag | manual-only | Visual verification: drag title bar, observe overlay | N/A |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~Layout" --no-build -v q`
- **Per wave merge:** `dotnet test tests/Wcmux.Tests -v q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Wcmux.Tests/Layout/PaneInteractionTests.cs` -- covers PINT-01 (SetSplitRatio), PINT-02 (SwapPanes), PINT-03 (MovePaneToTarget)
- [ ] No new framework install needed -- xUnit already configured

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn - Handle pointer input](https://learn.microsoft.com/en-us/windows/apps/develop/input/handle-pointer-input) -- PointerPressed/Moved/Released patterns, CapturePointer usage
- [Microsoft Learn - UIElement.CapturePointer](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.capturepointer?view=windows-app-sdk-1.8) -- CapturePointer API reference, remarks on capture semantics
- Existing codebase: LayoutReducer.cs, LayoutStore.cs, WorkspaceView.xaml.cs, PaneCommandBindings.cs -- verified architecture patterns

### Secondary (MEDIUM confidence)
- [WindowsAppSDK Discussion #1816 - Cursor changes](https://github.com/microsoft/WindowsAppSDK/discussions/1816) -- ProtectedCursor subclass pattern confirmed by community
- [Microsoft Learn - Drag and drop](https://learn.microsoft.com/en-us/windows/apps/design/input/drag-and-drop) -- Confirmed DragDrop API is for data transfer, not internal layout operations

### Tertiary (LOW confidence)
- None -- all findings verified with official documentation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all APIs are existing WinUI 3 primitives already used in codebase
- Architecture: HIGH -- follows established reducer/store/view pattern from phases 1-6
- Pitfalls: HIGH -- verified against WinUI 3 known issues and codebase structure

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable APIs, no expected breaking changes)
