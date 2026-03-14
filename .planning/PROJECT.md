# wcmux

## What This Is

`wcmux` is a Windows-first terminal multiplexer for local AI coding workflows. It hosts real Windows terminal sessions (ConPTY-backed) in a polished WinUI 3 shell with a vertical tab sidebar, per-pane title bars showing live process names, pane splitting, tab management, browser pane hosting, and Windows attention notifications — all without wrapping the terminal in a web interface or forcing an opinionated agent workflow.

## Core Value

Users can manage multiple real Windows terminal sessions in one place without losing terminal fidelity or being pushed into an opinionated workflow.

## Last Shipped: v1.1 UI/UX Overhaul (2026-03-14)

Replaced default Windows chrome and horizontal tabs with a polished dark UI: custom title bar, vertical tab sidebar with cwd and attention indicators, per-pane title bars with inline actions, browser pane hosting, and fluid mouse/keyboard pane interaction.

## Requirements

### Validated

- ✓ Users can open real Windows terminal sessions with ConPTY-backed hosting — v1.0
- ✓ Users can interact with full-screen terminal apps (vim, fzf) without losing fidelity — v1.0
- ✓ Users can split terminal layouts horizontally and vertically with keyboard focus and resize — v1.0
- ✓ Users can keep multiple tabbed terminal layouts open at once — v1.0
- ✓ Users receive Windows notifications when a background session needs attention (bell-based) — v1.0
- ✓ Users see a custom dark title bar replacing default Windows chrome — v1.1
- ✓ App uses a shared WebView2 environment across all panes (memory optimization) — v1.1
- ✓ Users see per-pane title bars showing live foreground process name — v1.1
- ✓ Users can close/split panes and open browser panes via pane title bar buttons — v1.1
- ✓ Users see tabs in a vertical sidebar with tab title and current working directory — v1.1
- ✓ Users see attention indicators on sidebar tabs when background panes ring the bell — v1.1
- ✓ Users can drag pane borders to resize, swap panes by keyboard, and drag-to-rearrange — v1.1

### Active (v2.0 candidates)

- [ ] Users can save and restore named tab and pane layouts across app launches (WORK-01)
- [ ] Users can reopen a previous working set with session metadata intact (WORK-02)
- [ ] Users can control sessions and layouts through a local CLI or automation API (AUTO-01)
- [ ] Users can trigger app actions from external scripts without tying the product to one AI vendor (AUTO-02)

### Deferred (not dropped)

- [ ] SIDE-02: Preview text (last 2-3 lines of terminal output) in sidebar tab entries — infrastructure built in v1.1, display disabled per user preference. One-line re-enable in `TabSidebarView.RenderTabs()` when desired.

### Out of Scope

- Electron-based implementation — explicitly rejected in favor of WinUI 3 native
- Cloud sync or collaboration — adds auth and privacy complexity unrelated to core
- Tab drag-and-drop reordering — high effort for WinUI 3 custom UI; defer to keyboard reorder
- Terminal scrollback search — xterm.js search addon wiring is non-trivial; defer
- Git/PR status in sidebar — requires git CLI + GitHub API integration; scope expansion
- Live terminal thumbnail previews — text-based preview (SIDE-02) is the right trade-off

## Context

**Current state:**
- v1.1 shipped 2026-03-14 with 8 phases, 16 plans, ~13,700 lines C# (src + tests), 69 files
- Stack: WinUI 3 / Windows App SDK, ConPTY (kernel32 P/Invoke), WebView2, xterm.js 5.5.0, .NET 9, xUnit
- Architecture: store pattern (LayoutStore, TabStore, AttentionStore), event-driven session bus, pure reducer layout transitions
- Test suite: ~190 automated tests across runtime, layout, terminal bridge, attention, and interaction subsystems
- Tech debt: SIDE-02 display deferred, Nyquist VALIDATION.md for phases 4-8 still draft status

**The user is building this for personal use** as a Windows developer running AI coding tools in terminal sessions. The primary use case is running Claude, Codex, and similar CLIs in managed pane/tab layouts with native Windows integration.

## Constraints

- **Platform:** Windows-first desktop app — exists to fill the gap for Windows users
- **Implementation:** No Electron — WinUI 3 / Windows App SDK is the chosen stack
- **Architecture:** Native Windows (WinUI 3) confirmed viable; Tauri fallback not needed
- **UX:** Unopinionated terminal-first design — avoid prescriptive agent shell patterns
- **Scope:** Terminal-first — browser, workspace, and deeper API parity can come later

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| WinUI 3 + Windows App SDK over Tauri | Native is viable for ConPTY + WebView2; preserves full terminal fidelity | ✓ Good — ConPTY/WebView2 combination works well |
| ConPTY (kernel32 P/Invoke) for terminal hosting | Real PTY semantics vs VT emulation | ✓ Good — full TUI fidelity (vim, fzf, etc.) confirmed |
| Store pattern (LayoutStore, TabStore, AttentionStore) + pure reducer | Observable state without heavy framework; pure reducers enable easy testing | ✓ Good — 190+ tests, all layout logic is pure functions |
| xterm.js 5.5.0 over custom renderer | Best-in-class VT rendering; well-maintained | ✓ Good — no rendering issues |
| InputNonClientPointerSource (not SetTitleBar) for custom chrome | SetTitleBar has post-drag interactive control bugs; InputNonClientPointerSource is more explicit | ✓ Good — no drag/interactive issues |
| Single shared CoreWebView2Environment singleton | Prevents one-process-per-pane memory bloat | ✓ Good — single msedgewebview2 process group confirmed |
| ToolHelp32 P/Invoke for foreground process detection | ~1ms vs 50-200ms for WMI; low polling overhead | ✓ Good — 2s timer imperceptible, names accurate |
| Browser pane as sentinel PaneKind (no ConPTY session) | Clean model separation; no session teardown needed for browser close | ✓ Good — no edge cases |
| Vertical sidebar replacing horizontal tab bar | Richer context (cwd, attention) in vertical space; aligns with app aesthetic | ✓ Good — visual improvement confirmed |
| SIDE-02 preview display disabled per user preference | User did not want cluttered sidebar; infrastructure preserved for future re-enable | ⚠ Revisit — ring buffer infrastructure is idle; easy to re-enable |
| CursorBorder as Grid subclass (not Border) | Border is sealed in WinUI 3; Grid subclass enables ProtectedCursor override | ✓ Good — resize cursors work correctly |
| Swap accelerators registered before resize in PaneCommandBindings | 3-modifier Ctrl+Alt+Shift must match before 2-modifier Ctrl+Alt | ✓ Good — no accelerator conflicts |
| 24px pane title bar height | Compact terminal aesthetic; consistent with sidebar item height | ✓ Good — visually balanced |

---
*Last updated: 2026-03-14 after v1.1 milestone*
