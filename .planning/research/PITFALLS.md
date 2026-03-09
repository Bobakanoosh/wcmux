# Pitfalls Research

**Domain:** v1.1 UI/UX overhaul for WinUI 3 terminal multiplexer (custom title bar, vertical tab sidebar, process detection, browser panes)
**Researched:** 2026-03-08
**Confidence:** MEDIUM-HIGH

## Critical Pitfalls

### Pitfall 1: Custom title bar drag regions break interactive controls

**What goes wrong:**
After setting `ExtendsContentIntoTitleBar = true` and placing UI elements (tab sidebar toggle, search, window controls) in the title bar area, those controls stop receiving pointer input. Flyouts opened over the title bar region also become non-interactive. The caption buttons (min/max/close) can register phantom clicks when the mouse has moved off them.

**Why it happens:**
WinUI 3 has two conflicting APIs for defining drag regions: `Window.SetTitleBar(element)` and the lower-level `InputNonClientPointerSource.SetRegionRects()`. Mixing them causes undefined behavior. `SetTitleBar` marks the element as the drag region, but any interactive children inside that element are silently eaten unless explicitly carved out. The `InputNonClientPointerSource.SetRegionRects` approach with `NonClientRegionKind.Passthrough` is the correct escape hatch, but has its own bug: after a window drag, `.Icon` region rects shrink back to initial values until the next `SetRegionRects` call.

**How to avoid:**
Use `ExtendsContentIntoTitleBar = true` paired exclusively with `InputNonClientPointerSource`. Do NOT also call `SetTitleBar()`. Define passthrough rects for every interactive element in the title bar area. Recalculate rects on `SizeChanged` to work around the post-drag shrink bug. Test with flyouts, context menus, and right-click specifically.

**Warning signs:**
- Buttons in the title bar area work on first launch but stop responding after dragging the window
- Flyouts opened from title bar elements cannot be clicked
- Right-clicking in the title bar region triggers `WM_NCRBUTTONDOWN` message artifacts

**Phase to address:**
Phase 1 (Custom Title Bar) -- must be validated before adding sidebar content into the title bar area.

---

### Pitfall 2: WebView2 keyboard focus black hole steals app-level shortcuts

**What goes wrong:**
Once a WebView2 control (terminal pane) has focus, WinUI 3 `KeyboardAccelerator` bindings fire twice or not at all. Alt+F4, Alt+Space, and other system shortcuts stop propagating to the window. Tab/Shift+Tab cannot move focus out of the WebView2 back to native WinUI controls (like the sidebar). When switching from a browser WebView2 pane back to a terminal WebView2 pane, focus does not restore to the input box inside the web content.

**Why it happens:**
WebView2 runs its own Chromium message loop that intercepts keyboard events before WinUI's XAML input pipeline sees them. The `KeyboardAccelerator` double-fire bug (microsoft/microsoft-ui-xaml#6231) is a known WinUI 3 defect. Focus restoration between multiple WebView2 controls is unreliable because each WebView2 maintains independent focus state.

**How to avoid:**
Route all keyboard shortcuts through the WebView2's JavaScript layer (as wcmux already does via `terminal-host.js` command messages), not through WinUI `KeyboardAccelerator`. For the sidebar and title bar, use explicit `GotFocus`/`LostFocus` handlers to track which WebView2 should receive focus. When switching panes, call both `webView.Focus(FocusState.Programmatic)` and `ExecuteScriptAsync("focus()")` -- the existing `FocusAsync()` pattern in `WebViewTerminalController` is correct, keep using it. For browser panes, add an equivalent focus protocol.

**Warning signs:**
- Ctrl+T (new tab) fires twice when a terminal has focus
- Users cannot tab-navigate to the sidebar
- Clicking a sidebar tab item does not reliably move focus to the target pane

**Phase to address:**
Phase 2 (Vertical Tab Sidebar) and Phase 4 (Browser Panes) -- focus management becomes critical when there are multiple focus targets beyond just terminal panes.

---

### Pitfall 3: Process tree walking picks wrong "foreground" process

**What goes wrong:**
The pane title bar shows "conhost" or "pwsh" when the user is actually running `npm run dev` or `claude`. Or it shows a background watcher process instead of the interactive foreground command. The detection lags seconds behind actual process changes, making the title feel stale.

**Why it happens:**
Windows has no direct equivalent of Unix `tcgetpgrp()` for ConPTY sessions. `GetConsoleProcessList` is documented as not recommended and does not have a virtual terminal equivalent. The common workaround (used by WezTerm) is to snapshot the process tree via `CreateToolhelp32Snapshot` + `Process32First`/`Process32Next`, find all descendants of the shell PID, and assume the most recently spawned descendant is the foreground process. This heuristic breaks when: (a) background processes spawn after the foreground one (e.g., file watchers), (b) process trees are deep (node spawns npm spawns node spawns the actual server), or (c) PID reuse causes stale parent-child relationships.

**How to avoid:**
Walk the full descendant tree from the shell PID using `CreateToolhelp32Snapshot`, but sort candidates by creation time (from `PROCESSENTRY32`) and prefer the deepest leaf in the tree rather than just the newest PID. Poll on a 1-2 second timer, not on every output chunk. Cache the last-known process name and only update the UI when it actually changes. Accept that this is a best-effort heuristic and display "pwsh" (the shell name) as a sensible fallback rather than showing stale or wrong process names. Consider also checking if the candidate process has any console handles open.

**Warning signs:**
- Title flickers between process names rapidly
- Title shows "conhost.exe" or "OpenConsole.exe" instead of the actual command
- High CPU usage from frequent process enumeration
- Title shows a background watcher instead of the interactive command

**Phase to address:**
Phase 3 (Pane Title Bars with Process Detection) -- this is the core feature of that phase.

---

### Pitfall 4: Multiple WebView2 environments cause process explosion and memory bloat

**What goes wrong:**
Each terminal pane spawns its own browser process group (browser process + renderer + GPU process), consuming 80-150MB per pane. With 8 terminal panes plus a browser pane, the app uses 1GB+ of memory from WebView2 alone. Disposing WebView2 controls leaks `msedgewebview2.exe` processes that accumulate over the session.

**Why it happens:**
If `EnsureCoreWebView2Async()` is called without passing a shared `CoreWebView2Environment`, each WebView2 control creates its own environment with a separate browser process. The existing `WebViewTerminalController.InitializeAsync()` calls `_webView.EnsureCoreWebView2Async()` without arguments, which means each pane currently gets an independent environment. WebView2 process cleanup on dispose is also known to be unreliable in WinUI 3 (microsoft/microsoft-ui-xaml#9088).

**How to avoid:**
Create a single `CoreWebView2Environment` at app startup (in `MainWindow` or a dedicated `WebView2EnvironmentProvider`) and pass it to every `EnsureCoreWebView2Async(environment)` call. This shares the browser process across all terminal panes and browser panes, reducing per-pane overhead to roughly one renderer process (~30MB). For disposal, explicitly call `CoreWebView2.Stop()` then close the WebView2 before removing it from the visual tree, and add a delayed GC nudge if process leaks are observed.

**Warning signs:**
- Task Manager shows many `msedgewebview2.exe` processes (more than pane count + 3)
- Memory usage grows linearly with pane count beyond ~30MB per pane
- Closing panes does not reduce process count in Task Manager

**Phase to address:**
Phase 1 (before adding more WebView2 instances) -- retrofit the shared environment pattern into existing code as prerequisite work.

---

### Pitfall 5: Vertical sidebar layout breaks existing absolute-positioned pane rendering

**What goes wrong:**
Adding a sidebar column to `MainWindow.xaml` shifts the coordinate space for `WorkspaceView`, but the absolute positioning logic (using `Grid.Margin` for canvas-style layout) still calculates pane rectangles relative to the wrong container. Panes render offset by the sidebar width, or the sidebar overlaps pane content. Resizing the sidebar causes all panes to need re-layout, but the `SizeChanged` event may not fire on the `RootContainer` when only the column proportions change.

**Why it happens:**
The current `WorkspaceView` uses absolute margin-based positioning (`container.Margin = new Thickness(rect.X, rect.Y, 0, 0)`) within a `Grid`. This works because the `Grid` is the full window width. Adding a sidebar column means `WorkspaceView` no longer spans the full window, and the `UpdateContainerSize` call must reflect the reduced available width. If the sidebar width changes (e.g., user resize or collapse), `SizeChanged` on the workspace grid may not re-fire unless the grid itself resizes.

**How to avoid:**
Place the sidebar and workspace area in a parent `Grid` with explicit `ColumnDefinition`s. The workspace `Grid`'s `SizeChanged` will fire correctly as the column adjusts. Verify that `UpdateContainerSize` receives `RootContainer.ActualWidth` (not window width) after the sidebar is present. Add explicit `SizeChanged` re-subscription if the sidebar is collapsible. Test with sidebar open, closed, and mid-resize.

**Warning signs:**
- Panes shift right by the sidebar width on first render
- Closing the sidebar does not reclaim space for panes
- Pane resize handles are offset from visual borders

**Phase to address:**
Phase 2 (Vertical Tab Sidebar) -- the very first integration task before styling.

---

### Pitfall 6: Live output preview in sidebar causes performance degradation

**What goes wrong:**
The sidebar shows a preview of terminal output for each tab, but updating this preview on every output chunk causes high CPU usage and UI jank, especially when an AI agent is streaming long responses. The preview text contains raw ANSI escape sequences instead of readable text, or the preview strips too aggressively and shows gibberish.

**Why it happens:**
Terminal output arrives as raw VT sequences at high frequency (the output pump reads 4KB chunks continuously). Naive approaches either: (a) forward every chunk to the sidebar, overwhelming the UI thread with TextBlock updates, or (b) try to parse VT sequences in C# to extract "clean" text, which is fragile and slow. The xterm.js `Terminal.buffer` API can extract screen content, but calling `ExecuteScriptAsync` frequently from C# to read buffer lines adds cross-process overhead.

**How to avoid:**
Sample output for preview on a timer (every 500ms-1s), not on every output event. Extract the last 2-3 lines of visible content from xterm.js using `terminal.buffer.active.getLine()` in JavaScript, then push the result to C# via `postMessage` -- do NOT poll from C# with `ExecuteScriptAsync`. Strip ANSI sequences in JavaScript (where the terminal parser already understands them) before sending to the sidebar. Truncate preview text to a fixed character count. For collapsed/background tabs, skip preview updates entirely.

**Warning signs:**
- CPU usage spikes when an agent is producing output in a background tab
- Sidebar preview shows escape sequences like `[0m` or `[32m`
- UI thread blocks visible as laggy sidebar scrolling

**Phase to address:**
Phase 2 (Vertical Tab Sidebar) -- design the preview data flow before building the sidebar UI.

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Independent WebView2 environments per pane | Simpler initialization code | 80-150MB per pane, process explosion | Never -- retrofit shared environment early |
| Parse VT sequences in C# for preview text | No JS-side changes needed | Fragile parser, misses edge cases, slower | Never -- use xterm.js buffer API instead |
| Hard-code sidebar width | Faster layout implementation | Users with different screen sizes cannot adjust | Only for initial prototype, add resize within same phase |
| Poll process tree on every output chunk | Responsive title updates | High CPU, flickering titles | Never -- use a timer-based approach |
| Skip `InputNonClientPointerSource` and use `SetTitleBar` | Simpler code, one API call | Interactive title bar controls stop working after window drag | Never for this app's design |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Custom title bar + sidebar | Using `SetTitleBar()` then adding interactive elements | Use `InputNonClientPointerSource` exclusively with `Passthrough` rects for interactive areas |
| WebView2 browser pane + terminal panes | Creating separate `CoreWebView2Environment` per control | Share one environment across all WebView2 instances via `EnsureCoreWebView2Async(sharedEnv)` |
| Process tree detection + ConPTY | Using `GetConsoleProcessList` which is deprecated for PTY use | Use `CreateToolhelp32Snapshot` to walk descendants from shell PID |
| Sidebar preview + output pump | Subscribing directly to `SessionOutputEvent` for preview updates | Sample on a timer and extract clean text from xterm.js buffer via `postMessage` |
| Title bar + NavigationView | Using `NavigationView` with `ExtendsContentIntoTitleBar` | NavigationView does not account for extended title bar padding (microsoft/microsoft-ui-xaml#6108); build custom sidebar panel instead |
| Browser pane focus + terminal pane focus | Relying on WinUI focus system to manage WebView2 focus | Explicitly manage focus state per-pane; use JavaScript `focus()` calls, not just WinUI `Focus()` |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Preview text updated per output chunk | UI thread saturation, dropped frames in sidebar | Timer-based sampling (500ms-1s), skip updates for collapsed tabs | Immediately with active agent output |
| Process tree snapshot per output event | High CPU from `CreateToolhelp32Snapshot` calls | Poll on 1-2 second timer, cache results | With 4+ panes all producing output |
| WebView2 process leak on pane close | `msedgewebview2.exe` count grows over session | Explicit cleanup sequence: stop navigation, remove from tree, dispose | After opening/closing ~10 panes |
| Sidebar re-renders on every tab state change | Jank when switching tabs rapidly | Batch state updates, use `DispatcherQueue` to coalesce | With 8+ tabs |
| Full process tree walk for deep trees | Seconds of latency for process name | Limit tree depth to 5-6 levels, timeout after 50ms | When npm/node spawns deep child trees |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Browser pane WebView2 shares settings with terminal WebView2 | Terminal WebView2 has dev tools disabled, but browser pane needs different security profile | Create browser panes with separate `CoreWebView2` settings (enable navigation, disable local file access) while sharing the same `CoreWebView2Environment` |
| Process tree walker follows symlinks or reused PIDs | Shows wrong process name, potentially leaking info about other users' processes | Verify parent PID chain is unbroken back to the known shell PID before trusting a descendant |
| Browser pane can navigate to `file://` URLs | Exposes local filesystem through the browser pane | Set `CoreWebView2Settings.IsScriptEnabled = true` but filter navigation events to block `file://` and other dangerous schemes |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Sidebar steals too much horizontal space | Terminal panes feel cramped, especially on 1080p displays | Default sidebar width of 200-220px max, collapsible with keyboard shortcut, remember collapsed state |
| Process name in title bar updates with visible flicker | Title feels unreliable and distracting | Only update when process name actually changes, use a fade transition |
| Browser pane looks identical to terminal pane | Users lose track of which pane is which | Add a visible icon or colored accent strip to browser pane borders |
| Custom title bar removes familiar window controls position | Users cannot find min/max/close buttons | Keep caption buttons in standard position (top-right), only customize the drag area and left content |
| Output preview shows meaningless content for TUI apps | vim/htop preview is gibberish | Detect alternate screen buffer mode and show "[vim]" or similar label instead of raw content |

## "Looks Done But Isn't" Checklist

- [ ] **Custom title bar:** Often missing drag region recalculation on resize -- verify dragging works after window resize and after opening/closing sidebar
- [ ] **Custom title bar:** Often missing high-DPI scaling for drag rects -- verify on 150% and 200% display scaling
- [ ] **Vertical sidebar:** Often missing keyboard navigation -- verify Tab key can move focus between sidebar and terminal panes
- [ ] **Process detection:** Often missing handling of exited processes -- verify title reverts to shell name when command finishes
- [ ] **Process detection:** Often missing handling of rapid process changes -- verify `cd && ls && git status` doesn't flicker through each command
- [ ] **Browser pane:** Often missing back/forward navigation and URL bar -- verify users can navigate within the browser pane
- [ ] **Browser pane:** Often missing download handling -- verify downloads don't silently fail or crash
- [ ] **Output preview:** Often missing alternate screen buffer detection -- verify preview shows something sensible during `vim` or `less`
- [ ] **Shared WebView2 env:** Often missing error handling for environment creation failure -- verify graceful degradation if Edge WebView2 runtime is outdated

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Drag region breakage from `SetTitleBar` | LOW | Remove `SetTitleBar` call, switch to `InputNonClientPointerSource` with passthrough rects |
| WebView2 process explosion (no shared env) | MEDIUM | Create shared `CoreWebView2Environment`, update all `EnsureCoreWebView2Async` calls, test cleanup |
| Wrong foreground process shown | LOW | Adjust heuristic (prefer deepest leaf over newest PID), add fallback to shell name |
| Sidebar breaks pane absolute positioning | MEDIUM | Verify `UpdateContainerSize` uses `RootContainer.ActualWidth`, add `SizeChanged` handler for sidebar changes |
| Performance degradation from output preview | LOW | Add timer-based sampling, move text extraction to JS side, skip updates for hidden tabs |
| Focus stuck in WebView2 | MEDIUM | Implement explicit focus manager that tracks active pane and sidebar state, route all focus changes through it |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Title bar drag region breakage | Phase 1: Custom Title Bar | Interactive controls in title bar work after window drag, resize, and DPI change |
| WebView2 shared environment | Phase 1: Custom Title Bar (prerequisite) | Task Manager shows 1 browser process + N renderer processes, not N full process groups |
| Sidebar breaks pane layout | Phase 2: Vertical Tab Sidebar | Panes render correctly with sidebar open, closed, and during resize |
| Output preview performance | Phase 2: Vertical Tab Sidebar | CPU stays under 5% with 4 tabs producing output, sidebar updates at ~1Hz |
| Focus management across pane types | Phase 2: Vertical Tab Sidebar | Tab key navigates sidebar to pane, clicking sidebar item focuses correct pane |
| Wrong foreground process detection | Phase 3: Pane Title Bars | Title shows correct command for `npm run dev`, `claude`, and interactive shells |
| Browser pane WebView2 conflicts | Phase 4: Browser Panes | Browser pane navigates freely, terminal panes unaffected, shared environment used |
| Keyboard shortcut double-fire | Phase 2 + Phase 4 | All shortcuts fire exactly once regardless of which pane type has focus |

## Sources

- [ExtendsContentIntoTitleBar & SetTitleBar make TitleBar unusable - Issue #6993](https://github.com/microsoft/microsoft-ui-xaml/issues/6993)
- [WinUI 3 custom title bar captures focus from flyouts - Issue #9463](https://github.com/microsoft/microsoft-ui-xaml/issues/9463)
- [Caption buttons clickable when mouse not over them - Issue #7259](https://github.com/microsoft/microsoft-ui-xaml/issues/7259)
- [InputNonClientPointerSource region rects bug - Issue #8976](https://github.com/microsoft/microsoft-ui-xaml/issues/8976)
- [Title bar customization docs](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar)
- [WebView2 triggers KeyboardAccelerator twice - Issue #6231](https://github.com/microsoft/microsoft-ui-xaml/issues/6231)
- [WebView2 focus issues - Issue #2961](https://github.com/MicrosoftEdge/WebView2Feedback/issues/2961)
- [WebView2 performance best practices](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)
- [WebView2 process leak on dispose - Issue #3378](https://github.com/MicrosoftEdge/WebView2Feedback/issues/3378)
- [WebView2 leaks msedgewebview2.exe processes - Issue #9088](https://github.com/microsoft/microsoft-ui-xaml/issues/9088)
- [CoreWebView2Environment sharing](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/environment-controller-core)
- [WezTerm foreground process detection](https://wezterm.org/config/lua/pane/get_foreground_process_info.html)
- [CreateToolhelp32Snapshot docs](https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/nf-tlhelp32-createtoolhelp32snapshot)
- [NavigationView does not account for ExtendsContentIntoTitleBar - Issue #6108](https://github.com/microsoft/microsoft-ui-xaml/issues/6108)

---
*Pitfalls research for: v1.1 UI/UX overhaul -- custom title bar, vertical tab sidebar, process detection, browser panes*
*Researched: 2026-03-08*
