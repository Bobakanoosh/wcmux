---
phase: 01-terminal-runtime-and-panes
plan: 02
subsystem: terminal
tags: [webview2, xterm.js, conpty, bridge, resize-debounce]

# Dependency graph
requires:
  - phase: 01-terminal-runtime-and-panes plan 01
    provides: ConPTY session host, ISession interface, SessionManager, session lifecycle events
provides:
  - WebView2-hosted xterm.js terminal surface per pane
  - Bidirectional IO bridge between ConPTY and terminal renderer
  - Debounced resize pipeline with redundancy suppression
  - Pixel-to-cell-size translation for resize negotiation
  - CWD tracking through bridge
  - 27 automated tests for bridge and resize behavior
  - Scripted Phase 1 smoke harness for TUI fidelity matrix
affects: [01-terminal-runtime-and-panes plan 03, pane-layout, terminal-fidelity]

# Tech tracking
tech-stack:
  added: [Microsoft.Web.WebView2 1.0.2651.64, xterm.js 5.5.0, xterm-addon-fit 0.10.0, xterm-addon-web-links 0.11.0]
  patterns: [terminal-surface-adapter, output-batching, resize-debounce, bridge-pattern]

key-files:
  created:
    - src/Wcmux.App/Terminal/WebViewTerminalController.cs
    - src/Wcmux.App/Views/TerminalPaneView.xaml
    - src/Wcmux.App/Views/TerminalPaneView.xaml.cs
    - src/Wcmux.App/TerminalWeb/index.html
    - src/Wcmux.App/TerminalWeb/terminal-host.js
    - src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs
    - tests/Wcmux.Tests/Terminal/TerminalBridgeTests.cs
    - tests/Wcmux.Tests/Terminal/ResizePipelineTests.cs
    - tests/Wcmux.Tests/Terminal/FakeSession.cs
    - tools/Run-Phase01TerminalSmoke.ps1
  modified:
    - src/Wcmux.App/MainWindow.xaml
    - src/Wcmux.App/MainWindow.xaml.cs
    - src/Wcmux.App/Wcmux.App.csproj
    - tests/Wcmux.Tests/Wcmux.Tests.csproj

key-decisions:
  - "Moved TerminalSurfaceBridge to Wcmux.Core to keep it testable without WinUI dependencies"
  - "Used CDN-hosted xterm.js rather than bundled NPM to avoid build tooling for Phase 1"
  - "Base64 encoding for WebView2 message transport to handle binary-safe VT data"

patterns-established:
  - "Bridge pattern: TerminalSurfaceBridge separates runtime from renderer with batching and debounce"
  - "Surface adapter: WebViewTerminalController is the narrow WinUI-to-xterm seam"
  - "FakeSession: reusable test double for ISession used across bridge and resize tests"

requirements-completed: [SESS-02]

# Metrics
duration: 10min
completed: 2026-03-06
---

# Phase 1 Plan 02: Terminal Surface, IO Bridge, and Fidelity Summary

**WebView2-hosted xterm.js terminal surface with bidirectional IO bridge, debounced resize pipeline, and 27 automated tests plus scripted smoke harness**

## Performance

- **Duration:** 10 min
- **Started:** 2026-03-07T02:55:03Z
- **Completed:** 2026-03-07T03:05:00Z
- **Tasks:** 3
- **Files modified:** 14

## Accomplishments
- WebView2 + xterm.js terminal surface rendering live ConPTY output in each pane
- Bidirectional IO bridge with output batching (~60fps), input routing, and cwd tracking
- Debounced resize pipeline that suppresses redundant ResizePseudoConsole calls and translates pixels to cell dimensions
- 27 automated tests covering bridge output forwarding, input routing, cwd signals, resize debounce, redundancy suppression, and pixel-to-cell translation
- Scripted smoke harness covering vim, fzf, paste, and aggressive resize scenarios

## Task Commits

Each task was committed atomically:

1. **Task 1: Host a WebView2-backed xterm surface for each terminal pane** - `e24b109` (feat)
2. **Task 2: Implement the terminal bridge for output batching, input routing, and resize negotiation** - `289e93c` (feat)
3. **Task 3: Add automated bridge coverage and the scripted Phase 1 smoke harness** - `6360399` (feat)

## Files Created/Modified
- `src/Wcmux.App/Terminal/WebViewTerminalController.cs` - Narrow adapter between WinUI WebView2 and xterm.js surface
- `src/Wcmux.App/Views/TerminalPaneView.xaml` - Native pane host with embedded WebView2 control
- `src/Wcmux.App/Views/TerminalPaneView.xaml.cs` - Pane lifecycle: attach/detach sessions, bridge wiring
- `src/Wcmux.App/TerminalWeb/index.html` - Hosted xterm.js entrypoint loaded per pane
- `src/Wcmux.App/TerminalWeb/terminal-host.js` - Terminal surface JS: input capture, resize observer, host API
- `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` - Bidirectional bridge with output batching, resize debounce, cwd tracking
- `tests/Wcmux.Tests/Terminal/TerminalBridgeTests.cs` - 14 tests for output forwarding, input routing, cwd signals
- `tests/Wcmux.Tests/Terminal/ResizePipelineTests.cs` - 13 tests for resize debounce and redundancy suppression
- `tests/Wcmux.Tests/Terminal/FakeSession.cs` - Reusable ISession test double
- `tools/Run-Phase01TerminalSmoke.ps1` - Scripted smoke harness for Phase 1 TUI fidelity matrix
- `src/Wcmux.App/MainWindow.xaml` - Replaced placeholder with live TerminalPaneView
- `src/Wcmux.App/MainWindow.xaml.cs` - Simplified to attach root session to pane view
- `src/Wcmux.App/Wcmux.App.csproj` - Added WebView2 package, TerminalWeb content, InternalsVisibleTo

## Decisions Made
- Moved TerminalSurfaceBridge to Wcmux.Core instead of Wcmux.App because the WinUI App project cannot be referenced from the test project due to MSBuild PRI generation task incompatibility
- Used CDN-hosted xterm.js (5.5.0) with fit and web-links addons rather than adding NPM build tooling
- Base64-encoded all WebView2 message payloads for binary-safe VT data transport
- Used ResizeObserver in the JS surface for automatic fit-on-resize without polling

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed WebView2 NuGet version downgrade error**
- **Found during:** Task 1 (WebView2 surface host)
- **Issue:** Initially referenced WebView2 1.0.2535.41 but WindowsAppSDK already transitively requires 1.0.2651.64
- **Fix:** Updated explicit WebView2 reference to match transitive dependency version
- **Files modified:** src/Wcmux.App/Wcmux.App.csproj
- **Verification:** dotnet build Wcmux.sln succeeds
- **Committed in:** e24b109 (Task 1 commit)

**2. [Rule 3 - Blocking] Moved TerminalSurfaceBridge from App to Core project**
- **Found during:** Task 2 (bridge implementation)
- **Issue:** Test project cannot reference WinUI App project (MSBuild PRI task assembly not found)
- **Fix:** Moved TerminalSurfaceBridge to Wcmux.Core.Terminal namespace since it has no WinUI dependencies
- **Files modified:** src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs, src/Wcmux.App/Views/TerminalPaneView.xaml.cs
- **Verification:** dotnet build and dotnet test both succeed
- **Committed in:** 289e93c (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Both fixes were necessary to unblock the build. Moving the bridge to Core actually improves the architecture by keeping testable logic out of the WinUI project. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Terminal surface and IO bridge are ready for pane layout work in Plan 03
- The bridge's resize pipeline is ready to receive pane-specific dimensions from a split tree
- The smoke harness can be extended with split/focus scenarios in Plan 03
- Blocker resolved: terminal rendering strategy is now validated (addressing Phase 1 blocker from STATE.md)

## Self-Check: PASSED

All 10 created files exist on disk. All 3 task commits (e24b109, 289e93c, 6360399) verified in git log. All artifact min_lines and contains requirements met. 43/43 tests pass.

---
*Phase: 01-terminal-runtime-and-panes*
*Completed: 2026-03-06*
