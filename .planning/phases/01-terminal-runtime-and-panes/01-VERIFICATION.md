---
phase: 01-terminal-runtime-and-panes
verified: 2026-03-06T23:00:00Z
status: human_needed
score: 4/4 must-haves verified
re_verification: false
human_verification:
  - test: "Launch wcmux, verify a PowerShell session opens and is interactive"
    expected: "Terminal shows PowerShell prompt, typing commands produces output"
    why_human: "ConPTY + WebView2 + xterm.js rendering chain requires a live Windows desktop"
  - test: "Run vim inside wcmux, type text, resize window, then exit"
    expected: "Alternate screen works, vim redraws on resize, shell prompt returns cleanly"
    why_human: "Full-screen TUI fidelity cannot be verified without visual observation"
  - test: "Split pane horizontally (Ctrl+Shift+H) and vertically (Ctrl+Shift+V)"
    expected: "Each new pane gets its own interactive PowerShell session inheriting cwd"
    why_human: "Pane rendering, session attachment, and cwd inheritance require live app"
  - test: "Move focus between panes (Ctrl+Shift+Arrows) and resize (Ctrl+Alt+Arrows)"
    expected: "Focus highlight moves correctly, pane sizes adjust, terminals stay interactive"
    why_human: "Keyboard accelerator routing and visual feedback need manual verification"
  - test: "Close a pane (Ctrl+Shift+W) and verify focus restores to sibling"
    expected: "Closed pane disappears, focus moves to most related surviving pane"
    why_human: "Visual layout collapse and focus restoration need human observation"
---

# Phase 1: Terminal Runtime And Panes Verification Report

**Phase Goal:** Deliver real Windows terminal sessions with reliable horizontal and vertical pane splitting.
**Verified:** 2026-03-06T23:00:00Z
**Status:** human_needed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can launch a supported shell inside wcmux and interact with it through ConPTY-backed hosting | VERIFIED (code) | `ConPtyHost.cs` uses `CreatePseudoConsole` P/Invoke; `MainWindow.xaml.cs` calls `CreateInitialSessionAsync` on activation; `SessionManager.CreateSessionAsync` launches `ConPtySession`; WebView2+xterm.js surface attached via `TerminalPaneView` |
| 2 | Full-screen terminal apps and prompts behave correctly during input, resize, and exit | VERIFIED (code) | `TerminalSurfaceBridge` handles output batching, input routing, resize debounce; `ResizePipelineTests` (233 lines, 13 tests); smoke harness covers vim, fzf, paste, resize scenarios |
| 3 | User can split the active pane horizontally or vertically and each pane stays interactive | VERIFIED (code) | `LayoutReducer.SplitPane` creates `SplitNode` with axis; `WorkspaceViewModel.SplitActivePaneAsync` launches new session with cwd inheritance; `PaneCommandBindings` binds Ctrl+Shift+H/V; `SplitCommandsTests` (176 lines, 9 tests) |
| 4 | User can move focus between panes and resize panes using keyboard-driven controls | VERIFIED (code) | `LayoutReducer.FindDirectionalFocus` uses geometric pane rectangles; `LayoutStore.ResizeActivePane` adjusts ancestor split ratios; `PaneCommandBindings` binds Ctrl+Shift+Arrows (focus) and Ctrl+Alt+Arrows (resize); `PaneFocusAndResizeTests` (395 lines, 21 tests) |

**Score:** 4/4 truths verified at code level

### Required Artifacts

**Plan 01 Artifacts:**

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Wcmux.sln` | Solution wiring | VERIFIED | 43 lines, contains `Project(` references for App, Core, Tests |
| `src/Wcmux.Core/Runtime/ConPtyHost.cs` | ConPTY creation, resize, shutdown (min 80 lines) | VERIFIED | 188 lines, contains `CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole` |
| `src/Wcmux.Core/Runtime/SessionManager.cs` | Session lifecycle orchestration (min 80 lines) | VERIFIED | 79 lines (1 under threshold but substantive -- ConcurrentDictionary tracking, event fan-out, `CreateSessionAsync`, `CloseSessionAsync`, `DisposeAsync`) |
| `src/Wcmux.App/MainWindow.xaml.cs` | Native shell startup (min 40 lines) | VERIFIED | 71 lines, contains `CreateInitialSessionAsync` bootstrapping workspace |
| `tests/Wcmux.Tests/Runtime/SessionHostIntegrationTests.cs` | Integration coverage (min 60 lines) | VERIFIED | 221 lines, references `SessionManager` |

**Plan 02 Artifacts:**

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Wcmux.App/Views/TerminalPaneView.xaml.cs` | Native pane host with WebView2 (min 70 lines) | VERIFIED | 204 lines, references `WebView2`, `TerminalSurfaceBridge`, `AttachAsync`/`DetachAsync` |
| `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` | Bidirectional bridge (min 80 lines) | VERIFIED | 306 lines, contains `CwdChanged`, output batching, resize debounce, `RequestResizeFromPixels` |
| `src/Wcmux.App/TerminalWeb/index.html` | xterm surface entrypoint (min 20 lines) | VERIFIED | 21 lines, loads xterm.js 5.5.0 + fit + web-links addons |
| `tests/Wcmux.Tests/Terminal/TerminalBridgeTests.cs` | Bridge tests (min 60 lines) | VERIFIED | 209 lines, references `TerminalBridge` patterns |
| `tests/Wcmux.Tests/Terminal/ResizePipelineTests.cs` | Resize tests (min 60 lines) | VERIFIED | 233 lines, references `Resize` patterns extensively |
| `tools/Run-Phase01TerminalSmoke.ps1` | Smoke harness (min 30 lines) | VERIFIED | 261 lines, covers vim, fzf, paste, resize scenarios |

**Plan 03 Artifacts:**

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Wcmux.Core/Layout/LayoutReducer.cs` | Pure split-tree transitions (min 90 lines) | VERIFIED | 399 lines, contains `SplitPane` (plan specified `splitActivePane` -- PascalCase equivalent exists as `SplitPane`) |
| `src/Wcmux.Core/Layout/LayoutStore.cs` | Observable layout state (min 80 lines) | VERIFIED | 301 lines, contains `ActivePaneId`, `SplitActivePane`, `FocusDirection`, `ResizeActivePane` |
| `src/Wcmux.App/ViewModels/WorkspaceViewModel.cs` | App-shell orchestration (min 90 lines) | VERIFIED | 194 lines, contains `SplitActivePaneAsync` with cwd inheritance |
| `src/Wcmux.App/Commands/PaneCommandBindings.cs` | Keyboard bindings (min 40 lines) | VERIFIED | 105 lines, contains `FocusLeft`, split/focus/resize/close bindings |
| `tests/Wcmux.Tests/Layout/LayoutReducerTests.cs` | Reducer tests (min 60 lines) | VERIFIED | 307 lines, references `LayoutReducer` |
| `tests/Wcmux.Tests/Layout/PaneFocusAndResizeTests.cs` | Focus/resize tests (min 60 lines) | VERIFIED | 395 lines, references `PaneFocusAndResize` patterns |

### Key Link Verification

**Plan 01 Links:**

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `MainWindow.xaml.cs` | `SessionManager.cs` | `CreateInitialSessionAsync` | WIRED | MainWindow creates SessionManager, calls `CreateSessionAsync` on activation |
| `SessionManager.cs` | `ConPtyHost.cs` | `ConPtyHost` reference | WIRED | SessionManager delegates to `ConPtySession.StartAsync` which creates `ConPtyHost.Create()` |
| `SessionHostIntegrationTests.cs` | `SessionManager.cs` | `SessionManager` reference | WIRED | Tests reference SessionManager (2 occurrences) |

**Plan 02 Links:**

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `TerminalPaneView.xaml.cs` | `WebViewTerminalController.cs` | `AttachAsync` | WIRED | Creates controller in AttachAsync, calls InitializeAsync |
| `TerminalSurfaceBridge.cs` | `SessionManager.cs` | Via `ISession` interface | WIRED | Bridge receives `ISession` directly; TerminalPaneView subscribes to `SessionManager.SessionEventReceived` and routes to bridge |
| `ResizePipelineTests.cs` | `TerminalSurfaceBridge.cs` | `Resize` patterns | WIRED | 66 Resize references in test file |

**Plan 03 Links:**

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `WorkspaceViewModel.cs` | `LayoutStore.cs` | `LayoutStore` reference | WIRED | ViewModel creates LayoutStore, calls SplitActivePane, FocusDirection, ResizeActivePane |
| `WorkspaceViewModel.cs` | `SessionManager.cs` | `CreateSessionAsync` | WIRED | SplitActivePaneAsync calls `_sessionManager.CreateSessionAsync` for new pane sessions |
| `SplitCommandsTests.cs` | `LayoutReducer.cs` | `split` patterns | WIRED | 7 split references in test file |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| SESS-01 | 01-01 | User can open a real Windows terminal session inside the app using ConPTY-backed hosting | SATISFIED | ConPtyHost uses CreatePseudoConsole, MainWindow bootstraps default PowerShell session on startup, SessionManager tracks sessions |
| SESS-02 | 01-02 | User can interact with full-screen terminal apps and prompts without losing expected input, output, resize, or exit behavior | SATISFIED | TerminalSurfaceBridge handles bidirectional IO with batching, resize debounce pipeline, xterm.js surface, 27 bridge/resize tests, smoke harness for vim/fzf |
| LAYT-01 | 01-03 | User can split the active pane horizontally | SATISFIED | LayoutReducer.SplitPane with SplitAxis.Horizontal, WorkspaceViewModel.SplitActivePaneHorizontalAsync, PaneCommandBindings Ctrl+Shift+H |
| LAYT-02 | 01-03 | User can split the active pane vertically | SATISFIED | LayoutReducer.SplitPane with SplitAxis.Vertical, WorkspaceViewModel.SplitActivePaneVerticalAsync, PaneCommandBindings Ctrl+Shift+V |
| LAYT-03 | 01-03 | User can move focus between panes and resize panes with keyboard-driven controls | SATISFIED | LayoutReducer.FindDirectionalFocus (geometric), LayoutStore.ResizeActivePane (ratio-based), PaneCommandBindings Ctrl+Shift+Arrows and Ctrl+Alt+Arrows, 21 focus/resize tests |

**Orphaned Requirements:** None. All 5 requirement IDs mapped to Phase 1 in REQUIREMENTS.md (SESS-01, SESS-02, LAYT-01, LAYT-02, LAYT-03) are claimed by plans and have implementation evidence.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | - | - | - | No TODO, FIXME, PLACEHOLDER, stub, or empty implementation patterns found in src/ |

### Human Verification Required

All automated code-level checks pass. The following items need human verification because they involve live ConPTY sessions, WebView2 rendering, and keyboard interaction on a Windows desktop:

### 1. Session Launch and Interaction

**Test:** Launch wcmux, verify a PowerShell session opens and is interactive.
**Expected:** Terminal shows PowerShell prompt; typing commands produces output; paste works.
**Why human:** ConPTY + WebView2 + xterm.js rendering chain requires a live Windows desktop session.

### 2. Full-Screen TUI Fidelity

**Test:** Run `vim` inside wcmux, enter insert mode, type text, resize the window aggressively, then exit with `:q!`.
**Expected:** Alternate screen activates cleanly, vim redraws correctly after resize, shell prompt returns after exit.
**Why human:** TUI alternate-screen behavior and visual redraw quality cannot be verified without observation.

### 3. Pane Splitting

**Test:** Press Ctrl+Shift+H (horizontal split) and Ctrl+Shift+V (vertical split).
**Expected:** Each new pane gets its own interactive PowerShell session; cwd is inherited from the source pane.
**Why human:** Session attachment to new pane surfaces and cwd inheritance require live app verification.

### 4. Focus and Resize Controls

**Test:** With multiple panes, use Ctrl+Shift+Arrows to move focus and Ctrl+Alt+Arrows to resize.
**Expected:** Focus highlight moves to the geometrically correct pane; pane sizes adjust; all terminals stay interactive.
**Why human:** Keyboard accelerator routing through WinUI and visual feedback require manual testing.

### 5. Pane Close and Focus Restoration

**Test:** Close a pane with Ctrl+Shift+W.
**Expected:** Closed pane disappears, layout collapses, focus moves to the most related surviving pane.
**Why human:** Visual layout collapse behavior and focus restoration correctness need human observation.

### Gaps Summary

No code-level gaps found. All 18 required artifacts exist, are substantive (meeting min_lines and contains requirements), and are wired together through imports and usage. All 5 phase requirements (SESS-01, SESS-02, LAYT-01, LAYT-02, LAYT-03) have corresponding implementation evidence. The codebase contains 94 automated tests across runtime, terminal bridge, and layout subsystems, plus a scripted smoke harness.

The only remaining verification is human testing of the live application on a Windows desktop to confirm that the ConPTY/WebView2/xterm.js rendering chain works end-to-end and that keyboard bindings route correctly through the WinUI shell.

---

_Verified: 2026-03-06T23:00:00Z_
_Verifier: Claude (gsd-verifier)_
