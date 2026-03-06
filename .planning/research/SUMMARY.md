# Research Summary

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Summarized:** 2026-03-06
**Confidence:** MEDIUM

## Executive Summary

The safest shape for `wcmux` is a terminal-first desktop app with a shell-agnostic Rust core and a native Windows shell researched first. The hard requirement is ConPTY-backed terminal fidelity; if that layer is wrong, the rest of the product does not matter. A credible v1 is narrower than full `cmux` parity: real terminal sessions, horizontal and vertical splits, multiple tabs, keyboard-first navigation, and Windows notifications for attention events.

## Key Findings

### Stack

- **Recommended default:** WinUI 3 / Windows App SDK shell plus a Rust core using ConPTY, `windows`, `tokio`, `serde`, and `tracing`
- **Fallback path:** Tauri 2.x over the same Rust core if native Windows UI proves too slow for a first release
- **Do not use:** Electron, tool-specific notification plumbing, or custom terminal emulation from scratch in v1

### Table Stakes

- Real terminal session hosting
- Horizontal and vertical pane splits
- Multiple tabs with independent layouts
- Keyboard-first focus and resize controls
- Clear pane identity and session context

### Good V1 Scope

- Include desktop notifications because they are central to the intended AI coding workflow
- Keep notifications generic so the app works with Claude, Codex, and other terminal tools
- Defer browser surfaces, broader workspace parity, and public automation APIs until the core session model is stable

### Architecture

- Separate shell, layout model, session manager, terminal runtime, and notification service
- Keep pane/tab state as deterministic data, not UI-owned widget state
- Keep session logic in a reusable core so the native-vs-Tauri shell decision stays reversible

### Watch Out For

- Fake terminal integration that breaks TUIs or shell behavior
- ConPTY deadlocks or hangs from poor IO threading
- Over-scoping parity before the terminal core is trustworthy
- Agent-specific notification semantics that make the app opinionated
- Coupling the shell framework to core session logic too early

## Recommendations For Requirements

### Include In v1

- Terminal session creation and lifecycle
- Horizontal and vertical pane splitting
- Multiple tabs
- Keyboard navigation across panes and tabs
- Windows notifications plus in-app unread state

### Defer To v1.x

- Workspace save/restore
- Rich session templates and metadata polish
- Deeper shell integration for cwd/title inheritance

### Defer To v2+

- Embedded browser surfaces
- Public automation CLI / API
- Cross-machine sync or collaboration features

## Roadmap Implications

1. Start with architecture and runtime choices that preserve native-first optionality.
2. Build and verify the ConPTY-backed terminal core before designing advanced product surfaces.
3. Put pane/tab state management immediately after terminal fidelity, because notifications depend on stable session identity.
4. Add notifications only after attention detection can be modeled generically.
5. Treat full `cmux` parity as later milestones, not launch scope.

## Research Files

- `.planning/research/STACK.md`
- `.planning/research/FEATURES.md`
- `.planning/research/ARCHITECTURE.md`
- `.planning/research/PITFALLS.md`

## Sources

- `.planning/research/STACK.md`
- `.planning/research/FEATURES.md`
- `.planning/research/ARCHITECTURE.md`
- `.planning/research/PITFALLS.md`

---
*Research summary for: Windows-first desktop terminal multiplexer for AI coding workflows*
*Summarized: 2026-03-06*
