# Feature Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these makes the product feel incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Real terminal session hosting | `cmux`, WezTerm, and Windows Terminal all treat terminal fidelity as the base capability rather than an optional wrapper | HIGH | Must preserve ConPTY-backed behavior for TUIs, prompts, resize, copy/paste, and process exit |
| Horizontal and vertical pane splits | Split panes are standard in modern terminal multiplexer workflows and are explicitly present in Windows Terminal and WezTerm | MEDIUM | Needs deterministic layout math and keyboard focus movement |
| Multiple tabs / workspaces | `cmux` and WezTerm both make tabs and workspaces first-class organization primitives | MEDIUM | V1 can start with tabs; richer named workspaces can follow after validation |
| Keyboard-first navigation and resize | Multiplexer users expect fast focus changes and pane manipulation without mouse dependency | MEDIUM | Shortcut design must be consistent with horizontal/vertical terminology |
| Per-pane identity and cwd context | Users need to know which tool/session they are looking at and where it is running | MEDIUM | Titles, cwd, and unread/attention state matter more once multiple agents are live |

### Differentiators (Competitive Advantage)

Features that set the product apart. Valuable, but not required for the first credible release.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Windows desktop notifications for attention events | Matches `cmux`'s AI-session workflow and makes long-running agent sessions practical on Windows | MEDIUM | Strong candidate for v1 because the user explicitly wants it, but it still sits above terminal correctness |
| Generic attention signaling instead of tool-specific hooks | Keeps the app unopinionated and useful for Claude, Codex, shell scripts, and future tools | MEDIUM | Prefer OSC-based or generic session-state signals over vendor-specific integrations |
| Native Windows shell with Rust core | Gives the project a real Windows identity rather than feeling like a web shell port | HIGH | Differentiates from browser-first desktop wrappers, but raises implementation risk |
| Workspace save/restore | Lets users return to complex tab/pane arrangements | MEDIUM | Likely v1.x once the live layout model is stable |
| Future automation CLI / local API | Brings the project closer to broader `cmux` parity without hard-coding an agent workflow | HIGH | Best deferred until core session semantics are stable |

### Anti-Features (Commonly Requested, Often Problematic)

Features that sound attractive but are likely to hurt v1.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Embedded browser surface in v1 | `cmux` includes browser surfaces and some users may equate that with parity | It adds a second product surface before the terminal core is proven and pushes the app toward a browser shell too early | Defer browser parity until the terminal foundation is trusted |
| Tool-specific workflow rules | Seems convenient for Claude/Codex demos | Makes the app opinionated and fragile when users run anything else | Build around generic terminal sessions and attention signals |
| Cloud sync / remote collaboration early | Sounds modern and useful for setups across machines | Adds auth, sync, and conflict complexity unrelated to the core value | Start with local-only persisted layouts |

## Feature Dependencies

```text
Real terminal session hosting
  -> requires -> ConPTY runtime stability
                  -> requires -> per-session IO isolation

Pane splits
  -> requires -> deterministic layout tree
                  -> requires -> focus and resize commands

Tabs / workspaces
  -> enhances -> pane splits
                  -> enhances -> per-pane identity and cwd context

Notifications
  -> requires -> attention event detection
                  -> requires -> per-pane / per-tab metadata

Automation API
  -> depends on -> stable session + layout model
```

### Dependency Notes

- **Pane splits require a deterministic layout tree:** resizing and focus behavior become brittle if the UI widgets own layout state directly.
- **Notifications require attention event detection:** desktop toasts are only useful if the app can reliably identify which session or pane needs attention.
- **Tabs and workspaces enhance pane management:** they are the structure users need once there is more than one active coding task.
- **Automation depends on stable session semantics:** exposing an API before the session model is stable bakes in the wrong abstractions.

## MVP Definition

### Launch With (v1)

- [ ] Real ConPTY-backed terminal sessions - the product is not credible without terminal fidelity
- [ ] Horizontal and vertical pane splitting - core multiplexer interaction
- [ ] Multiple tabs with independent layouts - required to manage more than one coding context
- [ ] Keyboard-first focus, resize, and tab switching - baseline multiplexer ergonomics
- [ ] Windows attention notifications plus in-app unread state - explicit user requirement and key `cmux`-style workflow advantage

### Add After Validation (v1.x)

- [ ] Named workspace save/restore - add once the live layout model is stable
- [ ] Rich pane metadata and session templates - add when users start reusing patterns
- [ ] Better shell integration for cwd/title detection - add after core runtime behavior is solid

### Future Consideration (v2+)

- [ ] Embedded browser surfaces - defer until parity work becomes strategically important
- [ ] Public automation CLI / local socket API - defer until the internal command model is stable
- [ ] Advanced cross-machine sync / collaboration - defer until there is proven demand

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Real terminal session hosting | HIGH | HIGH | P1 |
| Pane splitting | HIGH | MEDIUM | P1 |
| Multiple tabs | HIGH | MEDIUM | P1 |
| Keyboard navigation and resize | HIGH | MEDIUM | P1 |
| Windows attention notifications | HIGH | MEDIUM | P1 |
| Workspace save/restore | MEDIUM | MEDIUM | P2 |
| Generic attention signaling | HIGH | MEDIUM | P2 |
| Automation API | MEDIUM | HIGH | P3 |
| Embedded browser surface | LOW for v1 | HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | cmux | WezTerm | Our Approach |
|---------|------|---------|--------------|
| Panes | First-class | First-class | Match the table-stakes pane model in v1 |
| Tabs / workspaces | First-class | First-class | Ship tabs in v1; evolve toward richer workspaces after validation |
| Notifications | Explicit product feature for waiting agents and attention | Supported via toast notifications and notification handling controls | Include Windows notifications in v1, but keep them generic rather than agent-specific |
| Browser surface | Included | Not a core surface | Defer until terminal-first core is proven |
| Automation / scripting | Present in broader product surface | Extensive scripting and mux API | Treat as later parity rather than launch scope |

## Sources

- https://www.cmux.dev/ - Upstream product surface and positioning
- https://www.cmux.dev/docs/concepts - Upstream concepts for panes, tabs, workspaces, and surfaces
- https://www.cmux.dev/docs/notifications - Upstream attention / notification model
- https://wezterm.org/features.html - Reference for table-stakes multiplexer capabilities
- https://wezterm.org/config/lua/wezterm.mux/index.html - Reference for tabs, panes, windows, and workspaces as first-class concepts
- https://wezterm.org/config/lua/window/toast_notification.html - Reference for desktop attention notifications in a terminal workflow
- https://learn.microsoft.com/en-us/windows/terminal/panes - Reference for Windows users' baseline pane expectations

---
*Feature research for: Windows-first desktop terminal multiplexer for AI coding workflows*
*Researched: 2026-03-06*
