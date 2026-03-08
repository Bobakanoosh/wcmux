# Phase 2: Tabbed Multiplexer Shell - Context

**Gathered:** 2026-03-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Add tabbed layouts with independent pane trees, pane metadata (titles and session context), and clean shell/core boundaries. Each tab owns its own split tree and session set. Notifications, workspace persistence, and automation are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Tab bar design
- Tab bar sits at the top of the window, below the title bar, above the workspace area.
- Each tab shows a close (X) button directly on the tab — always visible, not hover-only.
- A [+] button at the end of the tab row creates a new tab. Keyboard shortcut also available.
- Tab labels default to the working directory of the tab's first pane at creation time.
- Tab labels are static — they do not update as panes change cwd or focus.
- Users can double-click a tab label to rename it inline. Once renamed, the custom name sticks.

### Tab lifecycle
- New tabs open with a single pane in the user's home directory (not inherited from the source tab).
- All sessions in inactive tabs keep running in the background — fully alive, not detached.
- Closing the last pane in a tab closes the tab itself.
- Closing the last tab exits the app.

### Pane titles & metadata
- Pane identity is displayed in the pane border area — title text embedded in the border, no extra header bar.
- Pane titles show the current working directory of the shell, updating as the user changes directories.
- Long paths are truncated from the left (e.g., `.../deep/path`).
- Pane cwd does NOT propagate up to the tab label — tab and pane titles are independent.

### Claude's Discretion
- Shell/core boundary decisions — what moves to Wcmux.Core vs stays in Wcmux.App for tab state management.
- Exact keyboard shortcut scheme for tab operations (new, close, switch).
- Exact visual styling of the tab bar and pane border titles.
- How cwd tracking is implemented (VT OSC sequences, polling, or session event-based).
- Tab switching animation or transition behavior (if any).

</decisions>

<specifics>
## Specific Ideas

- Tab bar should feel like a real terminal multiplexer tab bar — lightweight, not browser-heavy.
- Pane border titles should be subtle and space-efficient, embedded in the existing border rather than adding a full header row.
- Static tab labels keep the UI predictable — the tab name is set once and doesn't jump around as you navigate.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `LayoutStore` (Wcmux.Core): Already manages a single split tree with focus, rects, and events. Phase 2 needs one LayoutStore per tab.
- `WorkspaceViewModel` (Wcmux.App): Orchestrates pane commands and session creation. Needs to become tab-aware or be wrapped by a tab-level manager.
- `WorkspaceView` (Wcmux.App): Renders pane tree from PaneRects. Can be reused per-tab with attach/detach lifecycle.
- `SessionManager` (Wcmux.Core): Shared across tabs — sessions are global, pane-to-session mapping is per-tab.
- `TerminalPaneView`: Already supports attach/detach for WebView2 lifecycle — useful for tab switching.

### Established Patterns
- Immutable record types with pure reducer transitions for state (LayoutNode, LayoutReducer).
- Observable events (LayoutChanged, ActivePaneChanged) for view updates.
- Code-behind rendering from computed state (WorkspaceView reads PaneRects, positions controls).
- Mouse and keyboard both first-class for all interactions.

### Integration Points
- `MainWindow.xaml` currently hosts a single `WorkspaceView` — needs a tab bar above it and tab-switching logic.
- `WorkspaceViewModel` constructor takes an initial session — tab creation would create a new ViewModel per tab.
- `ISession.LastKnownCwd` provides cwd data for pane titles.

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---
*Phase: 02-tabbed-multiplexer-shell*
*Context gathered: 2026-03-07*
