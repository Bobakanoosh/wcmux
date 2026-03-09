---
phase: 03-attention-and-windows-integration
plan: 02
subsystem: ui, notifications
tags: [toast, notifications, flashwindow, deep-link, winui]

# Dependency graph
requires:
  - phase: 03-attention-and-windows-integration
    plan: 01
    provides: AttentionStore, AttentionChanged event, bell detection
  - phase: 02-tabbed-multiplexer-shell
    provides: TabStore, TabBarView, TabViewModel
provides:
  - NotificationService for Windows toast notifications and taskbar flashing
  - Window focus tracking for toast suppression
  - Deep-link activation from toast click to tab/pane focus
  - AppNotificationManager lifecycle management
affects: [03-attention-and-windows-integration]

# Tech tracking
tech-stack:
  added: [Microsoft.Windows.AppSDK toast notifications, FlashWindowEx P/Invoke]
  patterns: [window focus tracking, toast deep-link arguments, Action Center dismissal]

key-files:
  created:
    - src/Wcmux.App/Notifications/NotificationService.cs
  modified:
    - src/Wcmux.App/MainWindow.xaml.cs
    - src/Wcmux.App/App.xaml.cs

key-decisions:
  - "FlashWindowEx P/Invoke with FLASHW_ALL | FLASHW_TIMERNOFG for taskbar flashing"
  - "Toast Tag=paneId Group=tabId for targeted removal"
  - "AppNotificationManager subscribe before Register per SDK requirements"

patterns-established:
  - "Window focus tracking: Activated event with WindowActivationState.Deactivated check"
  - "Toast deep-link: action=focusPane with tabId/paneId arguments"
  - "Dismiss-on-focus: RemoveAllAsync when window regains focus"

requirements-completed: [NOTF-02]

# Metrics
duration: ~10min
completed: 2026-03-08
---

# Phase 03 Plan 02: Windows Toast Notifications Summary

**Windows toast notifications, taskbar flashing, and deep-link activation for background attention events**

## Performance

- **Completed:** 2026-03-08
- **Tasks:** 1 (auto) + 1 (human verification checkpoint)
- **Files modified:** 3

## Accomplishments
- NotificationService creates Windows toast notifications with tab name and pane title
- FlashWindowEx P/Invoke flashes taskbar icon alongside toast
- Toasts fire only when wcmux window is unfocused (focus tracking via Activated event)
- Clicking toast deep-links to correct tab and pane via AppNotificationManager activation
- Pending toasts dismissed from Action Center when window regains focus
- AppNotificationManager registered on startup, unregistered on shutdown
- Toast respects 5-second cooldown from AttentionStore (no duplicate toasts)

## Task Commits

1. **Task 1: NotificationService, window focus tracking, and toast lifecycle** - `01f11a2` (feat)

## Files Created/Modified
- `src/Wcmux.App/Notifications/NotificationService.cs` - Toast creation, FlashWindowEx P/Invoke, activation handling
- `src/Wcmux.App/MainWindow.xaml.cs` - Window focus tracking, notification wiring, deep-link activation
- `src/Wcmux.App/App.xaml.cs` - AppNotificationManager registration and unregistration lifecycle

## Decisions Made
- Used FlashWindowEx with FLASHW_ALL | FLASHW_TIMERNOFG flags for persistent taskbar flashing until user focuses
- Toast Tag/Group set to paneId/tabId for targeted Action Center management
- AppNotificationManager event subscription happens before Register() call per SDK pitfall documentation

## Deviations from Plan

None significant.

## Issues Encountered
- None

## User Setup Required
None - uses built-in Windows App SDK toast infrastructure.

## Next Phase Readiness
- Phase 3 is complete. All attention and notification requirements satisfied.

---
*Phase: 03-attention-and-windows-integration*
*Completed: 2026-03-08*
