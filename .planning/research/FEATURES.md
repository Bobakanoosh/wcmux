# Feature Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Real PTY-backed terminal sessions | A terminal multiplexer fails immediately if shells and TUIs do not behave like real terminals | HIGH | Must be ConPTY-backed on Windows; this is the foundation for Claude, Codex, `vim`, `fzf`, and similar tools. |
| Split panes with keyboard focus movement | Multiplexers are expected to manage several live sessions at once in one window | MEDIUM | Horizontal and vertical splits are core UX, with deterministic focus and resize behavior. |
| Multiple tabs / workspaces-in-window | Adjacent tools like Windows Terminal and WezTerm treat tabs as baseline organization, not an advanced feature | MEDIUM | Tabs are the minimum viable way to hold multiple pane arrangements before full workspace persistence exists. |
| Session metadata and clear labels | Users need to know which pane is running which tool or shell without guessing | LOW | Titles should expose pane name, cwd, shell/tool identity, and unread/attention state. |
| Keyboard-first navigation and pane management | Terminal users expect low-friction commands for split, close, move focus, and switch tabs | MEDIUM | Mouse support matters, but keyboard control is table stakes. |
| Attention / unread notifications | `cmux`, WezTerm, and similar tools treat attention state as part of multi-session usability | MEDIUM | v1 should support generic attention events and Windows notifications without assuming a single AI tool. |

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Windows-native shell UX | Makes the product feel purpose-built for Windows instead of a cross-platform compromise | HIGH | Native title bar, notifications, startup behavior, and settings integration are a strong product differentiator if feasible. |
| Generic AI-session attention model | Lets Claude, Codex, and any future terminal tool participate without product lock-in | MEDIUM | Prefer OSC-based or process-state-based attention semantics over brand-specific hooks. |
| Reusable Rust core with shell-agnostic boundaries | Preserves the option to start native-first and fall back to Tauri without rewriting core logic | HIGH | This is mostly architectural differentiation, but it has direct product impact on speed and maintainability. |
| Optional session restore / saved layouts | Helpful once the pane/tab model is trustworthy, especially for long-running coding sessions | MEDIUM | Valuable soon after launch, but not required to validate terminal fidelity. |
| Future automation surface | Matches part of `cmux`'s long-term appeal without forcing v1 into an opinionated agent workflow | HIGH | Defer public scripting until core state and security boundaries are stable. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Embedded browser in v1 | Upstream `cmux` includes browser surfaces, so parity pressure is natural | It expands scope before the terminal core is proven and pushes the product toward a browser-first shell | Keep v1 terminal-first; revisit browser surfaces after validation |
| Agent-specific workflow UI | It feels tempting to ship a "Claude mode" or "Codex mode" quickly | It violates the non-opinionated goal and creates brittle integrations tied to individual tools | Model generic sessions, metadata, and attention states instead |
| Cloud sync / multi-machine state in v1 | Users often imagine carrying layouts everywhere | It adds auth, sync, conflict, and privacy complexity before the local product is validated | Start with local-only tabs and later add explicit export/import or restore |
| Custom terminal emulation from scratch | It seems like the cleanest way to own the full experience | It is a massive product area on its own and will delay or derail v1 | Build on ConPTY plus a proven rendering strategy first |

## Feature Dependencies

```text
Real PTY-backed terminal sessions
    |--requires--> ConPTY lifecycle and IO handling
    |--enables----> split panes
    |--enables----> multiple tabs
    `--enables----> attention / unread notifications

Split panes
    |--requires--> layout tree + focus model
    `--enhances---> multiple tabs

Attention / unread notifications
    |--requires--> session metadata
    `--requires--> generic attention event parsing

Saved layouts / session restore
    |--requires--> stable tab + pane state model
    `--depends on-> schema-versioned persistence

Automation surface
    `--depends on-> stable session/layout command model
```

### Dependency Notes

- **Split panes require a layout tree and focus model:** pane management only becomes predictable once layout state is modeled independently from UI widgets.
- **Notifications require session metadata:** the app needs pane identity, titles, cwd, and unread state so desktop toasts map back to the correct terminal.
- **Saved layouts depend on a stable state model:** adding restore before tabs and panes are represented cleanly will create migration pain.
- **Automation depends on a stable command model:** exposing a CLI or socket API before internal commands settle will create churn and security risk.

## MVP Definition

### Launch With (v1)

Minimum viable product - what's needed to validate the concept.

- [ ] Open real ConPTY-backed terminal sessions inside the app - proves the core promise.
- [ ] Split panes horizontally and vertically with reliable focus movement - proves multiplexing value.
- [ ] Open and manage multiple tabs of pane arrangements - proves organization beyond a single layout.
- [ ] Surface unread / attention state in-app and through Windows notifications - proves multi-agent usefulness.
- [ ] Show useful pane metadata and keyboard shortcuts - keeps sessions understandable and fast to operate.

### Add After Validation (v1.x)

Features to add once core is working.

- [ ] Saved layouts or session restore - add once pane/tab state is stable and users want persistence.
- [ ] Better shell integration for cwd/title tracking - add once base session management works across PowerShell, cmd, WSL, and Git Bash.
- [ ] Command palette / quick-switch UX - add when the basic keyboard model is established.

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] Browser or webview surfaces - parity pressure exists, but it is not core to validating the Windows terminal product.
- [ ] Public scripting / automation API - valuable long-term, but only after the command/state model is stable.
- [ ] Remote / shared sessions - large scope increase with networking and security implications.

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Real PTY-backed terminal sessions | HIGH | HIGH | P1 |
| Split panes | HIGH | MEDIUM | P1 |
| Multiple tabs | HIGH | MEDIUM | P1 |
| Attention / unread notifications | HIGH | MEDIUM | P1 |
| Pane metadata and keyboard control | HIGH | LOW | P1 |
| Saved layouts / restore | MEDIUM | MEDIUM | P2 |
| Command palette / quick switch | MEDIUM | MEDIUM | P2 |
| Browser parity | LOW for v1 | HIGH | P3 |
| Public automation API | MEDIUM long-term | HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | Competitor A | Competitor B | Our Approach |
|---------|--------------|--------------|--------------|
| Panes and tabs | `cmux` treats panes and tabs as first-class primitives | WezTerm and Windows Terminal both treat panes/tabs as baseline productivity features | Match this in v1; it is core parity, not optional polish |
| Notifications / attention | `cmux` has pane/session notification concepts | WezTerm exposes desktop notifications and notification handling controls | Ship generic Windows attention handling in v1 |
| Browser / extra surfaces | `cmux` goes beyond terminals with browser support | Windows Terminal and WezTerm stay terminal-focused | Defer browser parity until after the terminal core is validated |
| Automation | `cmux` exposes scriptability / CLI-oriented control | WezTerm exposes rich configuration and mux APIs | Keep this as a future direction, not a v1 promise |

## Sources

- https://www.cmux.dev/ - Upstream product shape and terminal-first primitives.
- https://www.cmux.dev/docs/concepts - Upstream concepts for tabs, panes, and sessions.
- https://www.cmux.dev/docs/notifications - Upstream notification model and attention-state semantics.
- https://wezterm.org/features.html - Reference for expected multiplexer capabilities.
- https://learn.microsoft.com/en-us/windows/terminal/panes - Reference for panes as baseline Windows terminal UX.
- https://learn.microsoft.com/en-us/windows/terminal/tips-and-tricks - Reference for tab and keyboard-first usage expectations in Windows Terminal.

---
*Feature research for: Windows-first desktop terminal multiplexer for AI coding workflows*
*Researched: 2026-03-06*
