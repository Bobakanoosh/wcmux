# Project Research Summary

**Project:** wcmux
**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Executive Summary

`wcmux` fits a well-understood product category: a desktop terminal multiplexer whose credibility depends on terminal fidelity first and everything else second. The research points to a native Windows shell over a Rust core as the strongest fit for the project goals, with ConPTY as the non-negotiable session foundation and Tauri held as a fallback shell rather than the starting point.

The strongest recommendation is to treat panes, tabs, session metadata, and generic attention handling as the entire launch story. Browser parity, saved workspaces, and public automation are all reasonable follow-on directions, but they make poor launch bets for a first-time Windows desktop project because they pull focus away from the core proof: multiple real terminal sessions behaving correctly inside one Windows-native application.

The main risks are fake terminal integration, ConPTY deadlocks, and scope inflation from chasing upstream parity too early. Those can be managed by isolating the PTY/session core, keeping the shell replaceable, and ordering the roadmap around terminal runtime before notification polish or future surfaces.

## Key Findings

### Recommended Stack

The best fit is a split architecture: WinUI 3 / Windows App SDK for a native Windows shell, backed by a Rust core that owns ConPTY sessions, layout state, and notifications. That gives the project a genuinely Windows-native UX while keeping the hardest session logic in a shell-agnostic core that can survive a Tauri fallback if native UI complexity proves too high.

**Core technologies:**
- Windows App SDK + WinUI 3: native shell, tabs, window lifecycle, notifications - best fit for native-first.
- Rust + `windows` crate + `tokio`: session engine, ConPTY integration, async IO - best fit for correctness and reuse.
- Windows ConPTY: real terminal session hosting - required for credible terminal fidelity on Windows.
- Tauri 2.x: fallback shell only - acceptable if native UI cost is too high early.

### Expected Features

The market expects panes, tabs, keyboard-first controls, and trustworthy terminal behavior as baseline. For this project, Windows notifications and generic attention state also belong in v1 because the product value is explicitly tied to juggling multiple AI or terminal-driven tasks.

**Must have (table stakes):**
- Real PTY-backed terminal sessions - the product fails without this.
- Horizontal and vertical pane splits - core multiplexer behavior.
- Multiple tabs of pane layouts - baseline organization model.
- Session metadata and keyboard-first navigation - required for usability.
- Attention state with Windows notifications - central to the multi-agent workflow.

**Should have (competitive):**
- Native Windows shell UX - supports the product’s identity.
- Generic AI-session attention model - avoids brand lock-in.
- Saved layouts / restore - strong follow-up once the pane model is stable.

**Defer (v2+):**
- Browser parity - not needed to validate the terminal product.
- Public automation API - useful later, risky too early.
- Remote / shared sessions - large complexity jump.

### Architecture Approach

The architecture should be layered: shell -> layout/workspace state -> session manager -> terminal runtime -> Windows services. The crucial design choice is to keep sessions and layout state out of the UI layer so the shell stays replaceable and pane/tab behavior remains deterministic. That also gives a clean build order: terminal runtime first, then layout and tabs, then notifications, then persistence and future automation.

**Major components:**
1. Native shell - owns windows, tabs, settings, and shell-specific UX.
2. Layout engine - owns split tree, focus, tab membership, and serialization.
3. Session manager - owns process launch, PTY lifecycle, metadata, and attention state.
4. Terminal runtime - owns ConPTY, IO, buffering, and terminal input/output handling.
5. Notification service - maps session attention into in-app unread state and Windows toasts.

### Critical Pitfalls

1. **Fake terminal integration** - avoid any child-process wrapper approach that diverges from real terminal behavior; use ConPTY from the start.
2. **PTY deadlocks** - separate IO paths per session and instrument aggressively with tracing.
3. **Over-scoping upstream parity** - keep launch scope centered on sessions, panes, tabs, and notifications.
4. **Agent-specific notification semantics** - model generic attention events so the app works with Claude, Codex, or anything else.
5. **Shell lock-in too early** - keep the Rust core shell-agnostic until native-vs-Tauri is proven.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Terminal Runtime Foundation
**Rationale:** Everything depends on credible terminal hosting and process lifecycle.
**Delivers:** ConPTY-backed sessions, spawn/exit handling, shell adapter boundaries.
**Addresses:** Real terminal fidelity, session lifecycle.
**Avoids:** Fake terminal integration, PTY deadlocks.

### Phase 2: Pane and Tab Multiplexing
**Rationale:** Once sessions are real, the product can become a multiplexer.
**Delivers:** Split tree, focus/navigation, multiple tabs, pane metadata.
**Uses:** Core layout model and session manager.
**Implements:** Layout engine and shell bindings.

### Phase 3: Attention and Notification UX
**Rationale:** Notifications matter after sessions, panes, and metadata exist.
**Delivers:** Generic attention state, unread badges, Windows notifications, focus-aware suppression.
**Uses:** Notification service and shell integration.
**Implements:** Attention/event pipeline.

### Phase 4: Persistence and Extension Hooks
**Rationale:** Save/restore and future automation should build on a stable state model.
**Delivers:** Settings persistence, saved layouts, groundwork for future scripting.
**Uses:** Versioned persistence schema.
**Implements:** Persistence layer and extension boundaries.

### Phase Ordering Rationale

- Terminal fidelity must come before pane and tab UX because every later feature depends on trustworthy sessions.
- Notifications should follow pane/tab metadata because attention only works when sessions are identifiable and focus-aware.
- Persistence and automation should wait until the state model is stable, otherwise internal churn becomes external breakage.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** Terminal rendering/hosting choice and ConPTY edge cases need implementation-specific research.
- **Phase 3:** Windows notification packaging and installed-app behavior need validation on the chosen shell path.

Phases with standard patterns (skip research-phase):
- **Phase 2:** Split trees, focus movement, and tab models are well-established once the runtime is in place.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | Official sources are clear on ConPTY, Tauri, and Windows App SDK, but the exact terminal rendering path still needs implementation validation. |
| Features | MEDIUM | Product expectations are clear from `cmux`, Windows Terminal, and WezTerm references, but exact v1 cuts remain product-specific. |
| Architecture | MEDIUM | The layered model is strong and conventional, but shell-framework details still need concrete prototyping. |
| Pitfalls | HIGH | The main failure modes are well-documented and highly consistent across sources. |

**Overall confidence:** MEDIUM

### Gaps to Address

- **Terminal surface strategy:** confirm whether the native shell embeds an existing terminal control, a custom renderer, or a Tauri/web surface over the same core.
- **Notification packaging path:** confirm how native vs Tauri affects install-time requirements and toast behavior on Windows.
- **Shell matrix for v1:** confirm whether PowerShell, `cmd`, WSL, and Git Bash are all first-class launch targets or staged over time.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session - ConPTY lifecycle and threading constraints.
- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/ - Native Windows desktop stack direction.
- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/ - Windows desktop notification model.
- https://learn.microsoft.com/en-us/windows/terminal/panes - Baseline pane behavior expectations on Windows.
- https://v2.tauri.app/start/prerequisites/ - Tauri Windows prerequisites and operating constraints.
- https://www.cmux.dev/ - Upstream product philosophy and shape.
- https://www.cmux.dev/docs/concepts - Upstream pane / tab / session concepts.
- https://www.cmux.dev/docs/notifications - Upstream notification semantics.

### Secondary (MEDIUM confidence)
- https://wezterm.org/features.html - Multiplexer feature expectations.
- https://wezterm.org/config/lua/wezterm.mux/index.html - Workspace / mux concepts.
- https://wezterm.org/config/lua/window/toast_notification.html - Notification behavior reference.

### Tertiary (LOW confidence)
- None.

---
*Research completed: 2026-03-06*
*Ready for roadmap: yes*
