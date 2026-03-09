---
phase: 06-vertical-tab-sidebar
plan: 01
subsystem: terminal
tags: [ansi, vt-escape, ring-buffer, text-capture, regex]

# Dependency graph
requires:
  - phase: 04-custom-chrome-and-webview2-foundation
    provides: TerminalSurfaceBridge with batch output loop
provides:
  - AnsiStripper.Strip static method for VT escape code removal
  - TerminalSurfaceBridge.GetRecentLines for sidebar preview text polling
affects: [06-02-sidebar-panel, 06-03-tab-rendering]

# Tech tracking
tech-stack:
  added: []
  patterns: [GeneratedRegex for compiled regex, circular ring buffer with lock-based thread safety]

key-files:
  created:
    - src/Wcmux.Core/Terminal/AnsiStripper.cs
    - tests/Wcmux.Tests/Terminal/AnsiStripperTests.cs
    - tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs
  modified:
    - src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs

key-decisions:
  - "Used GeneratedRegex (source-generated) instead of new Regex for AnsiStripper pattern matching"
  - "Ring buffer uses lock-based synchronization (not ConcurrentQueue) for fixed-capacity circular access"
  - "Empty/whitespace-only lines excluded from ring buffer to keep preview text meaningful"

patterns-established:
  - "AnsiStripper.Strip as shared utility for any future plain-text extraction from VT output"
  - "Ring buffer pattern: fixed array + head pointer + count with modular arithmetic"

requirements-completed: [SIDE-02]

# Metrics
duration: 5min
completed: 2026-03-09
---

# Phase 6 Plan 01: Output Capture Infrastructure Summary

**ANSI-stripping ring buffer in TerminalSurfaceBridge capturing last N plain-text lines for sidebar tab preview**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-09T04:48:31Z
- **Completed:** 2026-03-09T04:53:00Z
- **Tasks:** 4 (TDD RED/GREEN x2 features)
- **Files modified:** 4

## Accomplishments
- AnsiStripper static class with GeneratedRegex removing CSI, OSC, charset, two-char ESC, and control characters
- Ring buffer (configurable capacity, default 20) integrated into TerminalSurfaceBridge batch output loop
- GetRecentLines public method for thread-safe sidebar preview polling
- 22 unit tests covering all behaviors including thread safety and wrap-around

## Task Commits

Each task was committed atomically:

1. **RED: AnsiStripper tests** - `264c4f6` (test)
2. **GREEN: AnsiStripper implementation** - `710998a` (feat)
3. **RED: Ring buffer tests** - `e7af9db` (test)
4. **GREEN: Ring buffer implementation** - `313c6bc` (feat)

_TDD flow: RED (failing tests) then GREEN (implementation passes) for each feature._

## Files Created/Modified
- `src/Wcmux.Core/Terminal/AnsiStripper.cs` - Static class with compiled regex for VT escape stripping
- `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` - Added ring buffer fields, AppendToRingBuffer, GetRecentLines
- `tests/Wcmux.Tests/Terminal/AnsiStripperTests.cs` - 13 tests for ANSI stripping edge cases
- `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` - 9 tests for ring buffer behavior

## Decisions Made
- Used GeneratedRegex (source-generated compiled regex) for AnsiStripper -- better AOT support and startup performance
- Ring buffer uses `lock` synchronization since reads and writes access a shared fixed-size array (ConcurrentQueue not suitable for circular overwrite)
- Empty/whitespace lines excluded from ring buffer to keep preview text clean for sidebar display
- Two-char ESC pattern expanded to cover full 0x20-0x7E range (catches DECKPAM `ESC =`, DECKPNM `ESC >`)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Expanded two-char ESC regex range**
- **Found during:** GREEN phase of AnsiStripper
- **Issue:** Original regex character class `[@A-Z\[\\\]^_\`a-z{|}~]` did not cover `=` (0x3D) and `>` (0x3E) used by DECKPAM/DECKPNM
- **Fix:** Changed to `[\x20-\x7e]` to match all printable chars after ESC
- **Files modified:** src/Wcmux.Core/Terminal/AnsiStripper.cs
- **Verification:** All 13 AnsiStripper tests pass
- **Committed in:** 710998a (part of GREEN commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Minor regex range correction. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- AnsiStripper.Strip and GetRecentLines are ready for Plan 02 (sidebar panel) to poll preview text per pane
- Bridge.GetRecentLines(2) returns the 2 most recent non-empty lines -- exactly what the sidebar tab preview needs
- No blockers

---
*Phase: 06-vertical-tab-sidebar*
*Completed: 2026-03-09*
