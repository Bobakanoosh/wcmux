# Feature Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Feature Landscape

### Table Stakes (Users Expect These)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| ConPTY-backed interactive terminal sessions | Without this, the app is not a credible terminal product on Windows | HIGH | Must support shells and TUIs with resize, input, and ANSI/VT handling intact. |
| Horizontal and vertical pane splitting | Windows Terminal and cmux both set this expectation | MEDIUM | Must support focus, resize, close, and directional splits. |
| Multiple tabs / workspaces | Users need concurrent layouts, not a single terminal tree | MEDIUM | Tabs should preserve pane trees and shell state per tab. |
| Shell profiles and working-directory launch | Windows users expect PowerShell, Command Prompt, WSL, and custom shells | MEDIUM | Dynamic profile discovery is a useful reference behavior. |
| Clipboard, search, and scrollback | Baseline usability requirement for any serious terminal UI | MEDIUM | Not the user's first ask, but quickly becomes make-or-break in real use. |
| Windows notifications for attention events | This is part of the product thesis, not optional polish | MEDIUM | Must work with Claude/Codex hooks and survive app backgrounding. |

### Differentiators (Competitive Advantage)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Agent-aware notification routing | Lets users see exactly which pane/tab needs attention | MEDIUM | Strong fit with cmux-style workflows and a good early differentiator on Windows. |
| CLI/API automation for panes, tabs, and keystrokes | Preserves the "primitive, not a workflow" philosophy from cmux | HIGH | Important for parity, but can be phased after the terminal core is solid. |
| Sidebar metadata per tab | Branch, cwd, latest alert text, shell label | MEDIUM | Helpful, but should not block v1 terminal credibility. |
| Session/layout restore | Makes the app feel like a daily driver | HIGH | Layout restore is much easier than process restore and should be scoped separately. |
| In-app browser | Moves toward fuller cmux parity | HIGH | Valuable, but not part of the terminal-first MVP. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Fake "terminal" built from plain stdout/stderr pipes | Seems faster to ship | Breaks full-screen apps, resize semantics, signal handling, and terminal fidelity | Use ConPTY from the start |
| Full cmux parity in v1 | Attractive because the upstream product already exists | Turns a hard terminal-hosting project into three projects at once: terminal, browser, and automation platform | Ship the terminal core first |
| Mandatory agent workflow assumptions | Feels productized | Violates the stated `cmux` principle of composable primitives | Offer notifications/hooks/API, not enforced workflows |
| Cloud dependency for core behavior | Seems convenient for sync or telemetry | Adds fragility and conflicts with a local developer-tooling product | Keep the core local-first |

## Feature Dependencies

```text
ConPTY-backed sessions
  -> requires -> VT rendering + input routing
  -> requires -> resize propagation

Pane splitting
  -> requires -> session/view abstraction
  -> requires -> layout tree state

Notifications
  -> requires -> attention event protocol
  -> enhances -> tabs/workspaces

CLI/API automation
  -> requires -> stable command router
  -> enhances -> panes, tabs, notifications

Browser pane
  -> conflicts with -> terminal-first MVP focus
```

### Dependency Notes

- **Pane splitting requires a session/view abstraction:** panes should point at terminal sessions through a stable model, not directly own the process lifecycle.
- **Notifications require an attention event protocol:** support OSC-based signals and a manual CLI path instead of hard-coding one agent vendor.
- **CLI/API automation requires a command router:** otherwise every future automation hook becomes UI-coupled glue.
- **Browser pane conflicts with terminal-first MVP:** it expands scope before the terminal foundation is validated.

## MVP Definition

### Launch With (v1)

- [ ] ConPTY-backed terminal sessions - the product fails without real terminal fidelity
- [ ] Horizontal and vertical pane splitting - core workspace composition primitive
- [ ] Multiple tabs with independent pane trees - needed for parallel coding sessions
- [ ] Windows notifications from terminal/CLI attention signals - core product promise
- [ ] Shell profile launch for PowerShell, Command Prompt, and WSL - needed for real Windows usage

### Add After Validation (v1.x)

- [ ] Search, better scrollback controls, and polished copy/paste - add once the terminal core is stable
- [ ] Sidebar metadata per tab - add when users need more at-a-glance context
- [ ] Layout restore on app relaunch - add once state modeling is trustworthy
- [ ] CLI/API for workspace automation - add after the internal command model is proven

### Future Consideration (v2+)

- [ ] In-app browser - valuable for parity but not required to validate the concept
- [ ] Full session restore including live process continuity - much harder than layout restore
- [ ] Cross-machine sync or cloud features - defer until there is strong local usage

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Real terminal sessions | HIGH | HIGH | P1 |
| Pane splitting | HIGH | MEDIUM | P1 |
| Multiple tabs | HIGH | MEDIUM | P1 |
| Notifications | HIGH | MEDIUM | P1 |
| Shell profiles | HIGH | MEDIUM | P1 |
| CLI/API automation | HIGH | HIGH | P2 |
| Sidebar metadata | MEDIUM | MEDIUM | P2 |
| Layout restore | MEDIUM | HIGH | P2 |
| In-app browser | MEDIUM | HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have after core validation
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | Competitor A | Competitor B | Our Approach |
|---------|--------------|--------------|--------------|
| Pane splitting | Windows Terminal supports vertical/horizontal panes | cmux supports pane splits plus tab/workspace structure | Match the baseline in v1 |
| Tabbed layouts | Windows Terminal supports tabs | cmux uses workspaces + surfaces/tabs | Ship simple tabs first, leave richer workspace taxonomy for later |
| Attention signals | cmux elevates notifications as a first-class concept | Windows Terminal is a general terminal, not agent-specific | Make notifications part of the core v1 |
| Automation | cmux exposes CLI/socket primitives | Windows Terminal exposes commands but not cmux-style automation primitives | Defer to v1.x after internal command routing is stable |

## Sources

- `cmux` upstream README and "The Zen of cmux" section
- Microsoft Learn: Windows Terminal installation, panes, tab behaviors, shell integration
- Microsoft Learn: ConPTY documentation
- Microsoft Learn: App notifications guidance
- Tauri official docs and xterm.js docs for fallback architecture expectations

---
*Feature research for: Windows-first desktop terminal multiplexer*
*Researched: 2026-03-06*
