---
phase: 03-attention-and-windows-integration
plan: 01
subsystem: runtime, ui
tags: [attention, bell, terminal, conpty, winui, animation]

# Dependency graph
requires:
  - phase: 01-terminal-runtime-and-panes
    provides: TerminalSurfaceBridge, SessionManager, SessionEvent, LayoutStore
  - phase: 02-tabbed-multiplexer-shell
    provides: TabStore, TabBarView, WorkspaceView, TabViewModel
provides:
  - AttentionStore for per-pane attention state management
  - Bell detection and stripping in TerminalSurfaceBridge
  - SessionBellEvent in the session event bus
  - Visual attention indicators (pane borders, tab text, dimming)
  - SessionManager.RaiseEvent for synthetic event injection
affects: [03-attention-and-windows-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: [DispatcherTimer blink animation, attention state store, synthetic session events]

key-files:
  created:
    - src/Wcmux.Core/Runtime/AttentionStore.cs
    - tests/Wcmux.Tests/Runtime/AttentionStoreTests.cs
    - tests/Wcmux.Tests/Terminal/BellDetectionTests.cs
  modified:
    - src/Wcmux.Core/Runtime/SessionEvent.cs
    - src/Wcmux.Core/Runtime/SessionManager.cs
    - src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs
    - src/Wcmux.App/MainWindow.xaml.cs
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs
    - src/Wcmux.App/Views/TabBarView.xaml.cs
    - src/Wcmux.App/Views/TerminalPaneView.xaml.cs

key-decisions:
  - "SessionManager.RaiseEvent method for synthetic events rather than custom event bus"
  - "TerminalPaneView fires SessionBellEvent through SessionManager on bridge BellDetected"
  - "DispatcherTimer-based blink with 8 toggles then steady color"

patterns-established:
  - "Synthetic event injection: SessionManager.RaiseEvent for events originating outside ConPTY"
  - "Attention store pattern: centralized state with cooldown and focus suppression"
  - "Blink animation: DispatcherTimer at 500ms, 4 full blinks, then steady"

requirements-completed: [NOTF-01, NOTF-03]

# Metrics
duration: 8min
completed: 2026-03-08
---

# Phase 03 Plan 01: Bell-Based Attention Detection Summary

**Generic bell-based attention system with AttentionStore, cooldown debounce, pane dimming, blinking blue borders, and tab attention indicators**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-08T19:05:49Z
- **Completed:** 2026-03-08T19:14:04Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments
- AttentionStore manages per-pane attention with 5-second cooldown, focus suppression, and cleanup
- Bell character (0x07) detected and stripped in TerminalSurfaceBridge before reaching xterm.js
- Non-active panes visually dimmed to 0.5 opacity; attention panes get blinking blue border
- Tab text blinks blue when any child pane has attention, clears only when all cleared
- All behavior is generic (bell-based), works with any shell or tool

## Task Commits

Each task was committed atomically:

1. **Task 1: AttentionStore, SessionBellEvent, and bell detection (TDD RED)** - `7010914` (test)
2. **Task 1: AttentionStore, SessionBellEvent, and bell detection (TDD GREEN)** - `1837832` (feat)
3. **Task 2: Wire attention through app and render visual indicators** - `7081fee` (feat)

## Files Created/Modified
- `src/Wcmux.Core/Runtime/AttentionStore.cs` - Per-pane attention state with cooldown, suppression, clearance
- `src/Wcmux.Core/Runtime/SessionEvent.cs` - Added SessionBellEvent record
- `src/Wcmux.Core/Runtime/SessionManager.cs` - Added RaiseEvent for synthetic events
- `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` - Bell detection, stripping, BellDetected event
- `src/Wcmux.App/MainWindow.xaml.cs` - AttentionStore creation, bell event routing, focus clearing
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - Pane dimming, attention border blink animation
- `src/Wcmux.App/Views/TabBarView.xaml.cs` - Tab text attention styling with blink
- `src/Wcmux.App/Views/TerminalPaneView.xaml.cs` - Bridge BellDetected to SessionBellEvent
- `tests/Wcmux.Tests/Runtime/AttentionStoreTests.cs` - 13 tests for attention state management
- `tests/Wcmux.Tests/Terminal/BellDetectionTests.cs` - 3 tests for bell detection and stripping

## Decisions Made
- Used SessionManager.RaiseEvent to inject SessionBellEvent through the existing event bus rather than creating a separate event mechanism
- TerminalPaneView fires SessionBellEvent on bridge.BellDetected since it has access to the sessionId
- Blink animation uses DispatcherTimer at 500ms intervals with 8 toggles (4 full blinks) then settles to steady attention color

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Pre-existing flaky test `SessionLifecycle_ConcurrentSessions_TrackedIndependently` fails intermittently - unrelated to attention changes, not addressed

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Attention system ready for integration with Windows toast notifications (plan 02)
- AttentionStore provides the signal source for escalating to OS-level notifications

---
*Phase: 03-attention-and-windows-integration*
*Completed: 2026-03-08*
