# Roadmap: wcmux

## Milestones

- ✅ **v1.0 MVP** - Phases 1-3 (shipped 2026-03-08)
- 🚧 **v1.1 UI/UX Overhaul** - Phases 4-7 (in progress)

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

<details>
<summary>✅ v1.0 MVP (Phases 1-3) - SHIPPED 2026-03-08</summary>

### Phase 1: Terminal Runtime And Panes
**Goal:** Deliver real Windows terminal sessions with reliable horizontal and vertical pane splitting.
**Depends on:** Nothing (first phase)
**Requirements**: [SESS-01, SESS-02, LAYT-01, LAYT-02, LAYT-03]
**Success Criteria** (what must be TRUE):
  1. User can launch a supported shell inside `wcmux` and interact with it through ConPTY-backed hosting.
  2. Full-screen terminal apps and prompts behave correctly during input, resize, and exit.
  3. User can split the active pane horizontally or vertically and each pane stays interactive.
  4. User can move focus between panes and resize panes using keyboard-driven controls.
**Plans**: 3 plans

Plans:
- [x] 01-01: Establish shell-agnostic core, session lifecycle, and ConPTY hosting primitives.
- [x] 01-02: Add terminal surface integration, resize handling, and fidelity checks for interactive TUIs.
- [x] 01-03: Implement split-tree layout state, pane creation, focus movement, and pane resizing.

### Phase 2: Tabbed Multiplexer Shell
**Goal:** Organize live sessions into stable tabbed layouts with clear pane identity and shell/core boundaries.
**Depends on:** Phase 1
**Requirements**: [SESS-03, TABS-01, TABS-02, TABS-03]
**Success Criteria** (what must be TRUE):
  1. User can create a new tab with its own independent pane arrangement.
  2. Switching tabs preserves inactive tab state and does not disrupt their sessions.
  3. Closing a tab removes only that tab's layout and leaves other tabs intact.
  4. User can identify each pane through useful titles and current session context.
**Plans**: 2 plans

Plans:
- [x] 02-01: TDD: Core tab state model (TabStore) and path display helper (PathHelper) with full test coverage.
- [x] 02-02: Wire tab lifecycle through app shell: TabViewModel, TabBarView, keyboard shortcuts, pane border titles.

### Phase 3: Attention And Windows Integration
**Goal:** Make background sessions visible through generic attention handling and native Windows notifications.
**Depends on:** Phase 2
**Requirements**: [NOTF-01, NOTF-02, NOTF-03]
**Success Criteria** (what must be TRUE):
  1. User can see unread or attention state on panes and tabs inside the app.
  2. User receives a Windows desktop notification when a non-focused session needs attention.
  3. Attention behavior works for generic terminal sessions instead of only a single AI tool.
  4. Desktop notifications and in-app unread state stay in sync when focus changes.
**Plans**: 2 plans

Plans:
- [x] 03-01: AttentionStore, bell detection, and in-app visual indicators (pane dimming, blinking borders, tab attention).
- [x] 03-02: Windows toast notifications, FlashWindowEx, deep-link activation, and end-to-end verification.

</details>

### 🚧 v1.1 UI/UX Overhaul (In Progress)

**Milestone Goal:** Replace default Windows chrome and horizontal tabs with a polished dark UI, vertical tab sidebar with output previews, pane title bars with inline actions, and refined pane interaction.

- [x] **Phase 4: Custom Chrome and WebView2 Foundation** - Replace default title bar with custom dark chrome and establish shared WebView2 environment. (completed 2026-03-09)
- [ ] **Phase 5: Pane Title Bars and Browser Panes** - Add per-pane title bars with process detection, close/split actions, and browser pane hosting.
- [ ] **Phase 6: Vertical Tab Sidebar** - Replace horizontal tab bar with vertical sidebar showing tab title, cwd, output preview, and attention state.
- [ ] **Phase 7: Pane Interaction** - Enable mouse resize, keyboard swap, and drag-to-rearrange for pane management.

## Phase Details

### Phase 4: Custom Chrome and WebView2 Foundation
**Goal**: Users see a polished dark app shell instead of default Windows chrome, and WebView2 resources are shared efficiently across panes.
**Depends on**: Phase 3
**Requirements**: [CHRM-01, CHRM-02]
**Success Criteria** (what must be TRUE):
  1. User sees a dark custom title bar with window controls (minimize, maximize, close) that matches the app aesthetic instead of default white Windows chrome.
  2. User can drag the window by the custom title bar and double-click to maximize/restore without glitches.
  3. All existing terminal panes share a single WebView2 browser process group instead of spawning independent ones.
**Plans**: 2 plans

Plans:
- [ ] 04-01-PLAN.md -- Custom dark title bar with InputNonClientPointerSource and system caption buttons
- [ ] 04-02-PLAN.md -- Shared WebView2 environment singleton and controller refactor

### Phase 5: Pane Title Bars and Browser Panes
**Goal**: Users can identify each pane by its foreground process, perform pane actions via mouse, and open browser panes alongside terminals.
**Depends on**: Phase 4
**Requirements**: [PBAR-01, PBAR-02, PBAR-03, PBAR-04]
**Success Criteria** (what must be TRUE):
  1. User sees a title bar above each pane displaying the foreground process name (e.g., "python", "claude", "bash") that updates as the running command changes.
  2. User can close a pane by clicking the X button in its title bar.
  3. User can split a pane horizontally or vertically by clicking icon buttons in its title bar.
  4. User can open a browser pane via a button in the pane title bar, and the browser pane renders web content with address bar and navigation controls.
**Plans**: TBD

Plans:
- [ ] 05-01: TBD
- [ ] 05-02: TBD

### Phase 6: Vertical Tab Sidebar
**Goal**: Users manage tabs through a vertical sidebar that shows richer context than the old horizontal tab bar.
**Depends on**: Phase 5
**Requirements**: [SIDE-01, SIDE-02, SIDE-03]
**Success Criteria** (what must be TRUE):
  1. User sees tabs listed vertically in a left sidebar showing each tab's title and current working directory.
  2. User sees the last 2-3 lines of terminal output as preview text on each sidebar tab entry.
  3. User sees attention indicators on sidebar tabs when background panes ring the terminal bell.
  4. The old horizontal tab bar is fully replaced; the sidebar is the only tab navigation surface.
**Plans**: TBD

Plans:
- [ ] 06-01: TBD
- [ ] 06-02: TBD

### Phase 7: Pane Interaction
**Goal**: Users can rearrange and resize panes fluidly using mouse and keyboard without relying solely on keyboard shortcuts.
**Depends on**: Phase 6
**Requirements**: [PINT-01, PINT-02, PINT-03]
**Success Criteria** (what must be TRUE):
  1. User can drag pane borders with the mouse to resize panes, and the resize persists correctly.
  2. User can swap two adjacent panes using Ctrl+Alt+Shift+Arrow keys.
  3. User can drag a pane title bar onto another pane and see a blue directional preview overlay indicating where the pane will land, then drop to rearrange.
**Plans**: TBD

Plans:
- [ ] 07-01: TBD
- [ ] 07-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 4 -> 5 -> 6 -> 7

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Terminal Runtime And Panes | v1.0 | 3/3 | Complete | 2026-03-07 |
| 2. Tabbed Multiplexer Shell | v1.0 | 2/2 | Complete | 2026-03-07 |
| 3. Attention And Windows Integration | v1.0 | 2/2 | Complete | 2026-03-08 |
| 4. Custom Chrome and WebView2 Foundation | 2/2 | Complete   | 2026-03-09 | - |
| 5. Pane Title Bars and Browser Panes | v1.1 | 0/? | Not started | - |
| 6. Vertical Tab Sidebar | v1.1 | 0/? | Not started | - |
| 7. Pane Interaction | v1.1 | 0/? | Not started | - |
