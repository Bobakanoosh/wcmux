# Requirements: wcmux

**Defined:** 2026-03-06
**Core Value:** Users can manage multiple real Windows terminal sessions in one place without losing terminal fidelity or being pushed into an opinionated workflow.

## v1.0 Requirements (Complete)

### Sessions

- [x] **SESS-01**: User can open a real Windows terminal session inside the app using ConPTY-backed hosting for supported shells.
- [x] **SESS-02**: User can interact with full-screen terminal apps and prompts without losing expected input, output, resize, or exit behavior.
- [x] **SESS-03**: User can see identifying metadata for each pane, including a useful title and current session context.

### Layout

- [x] **LAYT-01**: User can split the active pane horizontally.
- [x] **LAYT-02**: User can split the active pane vertically.
- [x] **LAYT-03**: User can move focus between panes and resize panes with keyboard-driven controls.

### Tabs

- [x] **TABS-01**: User can create a new tab with its own independent pane layout.
- [x] **TABS-02**: User can switch between tabs without disrupting inactive tab layouts.
- [x] **TABS-03**: User can close a tab without affecting sessions in other tabs.

### Notifications

- [x] **NOTF-01**: User can see when a pane or tab needs attention through in-app unread or attention indicators.
- [x] **NOTF-02**: User receives a Windows desktop notification when a non-focused session needs attention.
- [x] **NOTF-03**: User can receive attention notifications from generic terminal sessions rather than from a single hard-coded AI tool.

## v1.1 Requirements

### Window Chrome

- [x] **CHRM-01**: User sees a custom dark title bar replacing the default Windows chrome.
- [x] **CHRM-02**: App uses a shared WebView2 environment across all panes to reduce memory overhead.

### Pane Title Bars

- [x] **PBAR-01**: User sees a tab-like title bar above each pane showing the foreground process name.
- [x] **PBAR-02**: User can close a pane via an X button in its title bar.
- [x] **PBAR-03**: User can split a pane horizontally or vertically via icon buttons in the pane title bar.
- [x] **PBAR-04**: User can open a browser pane via a button in the pane title bar.

### Vertical Tab Sidebar

- [x] **SIDE-01**: User sees tabs in a vertical sidebar on the left showing tab title and cwd.
- [x] **SIDE-02**: User sees the last 2-3 lines of terminal output as preview text per tab.
- [x] **SIDE-03**: User sees attention indicators on sidebar tabs when background panes ring the bell.

### Pane Interaction

- [ ] **PINT-01**: User can drag pane borders with the mouse to resize panes.
- [ ] **PINT-02**: User can swap pane positions using Ctrl+Alt+Shift+Arrow keys.
- [ ] **PINT-03**: User can drag a pane title bar onto another pane to rearrange splits, with a blue preview showing the target split direction.

## v2 Requirements

### Workspaces

- **WORK-01**: User can save and restore named tab and pane layouts across app launches.
- **WORK-02**: User can reopen a previous working set with session metadata intact.

### Automation

- **AUTO-01**: User can control sessions and layouts through a local CLI or automation API.
- **AUTO-02**: User can trigger app actions from external scripts without tying the product to one AI vendor.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Electron shell | Explicitly rejected by project constraints |
| Public automation API in v1.1 | Depends on a stable internal command model |
| Cloud sync or collaboration | Adds auth, sync, and privacy complexity unrelated to core |
| Tab drag-and-drop reordering | High effort for WinUI 3 custom UI; defer to keyboard reorder |
| Terminal scrollback search | xterm.js search addon wiring is non-trivial; defer |
| Git/PR status in sidebar | Requires git CLI + GitHub API integration; scope expansion |
| Live terminal thumbnail previews | Massive complexity vs text-based preview for marginal value |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| SESS-01 | Phase 1 | Complete |
| SESS-02 | Phase 1 | Complete |
| SESS-03 | Phase 2 | Complete |
| LAYT-01 | Phase 1 | Complete |
| LAYT-02 | Phase 1 | Complete |
| LAYT-03 | Phase 1 | Complete |
| TABS-01 | Phase 2 | Complete |
| TABS-02 | Phase 2 | Complete |
| TABS-03 | Phase 2 | Complete |
| NOTF-01 | Phase 3 | Complete |
| NOTF-02 | Phase 3 | Complete |
| NOTF-03 | Phase 3 | Complete |
| CHRM-01 | Phase 4 | Complete |
| CHRM-02 | Phase 4 | Complete |
| PBAR-01 | Phase 5 | Complete |
| PBAR-02 | Phase 5 | Complete |
| PBAR-03 | Phase 5 | Complete |
| PBAR-04 | Phase 5 | Complete |
| SIDE-01 | Phase 6 | Complete |
| SIDE-02 | Phase 6 | Complete |
| SIDE-03 | Phase 6 | Complete |
| PINT-01 | Phase 7 | Pending |
| PINT-02 | Phase 7 | Pending |
| PINT-03 | Phase 7 | Pending |

**Coverage:**
- v1.0 requirements: 12 total (all complete)
- v1.1 requirements: 12 total
- Mapped to phases: 12
- Unmapped: 0

---
*Requirements defined: 2026-03-06*
*Last updated: 2026-03-08 after v1.1 roadmap creation*
