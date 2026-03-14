# Phase 8: v1.1 Tech Debt Cleanup - Research

**Researched:** 2026-03-14
**Domain:** C# / WinUI 3 — cosmetic layout fix, ring buffer gating, dead code removal
**Confidence:** HIGH (all findings verified against actual source code)

## Summary

Phase 8 closes three low-severity audit findings accumulated during v1.1. All three are surgical edits to existing code with zero external library dependencies. The entire phase fits in a single plan (08-01) because every change is self-contained and independently verifiable.

The resize handle overlap (PINT-01) is a cosmetic-only positioning error in `WorkspaceView.CreateResizeHandles()`. For horizontal splits, the handle is placed at the raw split boundary Y coordinate without accounting for the 24px title bar that occupies the top of each pane container. The fix is a single constant offset in the handle placement math. Functional resize behavior (pointer capture, ratio update) is unaffected.

The ring buffer waste (SIDE-02) is an unconditional `AppendToRingBuffer` call in `TerminalSurfaceBridge.RunOutputBatchLoop()`. The display was intentionally suppressed in Phase 6 per user preference, so the ring buffer and ANSI stripper run on every output batch but nothing ever reads the result. The fix is to add a `PreviewEnabled` bool property to `TerminalSurfaceBridge` (defaults to `false`) and guard the `AppendToRingBuffer` call. This approach keeps the infrastructure in place for a future one-line re-enable without removing any ring buffer tests.

Dead code removal targets three disjoint items: the `TabBarView.xaml/.xaml.cs` XAML control pair that was replaced by `TabSidebarView` in Phase 6, the `WorkspaceView.GetPreviewText()` method that became unreferenced when the SIDE-02 display call was suppressed, and the `TabSidebarView._tabViews` private field plus the matching parameter in `Attach()` that is assigned but never read.

**Primary recommendation:** Implement all three fixes in 08-01-PLAN.md as sequential edits with a single compile verification at the end. No new types, no new test files, no new dependencies.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PINT-01 | Horizontal split resize handle positioned correctly (no title bar overlap) | Fix is a 1-line Y offset in `CreateResizeHandles()` — see Architecture Patterns |
| SIDE-02 | Ring buffer / ANSI stripper only run when preview display is enabled | Add `PreviewEnabled` property to `TerminalSurfaceBridge`, guard the call — existing 9 ring buffer tests remain valid |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| C# / WinUI 3 | net9.0-windows10.0.19041.0 | All edits are in existing project files | Project standard |
| xUnit | 2.9.2 | Existing test framework for ring buffer tests | Already in Wcmux.Tests.csproj |

No new packages required. All changes are edits to existing source files.

**Installation:** None.

## Architecture Patterns

### Resize Handle Positioning — the Bug

`WorkspaceView.CreateResizeHandles()` calls `CollectSplitBoundaries(root, 0, 0, containerW, containerH, boundaries)`. For a horizontal split at ratio `r`, the boundary position is:

```csharp
// Current (buggy) — WorkspaceView.xaml.cs line 857
var boundary = y + h * split.Ratio;
```

The handle margin is then:
```csharp
// Current (buggy) — lines 917-919
handle.Width = crossSize;
handle.Height = handleThickness;  // 6px
handle.Margin = new Thickness(crossStart, boundaryPos - handleThickness / 2, 0, 0);
```

`boundaryPos` is the pixel Y where the lower pane container starts — i.e., exactly where the lower pane's 24px title bar begins. The 6px handle therefore overlaps the lower pane title bar from `boundaryPos - 3` to `boundaryPos + 3` px.

### Resize Handle Fix

Shift the horizontal handle down by `TitleBarHeight` (24px) so its center sits at the boundary between the lower pane's title bar and the lower pane's content area. The handle remains fully within what the user perceives as "the gap between panes":

```csharp
// Fixed — horizontal split handle placement
const double TitleBarHeight = 24.0;
// ...
else  // Horizontal split
{
    handle.Cursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    handle.Width = crossSize;
    handle.Height = handleThickness;
    // Offset down by TitleBarHeight so handle clears the lower pane's title bar
    handle.Margin = new Thickness(crossStart, boundaryPos + TitleBarHeight - handleThickness / 2, 0, 0);
}
```

No change to `CollectSplitBoundaries`, `_resizeContainingY`, `_resizeContainingH`, or the pointer move ratio math — those use `RootContainer`-relative coordinates which remain correct. Only the visual placement of the handle element changes.

### Ring Buffer Gating

The fix adds a property to `TerminalSurfaceBridge` and adds a guard in the batch loop:

```csharp
// Add to TerminalSurfaceBridge (after existing public properties, ~line 93)
/// <summary>
/// When false (default), AppendToRingBuffer is skipped to avoid CPU overhead
/// while SIDE-02 preview display is disabled.
/// </summary>
public bool PreviewEnabled { get; set; } = false;
```

```csharp
// In RunOutputBatchLoop, replace line 290:
// Before:  AppendToRingBuffer(batch);
// After:
if (PreviewEnabled) AppendToRingBuffer(batch);
```

`AppendToRingBuffer` and `AnsiStripper.Strip` are not removed. `GetRecentLines` is not removed. All 9 ring buffer unit tests in `OutputRingBufferTests.cs` remain valid — they test ring buffer correctness directly, not whether it fires during production output. Tests already pass `PreviewEnabled` implicitly (it defaults to false but tests call `AppendToRingBuffer`/`GetRecentLines` directly — wait, tests go through `EnqueueOutput` which hits the batch loop). Tests will need `PreviewEnabled = true` or the ring buffer will never fill. See Pitfalls.

### Dead Code Removal Map

| Item | Location | Action | Callers to Update |
|------|----------|--------|-----------------|
| `TabBarView.xaml` | `src/Wcmux.App/Views/TabBarView.xaml` | Delete file | None — no references in MainWindow |
| `TabBarView.xaml.cs` | `src/Wcmux.App/Views/TabBarView.xaml.cs` | Delete file | None |
| `WorkspaceView.GetPreviewText()` | `WorkspaceView.xaml.cs` lines 691-698 | Delete method | None — grep confirms zero callers |
| `TabSidebarView._tabViews` field | `TabSidebarView.xaml.cs` line 18 | Delete field | — |
| `TabSidebarView.Attach()` 3rd parameter | `TabSidebarView.xaml.cs` line 37 | Remove parameter `Dictionary<string, WorkspaceView> tabViews` | `MainWindow.xaml.cs` line 185 |
| `_tabViews = tabViews;` assignment | `TabSidebarView.xaml.cs` line 41 | Delete assignment | — |
| MainWindow `Attach()` call site | `MainWindow.xaml.cs` line 185 | Remove 3rd argument `_tabViews` | — |

**Important distinction:** `MainWindow._tabViews` (line 20, type `Dictionary<string, WorkspaceView>`) is actively used throughout MainWindow for tab switching, cleanup, and focus — do NOT remove it. Only the identically-named field `TabSidebarView._tabViews` is dead.

### Anti-Patterns to Avoid

- **Removing `AppendToRingBuffer` or `GetRecentLines`:** Keep the infrastructure. The user may re-enable the display. Only gate the call.
- **Removing `TabSidebarView._refreshTimer` or the 2-second tick:** The timer currently drives `RenderTabs()` for cwd polling, not ring buffer reads. Do not touch it.
- **Deleting `MainWindow._tabViews`:** This field is used by 7 different code paths in MainWindow. Only the `TabSidebarView._tabViews` field is dead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Feature flag for ring buffer | Custom flag infrastructure | Simple `bool PreviewEnabled` property | Only one call site; no config system needed |
| XAML file deletion | Build script | Direct file delete + rebuild | XAML codegen (`XamlTypeInfo.g.cs`) is automatic |

## Common Pitfalls

### Pitfall 1: Ring Buffer Tests Break After Gating

**What goes wrong:** All 9 tests in `OutputRingBufferTests.cs` use `EnqueueOutput()` → batch loop → `AppendToRingBuffer`. After the gate, `PreviewEnabled` defaults to `false`, so the ring buffer never fills and all tests expecting content fail with empty arrays.

**Why it happens:** Tests access the ring buffer through the public API (`GetRecentLines`), not by calling `AppendToRingBuffer` directly. With the guard in place, the batch loop skips the write.

**How to avoid:** Set `bridge.PreviewEnabled = true` in the test helper `CreateBridge()` in `OutputRingBufferTests.cs`. Alternatively set it per-test before calling `EnqueueOutput`. The property just needs to be set before the first batch flush.

**Fix in `OutputRingBufferTests.cs`:**
```csharp
private TerminalSurfaceBridge CreateBridge(int batchIntervalMs = 5, int ringBufferCapacity = 20)
{
    _bridge = new TerminalSurfaceBridge(...);
    _bridge.PreviewEnabled = true;  // Tests explicitly exercise ring buffer
    return _bridge;
}
```

### Pitfall 2: TabBarView XAML Deletion Leaves obj/ Generated Files

**What goes wrong:** Deleting `TabBarView.xaml` and `TabBarView.xaml.cs` leaves stale generated files in `obj/Debug/.../Views/TabBarView.g.cs` and `TabBarView.g.i.cs`. The project may still compile against those, masking any reference errors.

**Why it happens:** Generated files are not auto-cleaned unless a full rebuild or `dotnet clean` is run.

**How to avoid:** After deletion, run `dotnet build` (which triggers codegen) rather than relying on incremental build. The `XamlTypeInfo.g.cs` will be regenerated without `TabBarView`. If build fails on `TabBarView` references, grep the codebase first — the current codebase has zero references.

### Pitfall 3: Resize Handle Offset Direction

**What goes wrong:** Adding `-TitleBarHeight` instead of `+TitleBarHeight` pushes the handle up into the upper pane's content area rather than below the lower pane's title bar.

**Why it happens:** The boundary point is the top of the lower pane container. Moving up means going into the upper pane; moving down clears the lower pane's title bar.

**How to avoid:** The correct fix is `boundaryPos + TitleBarHeight - handleThickness / 2`. With a 600px container, 50/50 split: boundary=300, title bar ends at 324, handle center at `300 + 24 - 3 = 321`, handle spans 318-324 — snug against the bottom of the lower pane's title bar, visible without overlapping.

### Pitfall 4: Resize Ratio Math is Unaffected by Handle Offset

**What goes wrong:** Assuming the handle Y offset must also be corrected in the pointer move ratio calculation.

**Why it happens:** Confusing visual placement with functional coordinate tracking.

**How to avoid:** The ratio math in `PointerMoved` uses `e.GetCurrentPoint(RootContainer).Position.Y` and `_resizeContainingY` / `_resizeContainingH` — these are all relative to `RootContainer`, not the handle's visual position. The math is already correct for functional resizing. Only the handle's visual `Margin` changes.

## Code Examples

### Current Resize Handle Placement (buggy section only)

```csharp
// WorkspaceView.xaml.cs, lines 913-919 (the else branch for Horizontal splits)
else
{
    // Horizontal split: NS resize cursor, narrow horizontal strip
    handle.Cursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    handle.Width = crossSize;
    handle.Height = handleThickness;
    handle.Margin = new Thickness(crossStart, boundaryPos - handleThickness / 2, 0, 0);
}
```

### Fixed Resize Handle Placement

```csharp
// Add constant near top of CreateResizeHandles (or as a class constant):
private const double PaneTitleBarHeight = 24.0;

// In the else branch:
else
{
    handle.Cursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    handle.Width = crossSize;
    handle.Height = handleThickness;
    // Offset below the lower pane's 24px title bar
    handle.Margin = new Thickness(crossStart, boundaryPos + PaneTitleBarHeight - handleThickness / 2, 0, 0);
}
```

### Ring Buffer Guard

```csharp
// In RunOutputBatchLoop, replace:
AppendToRingBuffer(batch);
// With:
if (PreviewEnabled) AppendToRingBuffer(batch);
```

### TabSidebarView.Attach() Signature Change

```csharp
// Before:
public void Attach(TabViewModel viewModel, AttentionStore? attentionStore, Dictionary<string, WorkspaceView> tabViews)
{
    _viewModel = ...;
    _attentionStore = attentionStore;
    _tabViews = tabViews;   // delete this line
    ...
}

// After:
public void Attach(TabViewModel viewModel, AttentionStore? attentionStore)
{
    _viewModel = ...;
    _attentionStore = attentionStore;
    ...
}
```

### MainWindow Call Site Update

```csharp
// MainWindow.xaml.cs line 185 — before:
TabSidebar.Attach(_tabViewModel, _attentionStore, _tabViews);

// After:
TabSidebar.Attach(_tabViewModel, _attentionStore);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AppendToRingBuffer` always runs | Gate behind `PreviewEnabled` | Phase 8 | Eliminates ANSI strip + lock on every output batch while display is off |
| Handle at raw split boundary Y | Handle at `boundaryY + 24px` | Phase 8 | Cosmetic fix — handle no longer overlaps lower pane title bar |

## Open Questions

1. **Should `PaneTitleBarHeight = 24` be a named constant or inline?**
   - What we know: 24px is already used in multiple places (pane outer grid row definition, drag threshold comments). It is not currently a shared constant.
   - What's unclear: Whether to add a static class constant or a local `const double`.
   - Recommendation: Use a local `const double PaneTitleBarHeight = 24.0;` inside `CreateResizeHandles()` for locality. A shared constant would require a new file or adding to an existing class — excess ceremony for a cleanup phase.

2. **Will XAML codegen re-run cleanly after TabBarView deletion?**
   - What we know: WinUI 3 generates `XamlTypeInfo.g.cs` from all `.xaml` files in the project at build time. Deleting `TabBarView.xaml` removes it from codegen input.
   - What's unclear: Whether any reflection-based XAML factory in `XamlTypeInfo.g.cs` would require a clean build to fully clear.
   - Recommendation: Run `dotnet build` (not just incremental) after deletion and verify zero errors.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 |
| Config file | `tests/Wcmux.Tests/Wcmux.Tests.csproj` |
| Quick run command | `dotnet test tests/Wcmux.Tests --filter "Category!=Integration" -v q` |
| Full suite command | `dotnet test tests/Wcmux.Tests -v q` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PINT-01 | Resize handle Y offset does not overlap lower pane title bar | manual-only (UI visual) | N/A — visual inspection only | N/A |
| SIDE-02 | Ring buffer does not run when `PreviewEnabled = false` | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBufferTests" -v q` | Yes — `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` |
| SIDE-02 | Ring buffer fills correctly when `PreviewEnabled = true` | unit | same command | Yes |
| Dead code | Project compiles with no errors after deletions | build | `dotnet build src/Wcmux.App -v q` | N/A |

**PINT-01 is manual-only** — the overlap is a cosmetic pixel positioning issue in a WinUI 3 visual tree. There is no testable surface in the unit test project (which has no WinUI 3 runtime). Human visual verification against a running app is the appropriate gate.

### Sampling Rate
- **Per task commit:** `dotnet test tests/Wcmux.Tests -v q` + `dotnet build src/Wcmux.App -v q`
- **Per wave merge:** same (single-wave phase)
- **Phase gate:** All tests green + app builds clean before `/gsd:verify-work`

### Wave 0 Gaps

None — existing test infrastructure covers all phase requirements. The `OutputRingBufferTests.cs` file exists and has 9 passing tests. The only change needed to tests is adding `_bridge.PreviewEnabled = true` in `CreateBridge()` to keep the 9 tests passing after the guard is added.

## Sources

### Primary (HIGH confidence)
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` — Full source read; `CreateResizeHandles()` lines 872-983, `GetPreviewText()` lines 691-698, `CollectSplitBoundaries()` lines 837-866
- `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` — Full source read; `RunOutputBatchLoop()` lines 252-316, `AppendToRingBuffer()` lines 349-366
- `src/Wcmux.App/Views/TabSidebarView.xaml.cs` — Full source read; `Attach()` lines 37-57, `_tabViews` field line 18
- `src/Wcmux.App/Views/TabBarView.xaml` + `TabBarView.xaml.cs` — Full source read; confirmed zero callers in MainWindow
- `src/Wcmux.App/MainWindow.xaml.cs` — Full source read; `TabSidebar.Attach()` call line 185, `_tabViews` usage lines 151, 246, 276, 280, 294, 300, 408, 412
- `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` — Full source read; 9 ring buffer tests using `EnqueueOutput` + `GetRecentLines`
- `tests/Wcmux.Tests/Wcmux.Tests.csproj` — Confirmed xUnit 2.9.2, net9.0-windows10.0.19041.0
- `.planning/v1.1-MILESTONE-AUDIT.md` — Primary source of truth for all three audit findings
- `src/Wcmux.Core/Layout/LayoutNode.cs` — Confirmed `PaneRect` structure, `SplitAxis` enum, `SplitNode.Ratio`

### Secondary (MEDIUM confidence)
- `src/Wcmux.App/MainWindow.xaml` — Confirmed `TabSidebarView` is the only tab navigation element; no `TabBarView` usage in XAML

## Metadata

**Confidence breakdown:**
- PINT-01 fix: HIGH — root cause confirmed by reading `CreateResizeHandles()` and `CollectSplitBoundaries()` source; fix is arithmetic
- SIDE-02 fix: HIGH — `AppendToRingBuffer` call location confirmed; `PreviewEnabled` property pattern is idiomatic C#
- Dead code removal: HIGH — grep confirmed zero callers of `GetPreviewText`; `_tabViews` assignment confirmed but never read; `TabBarView` not referenced in MainWindow
- Test impact: HIGH — ring buffer tests verified to use `EnqueueOutput` → batch loop path that will be gated

**Research date:** 2026-03-14
**Valid until:** Indefinite — all findings are against actual codebase, not external library docs
