---
phase: 08-v1.1-tech-debt-cleanup
plan: 01
subsystem: ui
tags: [winui3, terminal, ring-buffer, resize-handle, dead-code]

# Dependency graph
requires:
  - phase: 06-sidebar-and-preview
    provides: Ring buffer infrastructure (OutputRingBuffer, AnsiStripper, TerminalSurfaceBridge)
  - phase: 07-pane-interaction
    provides: CreateResizeHandles() in WorkspaceView, horizontal split layout
provides:
  - PreviewEnabled flag on TerminalSurfaceBridge (SIDE-02 gating without removing infrastructure)
  - Corrected horizontal resize handle Y offset (+24px PaneTitleBarHeight)
  - Cleaned codebase: TabBarView deleted, GetPreviewText removed, TabSidebarView._tabViews pruned
affects: [09-future-preview-enable, any phase re-enabling SIDE-02 sidebar preview]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Feature flag property pattern: bool property (default false) guards optional CPU-intensive path
    - Dead code pruning: remove unused fields, parameters, and wrapper methods after refactor

key-files:
  created: []
  modified:
    - src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs
    - tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs
    - src/Wcmux.App/Views/TabSidebarView.xaml.cs
    - src/Wcmux.App/MainWindow.xaml.cs
  deleted:
    - src/Wcmux.App/Views/TabBarView.xaml
    - src/Wcmux.App/Views/TabBarView.xaml.cs

key-decisions:
  - "PreviewEnabled defaults to false — ring buffer and ANSI stripper skipped unless explicitly enabled, avoiding CPU overhead"
  - "Ring buffer infrastructure (AppendToRingBuffer, GetRecentLines, OutputRingBuffer) retained for future SIDE-02 re-enable"
  - "Horizontal handle offset = boundaryPos + 24 (PaneTitleBarHeight) - 3 (handleThickness/2), placing handle below lower pane title bar"
  - "TabBarView.xaml/.xaml.cs deleted (orphaned from Phase 6 sidebar refactor) — project builds clean after codegen flush"

patterns-established:
  - "Feature flag guard: add bool property (default false) then wrap optional call in if (flag) to gate CPU-intensive paths"
  - "Test helper setup: when production code adds a guard, test CreateBridge() must opt-in via _bridge.PreviewEnabled = true"

requirements-completed: [PINT-01, SIDE-02]

# Metrics
duration: ~15min
completed: 2026-03-14
---

# Phase 08 Plan 01: v1.1 Tech Debt Cleanup Summary

**Ring buffer gated behind PreviewEnabled flag, horizontal resize handle Y offset corrected by +24px, and orphaned TabBarView/dead code pruned — codebase ships clean for v1.1**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-03-14
- **Completed:** 2026-03-14
- **Tasks:** 3 (2 auto + 1 human-verify)
- **Files modified:** 5 modified, 2 deleted

## Accomplishments
- TerminalSurfaceBridge.PreviewEnabled (default false) gates AppendToRingBuffer — no CPU overhead for unconditional ring buffer writes while sidebar preview is disabled
- All 9 OutputRingBufferTests pass with PreviewEnabled = true set in CreateBridge() helper
- Horizontal split resize handle Margin corrected: `boundaryPos + PaneTitleBarHeight - handleThickness/2` places handle visually below the lower pane title bar instead of overlapping it
- Deleted TabBarView.xaml and TabBarView.xaml.cs (orphaned from Phase 6 sidebar refactor), removed GetPreviewText from WorkspaceView, pruned TabSidebarView._tabViews field and parameter, updated MainWindow call site — project builds with zero errors
- Human verification approved: app starts clean, resize handle positioned correctly, resize drag functional, vertical splits unaffected, sidebar working

## Task Commits

Each task was committed atomically:

1. **Task 1: Gate ring buffer behind PreviewEnabled and fix tests** - `3aaf9d1` (feat)
2. **Task 2: Fix horizontal resize handle Y offset and remove dead code** - `11cf3cd` (fix)
3. **Task 3: Visual verification of resize handle positioning and app health** - human-verify approved, no code commit

## Files Created/Modified
- `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` - Added PreviewEnabled property (default false); guarded AppendToRingBuffer call
- `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` - CreateBridge() sets _bridge.PreviewEnabled = true before return
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - Added PaneTitleBarHeight = 24.0 constant; fixed horizontal handle Margin; deleted GetPreviewText method
- `src/Wcmux.App/Views/TabSidebarView.xaml.cs` - Removed _tabViews field, removed third parameter from Attach(), removed dead assignment
- `src/Wcmux.App/MainWindow.xaml.cs` - Updated TabSidebar.Attach() call to two arguments only
- `src/Wcmux.App/Views/TabBarView.xaml` - DELETED (orphaned file)
- `src/Wcmux.App/Views/TabBarView.xaml.cs` - DELETED (orphaned file)

## Decisions Made
- PreviewEnabled defaults to false — ring buffer infrastructure stays intact for future re-enable (SIDE-02 is planned, not dropped)
- Test helper explicitly opts in with PreviewEnabled = true — tests exercise ring buffer correctness directly without coupling to production flag default
- PaneTitleBarHeight defined as local const (24.0) inside CreateResizeHandles() matching the 24px established in Phase 05 design decision
- TabBarView deletion required non-incremental build to flush XAML codegen artifacts

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- v1.1 tech debt closure complete for PINT-01 and SIDE-02 audit findings
- Ring buffer infrastructure is live and ready; future phase can set PreviewEnabled = true to re-enable sidebar preview text without any additional plumbing
- Codebase is clean: no orphaned files, no dead parameters, no unconditional CPU waste

---
*Phase: 08-v1.1-tech-debt-cleanup*
*Completed: 2026-03-14*
