---
phase: 03-attention-and-windows-integration
verified: 2026-03-08T22:00:00Z
status: human_needed
score: 12/12 must-haves verified
human_verification:
  - test: "Ring bell in a non-focused pane and verify blinking blue border appears"
    expected: "Border blinks blue 4 times then settles to steady blue"
    why_human: "Requires running app and visual verification of animation"
  - test: "Focus the attention pane and verify border clears"
    expected: "Blue border disappears, pane becomes active (undimmed)"
    why_human: "Requires interactive focus change"
  - test: "Ring bell in background tab pane, verify tab attention indicator"
    expected: "Tab text blinks blue, clears when pane is focused"
    why_human: "Cross-tab visual behavior requires running app"
  - test: "Alt-tab away from wcmux, ring bell, verify Windows toast appears"
    expected: "Toast shows tab name and pane path, taskbar icon flashes"
    why_human: "OS-level notification requires unfocused window state"
  - test: "Click toast notification and verify deep-link to correct tab/pane"
    expected: "wcmux activates, switches to correct tab, focuses correct pane"
    why_human: "Toast activation and deep-link requires OS interaction"
  - test: "Alt-tab away, trigger toast, alt-tab back, verify toast dismissed"
    expected: "Pending toasts cleared from Action Center on window focus"
    why_human: "Action Center state requires OS-level verification"
---

# Phase 3: Attention And Windows Integration Verification Report

**Phase Goal:** Generic attention state and Windows notifications for background terminal sessions
**Verified:** 2026-03-08T22:00:00Z
**Status:** human_needed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

**Plan 01 (Bell Detection and In-App Indicators):**

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Bell character (0x07) triggers attention on background panes only | VERIFIED | TerminalSurfaceBridge.BellDetected fires on 0x07 detection. AttentionStore.RaiseBell suppresses when paneId == activePaneId. 13 AttentionStore tests pass. |
| 2 | Rapid bells within 5 seconds are debounced | VERIFIED | AttentionStore.RaiseBell checks cooldown window (5s). Test `RaiseBell_WithinCooldown_DoesNotRefire` confirms. |
| 3 | Pane attention clears on focus | VERIFIED | AttentionStore.ClearAttention called on pane focus. WorkspaceView calls clear in focus handler. Tests confirm. |
| 4 | Tab shows attention until all child panes individually focused | VERIFIED | AttentionStore.TabHasAttention checks all paneIds. TabBarView re-checks on AttentionChanged. Tests confirm aggregation. |
| 5 | Non-active panes dimmed to 0.5 opacity | VERIFIED | WorkspaceView sets Opacity=0.5 on non-active pane containers. |
| 6 | Attention panes have blinking blue border (4 blinks then steady) | VERIFIED | WorkspaceView uses DispatcherTimer at 500ms, 8 toggles (4 full blinks), then steady SteelBlue. |

**Plan 02 (Windows Toast Notifications):**

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 7 | Windows toast fires when bell occurs and window is not focused | VERIFIED | MainWindow tracks _isWindowFocused via Activated event. NotificationService.ShowAttentionToast called only when !_isWindowFocused. Code in commit 01f11a2. |
| 8 | Toast is suppressed when window has OS-level focus | VERIFIED | Guard check `if (!_isWindowFocused)` before toast call. In-app indicators (Plan 01) handle focused state. |
| 9 | Toast shows tab name and pane title | VERIFIED | NotificationService builds notification with tabLabel and paneTitle text lines. |
| 10 | Clicking toast activates wcmux, switches to correct tab, focuses correct pane | VERIFIED | App.OnNotificationInvoked extracts tabId/paneId from arguments. MainWindow handler switches tab and calls FocusPaneAsync. |
| 11 | Taskbar icon flashes alongside toast via FlashWindowEx | VERIFIED | NotificationService.FlashTaskbar calls FlashWindowEx P/Invoke with FLASHW_ALL | FLASHW_TIMERNOFG. |
| 12 | Pending toasts dismissed from Action Center when window regains focus | VERIFIED | MainWindow Activated handler calls _notificationService.DismissAllAsync() on focus regain. |

### Requirements Satisfied

| REQ-ID | Description | Status | Evidence |
|--------|-------------|--------|----------|
| NOTF-01 | In-app attention indicators | SATISFIED | Pane dimming, blinking blue borders, tab attention text. 03-01-SUMMARY confirms. 16 automated tests pass. |
| NOTF-02 | Windows desktop notification | SATISFIED | Toast notifications, taskbar flashing, deep-link activation. Code committed (01f11a2). Integration checker confirmed 10-step E2E chain. |
| NOTF-03 | Generic bell-based attention | SATISFIED | All detection is bell-based (0x07), works with any shell or tool. No hard-coded tool patterns. |

## Integration Verification

All cross-phase integrations verified by milestone audit:
- Phase 1 → 3: TerminalSurfaceBridge.BellDetected → SessionBellEvent (WIRED)
- Phase 1 → 3: SessionManager.RaiseEvent for synthetic bell events (WIRED)
- Phase 2 → 3: TabBarView attention indicators via AttentionStore (WIRED)
- Phase 2 → 3: WorkspaceView pane dimming and attention borders (WIRED)
- Phase 2 → 3: Tab-level attention aggregation (WIRED)
- Phase 1 → 2 → 3: Toast deep-link → tab switch → pane focus (WIRED)

## Automated Test Results

- `AttentionStoreTests.cs`: 13 tests (attention state, cooldown, suppression, clearance)
- `BellDetectionTests.cs`: 3 tests (bell detection and stripping)
- All 16 tests pass

## Human Verification Needed

Toast notifications, deep-linking, taskbar flashing, and visual animations require manual verification with the running application. See frontmatter for specific test scenarios.

---
*Verified: 2026-03-08*
*Verifier: Claude (gap closure)*
