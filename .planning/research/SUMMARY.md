# Project Research Summary

**Project:** wcmux
**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Executive Summary

`wcmux` sits at the intersection of terminal emulation, window management, and desktop notifications. The research points to a clear product shape: treat the app as a terminal-first desktop shell built around real Windows pseudoconsole sessions, split-pane layout state, and generic attention signaling for long-running CLI tools. The main architectural choice is not "which web framework" but "how to preserve terminal fidelity while keeping the shell decision reversible."

The recommended approach is native-first: use Windows App SDK / WinUI 3 for the outer shell and keep the hard parts in a Rust core that owns ConPTY session management, layout state, and notification events. Tauri remains a viable fallback because it preserves the Rust core and avoids Electron, but it should be treated as a shell fallback rather than the defining architecture. The biggest risks are fake terminal behavior, ConPTY deadlocks, and over-scoping parity with upstream `cmux` before the local terminal experience is proven.

## Key Findings

### Recommended Stack

The stack that best fits the project is a native Windows shell over a Rust core. Windows App SDK 1.8.x provides the modern native shell path, ConPTY is the official Windows terminal-hosting primitive, and Rust fits the PTY/session problem well while keeping future shell choices open. Tauri 2.x is the right fallback if native UI delivery cost becomes too high, because it retains the same Rust core and Windows integrations without bringing Electron into scope.

**Core technologies:**
- Windows App SDK + WinUI 3: native Windows shell, tab UI, desktop integration
- Rust: PTY/session engine, layout core, notification plumbing, future automation surface
- ConPTY: real terminal hosting for Windows shells and CLI tools

### Expected Features

Research across `cmux`, Windows Terminal, and WezTerm makes the v1 line fairly clear: the product needs real terminal sessions, pane splitting, multiple tabs, keyboard-first actions, and attention notifications. Workspace persistence, richer tab/sidebar metadata, and automation are valuable but should follow once the terminal core is credible.

**Must have (table stakes):**
- Real terminal sessions with ConPTY-backed fidelity
- Horizontal and vertical pane splitting
- Multiple tabs with clear session metadata
- Keyboard-first navigation and layout actions
- Attention notifications with unread state

**Should have (competitive):**
- Generic agent-attention model rather than tool-specific hooks
- Same-cwd / same-context spawning
- Richer tab/sidebar metadata for many concurrent agents

**Defer (v2+):**
- Embedded browser parity
- Public automation API / local socket control
- Remote multiplexing

### Architecture Approach

The architecture should separate shell, layout state, session orchestration, terminal runtime, and platform services. Sessions must be owned centrally, not by individual pane widgets, and the layout must be represented as a deterministic split tree so panes can be moved, resized, persisted, and restored without UI drift.

**Major components:**
1. Native shell - windows, tabs, commands, settings, desktop notification presentation
2. Layout core - split tree, focus model, tab/workspace state, persistence schema
3. Session manager - process lifecycle, PTY bindings, cwd tracking, attention events
4. Terminal runtime - ConPTY, IO loops, resize handling, screen updates

### Critical Pitfalls

1. **Fake terminal integration** - use ConPTY from the start and verify common TUIs and agent CLIs against Windows Terminal.
2. **PTY deadlocks** - isolate read/write servicing paths and instrument session lifecycle with tracing.
3. **Over-scoped parity** - keep v1 centered on real sessions, panes, tabs, and notifications.
4. **Agent-specific notification logic** - design for generic attention events instead of one branded integration.
5. **Shell lock-in** - keep session/layout logic out of shell-specific view code.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Terminal Core
**Rationale:** Everything depends on credible ConPTY-backed session hosting and a shell-agnostic core.  
**Delivers:** session manager, PTY runtime, base tab shell, deterministic layout model  
**Addresses:** real terminal sessions, multiple tabs  
**Avoids:** fake terminal integration, PTY deadlocks

### Phase 2: Pane Layout + Notifications
**Rationale:** Once sessions are stable, the next validation loop is whether users can actually manage many active agents at once.  
**Delivers:** split-pane UX, focus commands, attention state, desktop notifications  
**Uses:** session event model and Windows notification APIs  
**Implements:** layout engine + notification service

### Phase 3: Session Ergonomics
**Rationale:** After the core product works, improve day-to-day usability before chasing broad parity.  
**Delivers:** command palette polish, richer metadata, same-cwd spawning, scrollback/search  
**Uses:** shell integration and persisted session metadata

### Phase 4: Persistence + Parity Extensions
**Rationale:** Workspace restore and broader `cmux` parity are valuable once the core has been validated locally.  
**Delivers:** workspace persistence, optional automation surface, evaluation of browser parity  
**Uses:** versioned layout serialization and stable event contracts

### Phase Ordering Rationale

- The roadmap starts with ConPTY and session fidelity because every later feature depends on terminal correctness.
- Splits and notifications follow once the app has a reliable model for sessions and attention events.
- Persistence and automation are deferred until the state model is stable enough to expose cleanly.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** Terminal surface choice and ConPTY adapter details still need implementation-level validation.
- **Phase 2:** Windows notification behavior should be validated for packaged vs installed flows if Tauri becomes the shell.
- **Phase 4:** Automation API shape should be researched only if parity pressure makes it relevant.

Phases with standard patterns (skip research-phase):
- **Phase 3:** Command palette, metadata presentation, and layout ergonomics use well-understood desktop patterns once the core exists.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | Native-vs-Tauri is still a product decision, but the platform primitives are clear. |
| Features | HIGH | `cmux`, Windows Terminal, and WezTerm converge strongly on the expected feature floor. |
| Architecture | MEDIUM | The component split is clear, but the specific terminal surface implementation still needs proof. |
| Pitfalls | MEDIUM | The major failure modes are clear from ConPTY and terminal product patterns, but implementation details remain to be tested. |

**Overall confidence:** MEDIUM

### Gaps to Address

- Terminal surface choice: validate whether the project should wrap an existing surface or build a thinner custom one over ConPTY.
- Native shell path: confirm how much WinUI 3 + Rust interop overhead is acceptable for a first Windows-native release.
- Notification semantics: define a generic attention contract that works across multiple terminal-based tools without becoming opinionated.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session - ConPTY terminal-hosting model and deadlock constraints
- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/ - Windows-native shell stack
- https://learn.microsoft.com/en-us/windows/terminal/panes - Pane UX baseline on Windows
- https://learn.microsoft.com/en-us/windows/terminal/command-palette - Keyboard-first action baseline on Windows
- https://www.cmux.dev/ - Upstream product surface and principle alignment
- https://www.cmux.dev/docs/notifications - Upstream notification model
- https://wezterm.org/features.html - Multiplexer feature baseline

### Secondary (MEDIUM confidence)
- https://v2.tauri.app/start/prerequisites/ - Tauri fallback shell requirements
- https://v2.tauri.app/ko/plugin/notification/ - Tauri Windows notification caveats
- https://wezterm.org/shell-integration.html - Shell integration implications for cwd-aware spawning

### Tertiary (LOW confidence)
- Inference from adjacent products that workspace persistence and automation are best delayed until the core session model is stable

---
*Research completed: 2026-03-06*
*Ready for roadmap: yes*
