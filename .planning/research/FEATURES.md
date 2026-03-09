# Feature Research

**Domain:** Terminal multiplexer UI/UX overhaul (v1.1)
**Researched:** 2026-03-08
**Confidence:** MEDIUM-HIGH

## Feature Landscape

This research focuses exclusively on the v1.1 UI/UX features: custom title bar, vertical tab sidebar with output preview, pane title bars with process detection, and browser pane hosting. All v1.0 features (ConPTY sessions, splits, tabs, attention system, notifications) are already built and working.

### Table Stakes (Users Expect These)

Features that users of a polished terminal multiplexer assume exist. Missing these makes the app feel unfinished.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Custom dark title bar | Default Windows chrome (white/system-colored) clashes with dark terminal backgrounds. Every serious terminal app (Windows Terminal, WezTerm, Alacritty) does this. | LOW | WinUI 3 supports `ExtendsContentIntoTitleBar` + `SetTitleBar` natively. Windows App SDK 1.7 added a dedicated TitleBar control that simplifies this further. Must set in code-behind, not XAML (known WinUI limitation). Must manually handle caption button colors for dark theme via `AppWindowTitleBar` color properties. Depends on: nothing (purely visual). |
| Pane title bars with process name | Users need to know what is running in each pane at a glance. tmux shows pane titles, Windows Terminal shows tab process names, cmux shows process name in pane headers. Without this, 3+ panes are disorienting. | MEDIUM | Requires walking the ConPTY child process tree to find the deepest (foreground) process. Use `Process.GetProcessById` on the stored shell PID, then enumerate children via WMI (`Win32_Process` where `ParentProcessId` matches) or toolhelp32 snapshot APIs. Poll on a timer (1-2s). Current `ISession` tracks `LastKnownCwd` but not foreground process -- needs new capability. |
| Pane close button | Every pane should be individually closable without keyboard shortcuts. Standard affordance in all split-pane UIs. | LOW | Already have close-pane keyboard command. Add an X button to the pane title bar. Wire to existing `CloseActivePaneAsync`. |
| Vertical tab sidebar replacing horizontal tabs | Horizontal tabs cannot show enough context when running 5+ agent workspaces. Every cmux-inspired workflow uses vertical tabs because each tab needs room for metadata (label, cwd, status). This is the defining UX change of v1.1. | HIGH | Replaces current horizontal `TabBarView` with a vertical sidebar panel. Requires restructuring `MainWindow.xaml` from row-based layout (top tabs + bottom content) to column-based (left sidebar + right content). Each tab entry needs: label, truncated cwd. The cwd is already tracked via OSC 7 in `ConPtySession`. |

### Differentiators (Competitive Advantage)

Features that set wcmux apart from basic terminal apps and move toward cmux parity.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Output preview in sidebar tabs | cmux's sidebar shows the latest notification text per workspace. Showing the last 1-2 lines of terminal output in each sidebar tab lets you monitor agent progress across workspaces without switching. No Windows terminal app does this. | MEDIUM | Requires buffering the last N lines of terminal output per session. The current `SessionOutputEvent` streams raw VT text but does not retain it. Add a small ring buffer (~5 lines) per session that strips ANSI escape sequences and stores plain text. Display the last line or two in the sidebar below the tab label. |
| Browser pane hosting | Split a Chromium-based browser pane alongside terminals. Critical for AI coding workflows where agents reference localhost dev servers, docs, or PRs. cmux has this and it is a key selling point. | MEDIUM | WebView2 is already used for xterm.js rendering, so the infrastructure exists. A browser pane is a WebView2 pointed at a user URL instead of xterm.js. Needs: address bar, back/forward/reload controls, URL display. Must handle navigation events and SSL errors. Does NOT need to share WebView2 environment with terminal panes. Requires adding pane-type metadata to `LayoutNode` (currently assumes all panes are terminals). |
| Pane split buttons in title bar | Quick-access buttons for splitting horizontally or vertically directly from the pane title bar. Lowers the barrier for mouse-oriented users. The current hover-only `+` button (bottom-right corner) is not discoverable enough. | LOW | Add two small icon buttons to the pane title bar. Wire to existing split commands. |
| Sidebar attention indicators | When a pane has attention (bell), its tab in the sidebar should show a visual indicator (blue highlight, dot, or text color). Extends the existing attention system to the new sidebar layout. | LOW | Existing `AttentionStore` and `TabHasAttention` already power horizontal tab indicators. Port the same logic to vertical sidebar entries. Near-zero new logic required. |
| Drag-to-resize pane borders | Users expect to grab the border between panes and drag to resize. Current keyboard-only resize is functional but not discoverable. | MEDIUM | Requires hit-testing on pane boundary regions in `WorkspaceView` and translating drag deltas into resize ratios. The current `LayoutReducer` already supports ratio-based resizing via `ResizePane`, so this is about adding the mouse interaction layer. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem appealing but should be explicitly avoided in v1.1.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Thumbnail/minimap pane preview in sidebar | cmux-style live terminal preview per tab | Rendering multiple live terminal thumbnails needs either separate xterm.js instances or canvas snapshot pipelines. Massive complexity for marginal value over text preview. | 1-2 line text preview of recent output. Cheap (string buffer), provides 80% of the value. |
| Full browser dev tools in browser pane | Users want Chrome DevTools inside embedded browser | Creates a browser-within-a-browser complexity spiral. Focus drifts from terminal multiplexer to browser IDE. | Enable DevTools only in debug builds. Production gets basic navigation only (URL bar, back/forward/reload). |
| Tab drag-and-drop reordering | Users want to drag tabs to reorder | Drag-and-drop in WinUI 3 is finicky for custom-rendered UI. High effort for a feature used once per session at most. | Keyboard shortcuts for tab reordering (move-tab-up/down). Add DnD in a future release. |
| Git branch / PR status in sidebar | cmux shows git branch and linked PR per workspace | Requires git CLI integration, GitHub API, and a "workspace = repo" concept. Significant scope expansion beyond UI/UX overhaul. | Defer to v1.2+. Process name + cwd in pane title bar provides enough orientation. |
| Per-pane scrollback search | Full-text search across terminal scrollback | xterm.js has a search addon, but wiring it to visible UI (search bar, highlights, cross-pane coordination) is non-trivial. | Defer. Users can use shell-level search (Ctrl+R, grep, etc.). |

## Feature Dependencies

```
[Custom Dark Title Bar]
    (no dependencies -- purely visual, do first)

[Pane Title Bar]
    +-- requires --> [Foreground Process Detection] (new ConPtySession capability)
    +-- enables --> [Pane Close Button]
    +-- enables --> [Pane Split Buttons]
    +-- enables --> [Browser Pane launch button]

[Vertical Tab Sidebar]
    +-- requires --> [MainWindow layout restructure: rows -> columns]
    +-- replaces --> [Existing horizontal TabBarView]
    +-- enhances with --> [Sidebar Attention Indicators]

[Output Preview in Sidebar]
    +-- requires --> [Vertical Tab Sidebar] (needs sidebar to exist)
    +-- requires --> [Output Ring Buffer per Session] (new SessionManager capability)

[Browser Pane]
    +-- requires --> [Pane type metadata in LayoutNode]
    +-- requires --> [Pane Title Bar] (browser needs URL bar in title area)
    +-- uses --> [Existing WebView2 infrastructure]

[Drag-to-Resize Pane Borders]
    (independent -- mouse interaction layer on existing LayoutReducer)
```

### Dependency Notes

- **Pane Title Bar requires Foreground Process Detection:** The title bar's primary value is showing the running process name. Without process detection, the title bar only shows cwd (which the existing text overlay already does). Must add process tree walking to `ConPtySession` or a dedicated `ProcessMonitor` service that polls child processes of the shell PID stored during `LaunchWithPseudoConsole`.
- **Vertical Tab Sidebar requires MainWindow restructure:** Current `MainWindow.xaml` uses `Grid.RowDefinitions` with `TabBarView` in row 0 and `TabContentArea` in row 1. Sidebar needs `Grid.ColumnDefinitions` with sidebar in column 0 and content in column 1. This is a one-time structural change.
- **Output Preview requires Ring Buffer:** Currently `SessionOutputEvent` is fire-and-forget -- output goes to xterm.js and is not retained. Need a small circular buffer (~5 lines of ANSI-stripped plain text) per session for the sidebar to display.
- **Browser Pane requires pane type metadata:** Current `WorkspaceView.CreatePaneViewAsync` always creates `TerminalPaneView`. Must support a second pane type. `LayoutNode` tracks pane IDs but not types -- needs a `PaneType` enum (Terminal, Browser) and the URL to navigate to.

## MVP Definition

### Launch With (v1.1)

Minimum for the UI/UX overhaul to feel complete.

- [ ] Custom dark title bar -- eliminates the most visible "unfinished app" signal
- [ ] Vertical tab sidebar with label + cwd display -- replaces horizontal tabs, shows more context
- [ ] Pane title bars with foreground process name -- orients users across multiple pane splits
- [ ] Pane close and split buttons in title bar -- discoverable mouse affordances for core actions

### Add After Validation (v1.1.x)

Features to add once the core UI overhaul is stable.

- [ ] Output preview in sidebar tabs -- requires ring buffer, can add incrementally
- [ ] Browser pane hosting -- high-value but can ship after core UI is solid
- [ ] Sidebar attention indicators -- port existing attention logic to new sidebar
- [ ] Drag-to-resize pane borders -- important polish, not blocking

### Future Consideration (v1.2+)

Features to defer until the overhaul is proven.

- [ ] Git branch / PR status in sidebar -- significant new infrastructure
- [ ] Tab drag-and-drop reordering -- rarely critical
- [ ] Per-pane scrollback search -- xterm.js search addon integration
- [ ] Sidebar collapse/expand toggle -- useful but not urgent

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Custom dark title bar | HIGH | LOW | P1 |
| Vertical tab sidebar (label + cwd) | HIGH | HIGH | P1 |
| Pane title bars + process name | HIGH | MEDIUM | P1 |
| Pane close/split buttons | MEDIUM | LOW | P1 |
| Output preview in sidebar | MEDIUM | MEDIUM | P2 |
| Browser pane hosting | HIGH | MEDIUM | P2 |
| Sidebar attention indicators | MEDIUM | LOW | P2 |
| Drag-to-resize pane borders | MEDIUM | MEDIUM | P2 |
| Git branch in sidebar | LOW | HIGH | P3 |
| Tab reordering (keyboard) | LOW | LOW | P3 |
| Tab reordering (drag-and-drop) | LOW | MEDIUM | P3 |

**Priority key:**
- P1: Must have for v1.1 launch
- P2: Should have, add in v1.1.x patches
- P3: Nice to have, defer to v1.2+

## Competitor Feature Analysis

| Feature | cmux (macOS) | Windows Terminal | WezTerm | wcmux v1.1 Approach |
|---------|-------------|-----------------|---------|----------------------|
| Title bar | Native macOS chrome | Custom with tabs embedded in title bar | Custom with tab bar | Custom dark title bar via `ExtendsContentIntoTitleBar`, app name + window controls only |
| Tab style | Vertical sidebar: label, git branch, PR, cwd, ports, notification text | Horizontal tabs with profile icon + process name | Horizontal tabs with process name | Vertical sidebar: label, cwd, output preview (text, not thumbnail) |
| Pane title bar | Process name + visual indicators | None (panes have no individual headers) | Pane title via OSC escape sequences | Thin title bar: process name, close, split-h, split-v, browser buttons |
| Process detection | Provided by libghostty | Win32 console APIs internally | Cross-platform process query per OS | Walk ConPTY child process tree via toolhelp32 snapshot or WMI |
| Browser pane | Built-in WebKit browser, scriptable | None | None | WebView2 browser pane with address bar and basic navigation |
| Output preview | Last notification text in sidebar tab | None | None | Ring buffer of last ~5 lines per session, ANSI-stripped, last 1-2 lines shown in sidebar |
| Attention in sidebar | Blue ring on pane, tab lights up | Tab title flash (limited) | Bell support only | Existing: blinking borders + tab color + toast. Extend: sidebar tab highlight. |

## Sources

- [cmux GitHub - manaflow-ai/cmux](https://github.com/manaflow-ai/cmux) -- feature reference for vertical sidebar, browser pane, notification system
- [cmux website](https://www.cmux.dev/) -- product description and keyboard shortcuts
- [WinUI 3 Title Bar Customization - Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar) -- official docs for ExtendsContentIntoTitleBar, AppWindowTitleBar
- [WinUI 3 TitleBar control - Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/design/controls/title-bar) -- Windows App SDK 1.7 TitleBar control
- [WebView2 in WinUI 3 - Microsoft Learn](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winui) -- browser pane hosting via WebView2
- [Creating a Pseudoconsole session - Microsoft Learn](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session) -- ConPTY process management
- [WezTerm foreground process detection](https://softkube.com/blog/displaying-current-running-process-and-its-arguments-window-title-bar-wezterm) -- pattern reference for process name in pane title
- [Hacker News cmux discussion](https://news.ycombinator.com/item?id=47079718) -- community feedback on vertical tab sidebar UX
- [WinUI 3 dark theme title bar discussion](https://github.com/microsoft/microsoft-ui-xaml/issues/2484) -- known limitation requiring manual dark theme handling

---
*Feature research for: wcmux v1.1 UI/UX overhaul*
*Researched: 2026-03-08*
