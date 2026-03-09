# Project Research Summary

**Project:** wcmux v1.1 UI/UX Overhaul
**Domain:** WinUI 3 terminal multiplexer -- custom chrome, vertical tabs, process detection, browser panes
**Researched:** 2026-03-08
**Confidence:** HIGH

## Executive Summary

wcmux v1.1 is a UI/UX overhaul of an existing, functional WinUI 3 terminal multiplexer. The v1.0 foundation (ConPTY sessions, immutable layout reducer, split panes, tabs, attention/notification system) is solid and working. The v1.1 scope adds four features: custom dark title bar, vertical tab sidebar with output preview, pane title bars with foreground process detection, and browser pane hosting. Critically, **no new NuGet packages are required** -- every feature builds on the existing Windows App SDK 1.8 and WebView2 stack.

The recommended approach is to build incrementally in dependency order: title bar first (isolated, visual foundation), then pane title bars with process detection (architecturally invasive, changes pane container structure), then vertical sidebar (view rewrite with new data flow), then browser panes (depends on pane type metadata and title bar actions). The key architectural decisions are: use `InputNonClientPointerSource` (not `SetTitleBar`) for the custom title bar, use ToolHelp32 P/Invoke (not WMI) for process detection, use text-based output preview from xterm.js buffer (not WebView2 screenshots), and share a single `CoreWebView2Environment` across all WebView2 instances.

The top risks are: (1) WinUI 3 title bar drag region bugs that break interactive controls after window drag -- mitigated by avoiding `SetTitleBar` entirely, (2) WebView2 keyboard focus conflicts when adding sidebar and browser panes as new focus targets -- mitigated by routing shortcuts through JavaScript and managing focus explicitly, and (3) process tree heuristic showing wrong foreground process -- mitigated by preferring deepest tree leaf over newest PID and accepting shell name as fallback. A prerequisite task -- retrofitting a shared `CoreWebView2Environment` -- should be done first to prevent memory bloat as pane count grows.

## Key Findings

### Recommended Stack

No new dependencies. All v1.1 features are built with the existing stack: Windows App SDK 1.8 (`TitleBar` control or `ExtendsContentIntoTitleBar`), WebView2 1.0.3179.45 (browser panes, `CapturePreviewAsync`), and kernel32.dll P/Invoke (process detection). See [STACK.md](./STACK.md) for full details.

**Core technologies (additions only):**
- `InputNonClientPointerSource` API: custom title bar drag regions -- avoids the `SetTitleBar` bug that breaks interactive controls
- ToolHelp32 P/Invoke (`CreateToolhelp32Snapshot` / `Process32First` / `Process32Next`): foreground process detection -- ~1ms, no WMI overhead, same pattern as WezTerm and Windows Terminal
- `CoreWebView2.CapturePreviewAsync`: optional visual thumbnail for sidebar hover -- `RenderTargetBitmap` cannot capture WebView2 content
- Shared `CoreWebView2Environment`: single browser process tree for all WebView2 instances -- prevents 80-150MB per-pane memory bloat

### Expected Features

See [FEATURES.md](./FEATURES.md) for full analysis and dependency graph.

**Must have (P1 -- v1.1 launch):**
- Custom dark title bar -- eliminates the most visible "unfinished app" signal
- Vertical tab sidebar with label + cwd -- replaces horizontal tabs, shows more context per workspace
- Pane title bars with foreground process name -- orients users across multiple splits
- Pane close and split buttons -- discoverable mouse affordances for core actions

**Should have (P2 -- v1.1.x patches):**
- Output preview in sidebar tabs -- monitor agent progress without switching tabs
- Browser pane hosting -- Chromium pane alongside terminals for AI coding workflows
- Sidebar attention indicators -- extend existing attention system to new sidebar
- Drag-to-resize pane borders -- mouse-based resize on existing `LayoutReducer`

**Defer (v1.2+):**
- Git branch / PR status in sidebar -- significant new infrastructure
- Tab drag-and-drop reordering -- WinUI 3 DnD is finicky for custom UI
- Per-pane scrollback search -- non-trivial xterm.js search addon integration

### Architecture Approach

The v1.1 overhaul preserves all existing architectural invariants: LayoutStore owns geometry, SessionManager is the event bus, WorkspaceViewModel bridges layout and sessions. New components integrate through existing patterns. The main structural change is `MainWindow.xaml` shifting from row-based (horizontal tabs on top) to column-based (sidebar on left). Pane title bar actions route through WorkspaceViewModel, never directly to LayoutStore. See [ARCHITECTURE.md](./ARCHITECTURE.md) for full component map and data flows.

**New components:**
1. `TabSidebarView` -- replaces `TabBarView`, vertical list with cwd + output preview
2. `ProcessDetector` -- polls process tree per session on 1-2s timer, emits `SessionProcessChangedEvent`
3. `OutputRingBuffer` -- circular buffer of recent terminal output for sidebar preview
4. `BrowserPaneView` -- WebView2 navigated to user URL with address bar and navigation controls
5. `PaneTitleBar` -- inline in WorkspaceView, shows process name + action buttons per pane

**Key layout model change:** Add `PaneKind` enum (`Terminal | Browser`) and `BrowserUrl` to `LeafNode` so the layout tree supports both pane types while remaining type-agnostic for geometry computation.

### Critical Pitfalls

See [PITFALLS.md](./PITFALLS.md) for full details, recovery strategies, and "looks done but isn't" checklist.

1. **Title bar drag region breakage** -- `SetTitleBar()` + interactive controls = broken input after window drag. Use `InputNonClientPointerSource` with `Passthrough` rects exclusively. Recalculate on `SizeChanged`.
2. **WebView2 process explosion** -- Without shared `CoreWebView2Environment`, each pane spawns a full browser process group (80-150MB). Retrofit shared environment as Phase 1 prerequisite.
3. **WebView2 keyboard focus black hole** -- Chromium message loop intercepts keyboard events before WinUI. Route shortcuts through JavaScript. Explicitly manage focus when switching between sidebar, terminal, and browser panes.
4. **Wrong foreground process detection** -- Heuristic breaks with background watchers and deep npm/node trees. Prefer deepest tree leaf, limit depth to 5-6 levels, fall back to shell name.
5. **Sidebar breaks pane absolute positioning** -- Adding sidebar column shifts coordinate space for `WorkspaceView`. Verify `UpdateContainerSize` uses `RootContainer.ActualWidth`, not window width.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Custom Title Bar + WebView2 Shared Environment

**Rationale:** Title bar is the smallest, most isolated change. Establishing the shared WebView2 environment is a prerequisite for all subsequent phases that add WebView2 instances. Doing both together sets the visual and resource foundation.
**Delivers:** Dark custom title bar matching app aesthetic, shared `CoreWebView2Environment` reducing per-pane memory.
**Addresses:** Custom dark title bar (P1 table stakes).
**Avoids:** Pitfall 1 (drag region breakage -- use `InputNonClientPointerSource`), Pitfall 4 (process explosion -- shared environment).
**Stack:** `InputNonClientPointerSource` API, `CoreWebView2Environment.CreateAsync`.

### Phase 2: Pane Title Bars + Foreground Process Detection

**Rationale:** This is the most architecturally invasive change -- it adds a new background service (`ProcessDetector`), modifies the pane container structure in `WorkspaceView`, adds new event types, and exposes `ShellProcessId` on `ISession`. Must be done before the sidebar (which depends on the pane container structure being stable) and before browser panes (which need the title bar action button and `PaneKind` discriminator on `LeafNode`).
**Delivers:** Per-pane title bars showing foreground process name + cwd, close/split action buttons.
**Addresses:** Pane title bars (P1), pane close button (P1), pane split buttons (P1).
**Avoids:** Pitfall 3 (wrong process detection -- use ToolHelp32, prefer deepest leaf, cache + debounce).
**Stack:** ToolHelp32 P/Invoke, new `ProcessDetector` service, `SessionProcessChangedEvent`.

### Phase 3: Vertical Tab Sidebar + Output Preview

**Rationale:** Depends on Phase 1 (MainWindow layout restructure needs title bar row established first) and benefits from Phase 2 (pane container structure is stable). This phase replaces `TabBarView` with `TabSidebarView`, changes `MainWindow.xaml` from rows to columns, and adds the `OutputRingBuffer` data pipeline.
**Delivers:** Vertical sidebar with tab labels, cwd, output preview text, attention indicators.
**Addresses:** Vertical tab sidebar (P1), output preview (P2), sidebar attention indicators (P2).
**Avoids:** Pitfall 5 (sidebar breaks pane layout -- verify `UpdateContainerSize`), Pitfall 6 (preview performance -- timer-based sampling, extract text from xterm.js via `postMessage`).
**Stack:** Custom `TabSidebarView` UserControl, `OutputRingBuffer`, ANSI stripping in JavaScript.

### Phase 4: Browser Pane Hosting

**Rationale:** Depends on Phase 2 (needs `PaneKind` on `LeafNode` and browser action button in pane title bar) and Phase 1 (shared WebView2 environment). Browser pane is a high-value differentiator but is the least coupled to the core terminal experience.
**Delivers:** WebView2 browser panes in split tree with address bar, navigation, and proper security settings.
**Addresses:** Browser pane hosting (P2).
**Avoids:** Pitfall 2 (focus black hole -- explicit focus management between terminal and browser WebView2s), Pitfall 4 (shared environment already in place from Phase 1).
**Stack:** `BrowserPaneView` UserControl, `PaneKind` enum, browser-specific WebView2 settings.

### Phase 5: Polish + Mouse Resize

**Rationale:** Drag-to-resize is independent of all other features and builds on the existing `LayoutReducer.ResizePane`. Best done after all layout changes are stable.
**Delivers:** Mouse drag-to-resize on pane borders.
**Addresses:** Drag-to-resize pane borders (P2).

### Phase Ordering Rationale

- Title bar and shared WebView2 environment are prerequisites with no downstream blockers -- do first.
- Pane title bars modify the pane container structure in `WorkspaceView`, which the sidebar and browser pane both depend on -- do second.
- Sidebar is a large view rewrite but architecturally self-contained once the MainWindow layout is columnar -- do third.
- Browser panes need the `PaneKind` model change and title bar action button, both from Phase 2 -- do last.
- Mouse resize is fully independent and best done after layout churn has settled.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1 (Title Bar):** The `InputNonClientPointerSource` API has known bugs (post-drag rect shrinkage). Needs hands-on prototyping to verify the recalculate-on-SizeChanged workaround.
- **Phase 3 (Sidebar + Output Preview):** The xterm.js buffer extraction and ANSI stripping approach needs a proof-of-concept to validate the `postMessage` data flow from JavaScript to C#.

Phases with standard patterns (skip deep research):
- **Phase 2 (Process Detection):** ToolHelp32 P/Invoke is well-documented with clear examples from WezTerm and Windows Terminal. Standard pattern.
- **Phase 4 (Browser Pane):** WebView2 browser hosting is straightforward -- navigate to URL, handle events. The main work is UI (address bar, focus management).
- **Phase 5 (Mouse Resize):** Hit-testing and drag translation to existing `ResizePane` is a standard UI interaction pattern.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All recommendations verified against official Microsoft docs (SDK 1.8, WebView2). No new packages needed. |
| Features | MEDIUM-HIGH | Feature priorities clear. Dependency graph well-mapped. Output preview approach (text vs screenshot) is a judgment call but well-reasoned. |
| Architecture | HIGH | Existing codebase fully analyzed. Integration points identified with specific file and line references. Patterns preserve existing invariants. |
| Pitfalls | MEDIUM-HIGH | WinUI 3 title bar bugs are well-documented in GitHub issues. WebView2 focus issues are known. Process detection heuristic limitations are inherent, not solvable. |

**Overall confidence:** HIGH

### Gaps to Address

- **`InputNonClientPointerSource` post-drag rect bug:** Workaround (recalculate on `SizeChanged`) is documented but not verified in this app's layout. Validate in Phase 1 prototype.
- **ANSI stripping approach:** Research recommends extracting clean text from xterm.js buffer in JavaScript rather than parsing VT sequences in C#. The exact `terminal.buffer.active.getLine()` API usage needs validation with the current xterm.js version.
- **Alternate screen buffer detection for preview:** When a user runs `vim` or `less`, the output preview would show gibberish. Need to detect alternate screen mode and display a label instead. The detection mechanism (xterm.js `terminal.buffer.active` vs `terminal.buffer.alternate`) needs testing.
- **STACK.md vs ARCHITECTURE.md disagreement on title bar approach:** STACK.md recommends the `TitleBar` control (SDK 1.7+), while ARCHITECTURE.md and PITFALLS.md recommend `ExtendsContentIntoTitleBar` + `InputNonClientPointerSource`. The latter is safer given the documented `SetTitleBar` bugs. **Recommendation: use `InputNonClientPointerSource` approach.**
- **Process detection: ARCHITECTURE.md suggests WMI, STACK.md and PITFALLS.md recommend ToolHelp32.** ToolHelp32 is clearly better (~1ms vs 50-200ms). **Recommendation: use ToolHelp32 P/Invoke.**

## Sources

### Primary (HIGH confidence)
- [Microsoft TitleBar Control API Reference (SDK 1.8)](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.titlebar?view=windows-app-sdk-1.8)
- [Title bar customization guide (Windows App SDK)](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar)
- [WebView2 Process Model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model)
- [WebView2 performance best practices](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)
- [CreateToolhelp32Snapshot docs](https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/nf-tlhelp32-createtoolhelp32snapshot)
- [WezTerm foreground process detection](https://wezterm.org/config/lua/pane/get_foreground_process_info.html)

### Secondary (MEDIUM confidence)
- [WinUI 3 title bar drag region issues - GitHub #6993, #9463, #7259, #8976](https://github.com/microsoft/microsoft-ui-xaml/issues/6993) -- multiple corroborating reports
- [WebView2 KeyboardAccelerator double-fire - GitHub #6231](https://github.com/microsoft/microsoft-ui-xaml/issues/6231) -- confirmed WinUI 3 defect
- [WebView2 process leak on dispose - GitHub #9088, #3378](https://github.com/microsoft/microsoft-ui-xaml/issues/9088) -- community-reported, widely corroborated
- [RenderTargetBitmap cannot capture WebView2 content](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1433)

### Tertiary (LOW confidence)
- Alternate screen buffer detection via xterm.js buffer API -- inferred from xterm.js docs, needs validation

---
*Research completed: 2026-03-08*
*Ready for roadmap: yes*
