# Project Research Summary

**Project:** wcmux
**Domain:** Windows-first desktop terminal multiplexer
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Executive Summary

`wcmux` fits a fairly clear product category: a desktop terminal multiplexer with pane, tab, and attention-management primitives. The research supports a terminal-first roadmap rather than trying to chase full `cmux` parity immediately. On Windows, the hard technical requirement is not the pane UI but correct pseudoconsole hosting through ConPTY, because everything else depends on real terminal behavior matching tools like Claude, Codex, `vim`, and `fzf`.

The strongest recommendation is to separate the product into a shell-agnostic Rust core plus a Windows UI shell. Native Windows remains the preferred direction, with WinUI 3 / Windows App SDK as the best fit for a genuinely native shell, but Tauri remains a credible fallback if native UI delivery risk becomes too high for a first release. The biggest risks are fake terminal integration, ConPTY deadlocks, and over-scoping parity before the terminal core is trustworthy.

## Key Findings

### Recommended Stack

Use a Rust core around ConPTY, async session management, structured tracing, and versioned state models. Pair that core with a native Windows shell if feasible, using WinUI 3 and Windows App SDK for tabs, notifications, and packaging; if that path proves too expensive for a first-time native Windows app, keep the Rust core and swap the shell to Tauri 2.x rather than relaxing into Electron.

**Core technologies:**
- **WinUI 3 / Windows App SDK**: native shell, tabs, notification integration - best match for the "native first" goal
- **Rust + `windows` crate + `tokio`**: PTY/session engine, eventing, and platform interop - strongest fit for ConPTY-heavy systems work
- **Windows ConPTY**: real terminal hosting - non-negotiable if terminal fidelity is the core promise
- **Tauri 2.x**: fallback shell only - acceptable if native delivery risk is too high early

### Expected Features

Table stakes are straightforward: real PTY-backed sessions, pane splitting, multiple tabs, clear session metadata, keyboard-first navigation, and attention notifications. The research strongly supports your current terminal-first v1: these are enough to validate the product without dragging in browser surfaces, cloud sync, or deep automation too early.

**Must have (table stakes):**
- Real terminal sessions backed by ConPTY
- Horizontal and vertical pane splitting
- Multiple tabs containing pane arrangements
- Session labels / metadata and keyboard control
- In-app unread state and Windows attention notifications

**Should have (competitive):**
- Native Windows shell polish
- Generic tool-agnostic attention handling
- Architecture that preserves native-shell and Tauri-shell optionality

**Defer (v2+):**
- Browser surfaces
- Public automation / scripting API
- Remote or shared sessions

### Architecture Approach

The recommended structure is a state-first layout and session model: a shell layer drives commands into a Rust core, the core owns tabs, pane trees, session state, and notifications, and a ConPTY host layer isolates each terminal session behind its own lifecycle and IO handling. This keeps pane movement, tab switching, and notification state deterministic while avoiding early lock-in to either WinUI or Tauri.

**Major components:**
1. **Shell layer** - windows, tab strip, shortcuts, notification presentation
2. **Layout core** - split tree, focus rules, tab/workspace membership, persistence schema
3. **Session manager** - spawn/kill/restart, cwd tracking, metadata, unread state
4. **PTY host** - ConPTY lifecycle, input/output, resize, shutdown
5. **Notification service** - generic attention events mapped to Windows toasts and in-app state

### Critical Pitfalls

1. **Fake terminal integration** - solve this first with ConPTY-backed fidelity checks against real terminal behavior
2. **PTY deadlocks and shutdown bugs** - isolate IO paths per session and add tracing from the beginning
3. **Over-scoping parity too early** - keep v1 centered on sessions, panes, tabs, and notifications
4. **Agent-specific notification design** - model attention generically so the app works with any terminal process
5. **Shell lock-in too early** - keep session/layout logic outside the UI framework

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Terminal Core
**Rationale:** Everything depends on credible terminal hosting.
**Delivers:** ConPTY session management, shell launching, terminal fidelity checks, and the initial pane/layout state model.
**Addresses:** Real sessions, split-pane foundations, shell-agnostic core setup.
**Avoids:** Fake terminal integration and PTY deadlocks.

### Phase 2: Multiplexing UX
**Rationale:** Once sessions are real, the product can become useful day to day.
**Delivers:** Horizontal/vertical splits, focus movement, tab management, pane metadata, and keyboard shortcuts.
**Uses:** The stable session and layout core from Phase 1.
**Implements:** The user-facing multiplexer shell.

### Phase 3: Attention and Product Hardening
**Rationale:** Notifications and polish matter after the terminal/multiplexer loop is stable.
**Delivers:** Generic attention detection, Windows notifications, unread state, and architecture hardening around native-vs-Tauri boundaries.
**Uses:** Session metadata, notification service, and shell integration.
**Implements:** The multi-agent workflow value that differentiates the product.

### Phase Ordering Rationale

- ConPTY fidelity has to land before any meaningful parity claims.
- Split panes and tabs should build on a deterministic state model rather than direct widget mutations.
- Notifications should follow stable session identity and unread state instead of being bolted on per tool.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** terminal surface strategy on Windows, especially if native UI requires a specific rendering/control approach
- **Phase 3:** installed-app notification behavior and packaging constraints across native and Tauri paths

Phases with standard patterns:
- **Phase 2:** split trees, focus movement, tab membership, and keyboard shortcuts are well-understood multiplexer patterns

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | Official Windows/Tauri docs are clear, but the exact native terminal rendering path still needs implementation-level validation |
| Features | MEDIUM | Product expectations are consistent across `cmux`, WezTerm, and Windows Terminal, but v1 cuts remain judgment-based |
| Architecture | MEDIUM | The component split is strong, but exact shell/core boundaries will matter in implementation |
| Pitfalls | HIGH | The major Windows PTY and scope risks are clear and repeated across the ecosystem |

**Overall confidence:** MEDIUM

### Gaps to Address

- **Terminal rendering/control strategy:** validate the specific UI approach during Phase 1 planning before locking the shell stack
- **Notification packaging behavior:** verify installed-app constraints early so Windows toasts do not surprise the project late

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session - ConPTY architecture and deadlock constraints
- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/ - current native Windows desktop app stack
- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/ - Windows desktop notification model
- https://v2.tauri.app/start/prerequisites/ - Tauri Windows prerequisites and runtime assumptions
- https://www.cmux.dev/ - upstream product shape and principles
- https://www.cmux.dev/docs/concepts - upstream tabs / panes / sessions concepts
- https://www.cmux.dev/docs/notifications - upstream notification semantics

### Secondary (MEDIUM confidence)
- https://wezterm.org/features.html - expected multiplexer feature baseline
- https://wezterm.org/config/lua/wezterm.mux/index.html - workspace and mux reference patterns
- https://learn.microsoft.com/en-us/windows/terminal/panes - Windows pane UX baseline

### Tertiary (LOW confidence)
- None

---
*Research completed: 2026-03-06*
*Ready for roadmap: yes*
