# Requirements: wcmux

**Defined:** 2026-03-06
**Core Value:** Users can manage multiple real Windows terminal sessions in one place without losing terminal fidelity or being pushed into an opinionated workflow.

## v1 Requirements

### Sessions

- [x] **SESS-01**: User can open a real Windows terminal session inside the app using ConPTY-backed hosting for supported shells.
- [x] **SESS-02**: User can interact with full-screen terminal apps and prompts without losing expected input, output, resize, or exit behavior.
- [ ] **SESS-03**: User can see identifying metadata for each pane, including a useful title and current session context.

### Layout

- [x] **LAYT-01**: User can split the active pane horizontally.
- [x] **LAYT-02**: User can split the active pane vertically.
- [x] **LAYT-03**: User can move focus between panes and resize panes with keyboard-driven controls.

### Tabs

- [ ] **TABS-01**: User can create a new tab with its own independent pane layout.
- [ ] **TABS-02**: User can switch between tabs without disrupting inactive tab layouts.
- [ ] **TABS-03**: User can close a tab without affecting sessions in other tabs.

### Notifications

- [ ] **NOTF-01**: User can see when a pane or tab needs attention through in-app unread or attention indicators.
- [ ] **NOTF-02**: User receives a Windows desktop notification when a non-focused session needs attention.
- [ ] **NOTF-03**: User can receive attention notifications from generic terminal sessions rather than from a single hard-coded AI tool.

## v2 Requirements

### Workspaces

- **WORK-01**: User can save and restore named tab and pane layouts across app launches.
- **WORK-02**: User can reopen a previous working set with session metadata intact.

### Automation

- **AUTO-01**: User can control sessions and layouts through a local CLI or automation API.
- **AUTO-02**: User can trigger app actions from external scripts without tying the product to one AI vendor.

### Surfaces

- **SURF-01**: User can open browser or other non-terminal surfaces inside the workspace when parity work becomes worthwhile.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Electron shell | Explicitly rejected by project constraints |
| Embedded browser parity in v1 | Expands scope before terminal fidelity is proven |
| Public automation API in v1 | Depends on a stable internal command model |
| Cloud sync or collaboration | Adds auth, sync, and privacy complexity unrelated to v1 validation |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| SESS-01 | Phase 1 | Complete |
| SESS-02 | Phase 1 | Complete |
| SESS-03 | Phase 2 | Pending |
| LAYT-01 | Phase 1 | Complete |
| LAYT-02 | Phase 1 | Complete |
| LAYT-03 | Phase 1 | Complete |
| TABS-01 | Phase 2 | Pending |
| TABS-02 | Phase 2 | Pending |
| TABS-03 | Phase 2 | Pending |
| NOTF-01 | Phase 3 | Pending |
| NOTF-02 | Phase 3 | Pending |
| NOTF-03 | Phase 3 | Pending |

**Coverage:**
- v1 requirements: 12 total
- Mapped to phases: 12
- Unmapped: 0

---
*Requirements defined: 2026-03-06*
*Last updated: 2026-03-06 after roadmap creation*
