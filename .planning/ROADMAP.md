# Roadmap: wcmux

## Overview

`wcmux` reaches a credible v1 by proving terminal fidelity first, then layering the multiplexer UX on top, then adding the attention and Windows integration that make multi-session AI workflows practical. The roadmap stays intentionally terminal-first and avoids chasing browser or automation parity until the real-session core is trustworthy.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Terminal Runtime And Panes** - Build the ConPTY-backed session core and first-class pane splitting behavior.
- [ ] **Phase 2: Tabbed Multiplexer Shell** - Add tabbed layouts, pane metadata, and stable session organization.
- [ ] **Phase 3: Attention And Windows Integration** - Deliver generic attention state and Windows notifications without hard-coding tool-specific behavior.

## Phase Details

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
- [ ] 01-01: Establish shell-agnostic core, session lifecycle, and ConPTY hosting primitives.
- [ ] 01-02: Add terminal surface integration, resize handling, and fidelity checks for interactive TUIs.
- [ ] 01-03: Implement split-tree layout state, pane creation, focus movement, and pane resizing.

### Phase 2: Tabbed Multiplexer Shell
**Goal:** Organize live sessions into stable tabbed layouts with clear pane identity and shell/core boundaries.
**Depends on:** Phase 1
**Requirements**: [SESS-03, TABS-01, TABS-02, TABS-03]
**Success Criteria** (what must be TRUE):
  1. User can create a new tab with its own independent pane arrangement.
  2. Switching tabs preserves inactive tab state and does not disrupt their sessions.
  3. Closing a tab removes only that tab's layout and leaves other tabs intact.
  4. User can identify each pane through useful titles and current session context.
**Plans**: 3 plans

Plans:
- [ ] 02-01: Build the shell layer for tab creation, switching, and close semantics over the core state model.
- [ ] 02-02: Add pane and tab metadata flow, including titles and session-context labeling.
- [ ] 02-03: Harden shell/core boundaries so the UI shell remains replaceable while tab state stays deterministic.

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
- [ ] 03-01: Implement generic attention event detection, unread state, and focus-aware suppression rules.
- [ ] 03-02: Integrate Windows notifications and validate native-shell versus fallback-shell behavior.

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Terminal Runtime And Panes | 0/3 | Not started | - |
| 2. Tabbed Multiplexer Shell | 0/3 | Not started | - |
| 3. Attention And Windows Integration | 0/2 | Not started | - |
