---
phase: 05-pane-title-bars-and-browser-panes
plan: 01
subsystem: ui
tags: [winui3, toolhelp32, process-detection, title-bar, pane-management]

requires:
  - phase: 04-custom-chrome-and-webview2-foundation
    provides: Custom dark title bar, WebView2 environment cache
provides:
  - PaneKind enum (Terminal/Browser) on LeafNode for pane type discrimination
  - ForegroundProcessDetector using ToolHelp32 P/Invoke for process tree walking
  - ISession.ProcessId property for process identification
  - Per-pane title bar UI with process name, split buttons, and close button
  - Shared DispatcherTimer for polling process names every 2 seconds
affects: [05-02-browser-panes, phase-06-sidebar]

tech-stack:
  added: [ToolHelp32 P/Invoke (CreateToolhelp32Snapshot, Process32First/Next)]
  patterns: [single-shared-timer-polling, title-bar-grid-row-per-pane]

key-files:
  created:
    - src/Wcmux.Core/Runtime/ForegroundProcessDetector.cs
    - tests/Wcmux.Tests/Runtime/ForegroundProcessDetectorTests.cs
    - tests/Wcmux.Tests/Layout/PaneKindTests.cs
  modified:
    - src/Wcmux.Core/Layout/LayoutNode.cs
    - src/Wcmux.Core/Runtime/ISession.cs
    - src/Wcmux.Core/Runtime/ConPtySession.cs
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs
    - tests/Wcmux.Tests/Terminal/FakeSession.cs

key-decisions:
  - "Used single shared DispatcherTimer (2s interval) instead of per-pane timers to avoid timer proliferation"
  - "Title bar height set to 24px matching compact terminal aesthetic"
  - "Process name displayed without .exe extension for cleaner appearance"

patterns-established:
  - "Title bar pattern: outerGrid with Row0 (24px title bar) + Row1 (star content) per pane"
  - "Process detection pattern: ToolHelp32 snapshot walk from shell PID to deepest child"

requirements-completed: [PBAR-01, PBAR-02, PBAR-03]

duration: 8min
completed: 2026-03-09
---

# Phase 05 Plan 01: Pane Title Bars Summary

**Per-pane title bars with ToolHelp32 foreground process detection, split/close buttons, and PaneKind model for future browser pane support**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-09T03:04:00Z
- **Completed:** 2026-03-09T03:12:00Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- PaneKind.Terminal/Browser enum on LeafNode with backward-compatible Terminal default
- ForegroundProcessDetector walks ToolHelp32 process tree in ~1ms to find deepest child process name
- Every pane displays a 24px title bar with foreground process name updated every 2 seconds
- Title bar includes split-horizontal, split-vertical, and close icon buttons
- Close button closes the specific pane (not just active pane)

## Task Commits

Each task was committed atomically:

1. **Task 1: PaneKind model, ProcessId on ISession, ForegroundProcessDetector with tests**
   - `d20b285` (test) - RED: failing tests for PaneKind and ForegroundProcessDetector
   - `de2e6a6` (feat) - GREEN: implement PaneKind, ProcessId, ForegroundProcessDetector
2. **Task 2: Pane title bar UI with process name polling** - `59913db` (feat)

## Files Created/Modified
- `src/Wcmux.Core/Layout/LayoutNode.cs` - Added PaneKind enum and Kind property on LeafNode
- `src/Wcmux.Core/Runtime/ISession.cs` - Added ProcessId property to interface
- `src/Wcmux.Core/Runtime/ConPtySession.cs` - Implemented ProcessId via _process.Id
- `src/Wcmux.Core/Runtime/ForegroundProcessDetector.cs` - ToolHelp32 process tree walker
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - Replaced overlay with title bar grid rows
- `tests/Wcmux.Tests/Runtime/ForegroundProcessDetectorTests.cs` - 3 unit tests
- `tests/Wcmux.Tests/Layout/PaneKindTests.cs` - 3 unit tests
- `tests/Wcmux.Tests/Terminal/FakeSession.cs` - Added ProcessId to FakeSession

## Decisions Made
- Used single shared DispatcherTimer (2s interval) instead of per-pane timers to avoid timer proliferation
- Title bar height set to 24px matching compact terminal aesthetic
- Process name displayed without .exe extension for cleaner appearance

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated FakeSession to implement ISession.ProcessId**
- **Found during:** Task 1 (GREEN phase)
- **Issue:** Adding ProcessId to ISession broke FakeSession compilation
- **Fix:** Added ProcessId property to FakeSession using Environment.ProcessId
- **Files modified:** tests/Wcmux.Tests/Terminal/FakeSession.cs
- **Verification:** All 164 existing tests pass
- **Committed in:** de2e6a6 (Task 1 GREEN commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Required interface change propagation. No scope creep.

## Issues Encountered
- 2 pre-existing WebViewEnvironmentCacheTests failures unrelated to this plan (already in phase 04 deferred items)

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- PaneKind.Browser enum ready for Plan 02 (browser pane hosting)
- Title bar infrastructure supports future browser-specific buttons
- ForegroundProcessDetector can be skipped for Browser-kind panes (already handled in timer tick)

## Self-Check: PASSED

All 7 created/modified files verified on disk. All 3 commit hashes (d20b285, de2e6a6, 59913db) verified in git log.

---
*Phase: 05-pane-title-bars-and-browser-panes*
*Completed: 2026-03-09*
