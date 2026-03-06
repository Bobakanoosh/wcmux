# Architecture Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Standard Architecture

### System Overview

```text
+-----------------------------------------------------------+
| Native Shell                                              |
| WinUI 3 window, tab strip, command palette, notification  |
| center, settings                                          |
+-------------------------+---------------------------------+
                          |
                          v
+-----------------------------------------------------------+
| Workspace / Layout Layer                                  |
| tabs, panes, focus graph, split tree, persistence model   |
+-------------------------+---------------------------------+
                          |
                          v
+-----------------------------------------------------------+
| Session Orchestration Layer                               |
| terminal session manager, process launcher, cwd tracking, |
| attention events, future automation API                   |
+-------------------------+---------------------------------+
                          |
                          v
+-----------------------------------------------------------+
| Terminal Runtime Layer                                    |
| ConPTY host, pipe IO, screen buffers, escape handling,    |
| shell integration hooks                                   |
+-------------------------+---------------------------------+
                          |
                          v
+-----------------------------------------------------------+
| Windows Platform Services                                 |
| CreatePseudoConsole, process APIs, app notifications,     |
| filesystem, settings store                                |
+-----------------------------------------------------------+
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| Native shell | Owns windows, tabs, commands, settings UI, notification center | WinUI 3 shell with explicit view models over a Rust-backed core |
| Layout engine | Owns split tree, pane sizing, focus moves, tab membership | Pure data model with deterministic reducers and serialization |
| Session manager | Owns session lifecycle, spawn/kill/restart, metadata, attention state | Rust service that wraps process creation and PTY registration |
| Terminal adapter | Bridges session output into a renderable terminal surface | Native control or custom surface over ConPTY output and input |
| Notification service | Maps OSC attention events or process state into Windows notifications and in-app badges | Windows App SDK notifications on native path, Tauri plugin on fallback path |
| Persistence layer | Saves settings and workspace metadata | JSON-backed local store with versioned schema |

## Recommended Project Structure

```text
app/
|-- shell/             # WinUI 3 or Tauri shell project
|-- assets/            # icons, manifests, packaging assets
|
crates/
|-- core-model/        # tabs, panes, commands, persistence schema
|-- pty-host/          # ConPTY lifecycle and pipe handling
|-- session-manager/   # session spawn, restart, cwd, attention state
|-- notifications/     # OSC parsing and desktop notification routing
|-- automation-api/    # future CLI/socket bridge
|
docs/
|-- architecture/      # diagrams, event contracts, platform notes
```

### Structure Rationale

- **`app/shell/`**: Keeps UI-shell choices isolated so native Windows and Tauri remain swappable until the architecture decision is finalized.
- **`crates/`**: Splits the hardest platform logic into testable modules that are not coupled to the UI framework.
- **`docs/architecture/`**: Needed early because PTY behavior and notification semantics are easier to regress than to rediscover.

## Architectural Patterns

### Pattern 1: State-First Layout Model

**What:** Represent each workspace as a split tree plus focus metadata instead of mutating UI widgets directly.  
**When to use:** Always; pane splitting and tab movement need deterministic state transitions.  
**Trade-offs:** Slightly more upfront modeling, much less UI drift and fewer resize/focus bugs later.

### Pattern 2: PTY Isolation Per Session

**What:** Give each terminal session its own ConPTY lifecycle, IO loop, and failure boundary.  
**When to use:** Always; terminal sessions should not block each other.  
**Trade-offs:** More background tasks, but cleaner recovery and easier debugging.

### Pattern 3: OS-Neutral Core, OS-Specific Shell

**What:** Keep layout/session/notification semantics in the Rust core and keep shell-specific rendering in the UI project.  
**When to use:** Use from the first commit if native-vs-Tauri is still open.  
**Trade-offs:** More interface definition work, but it prevents an irreversible shell-framework lock-in too early.

## Data Flow

### Request Flow

```text
User action
  -> shell command handler
  -> layout/session service
  -> ConPTY or state store
  -> shell view update
```

### State Management

```text
Command
  -> reducer / service
  -> workspace state
  -> view model
  -> shell render
```

### Key Data Flows

1. **New split:** User triggers split -> layout engine inserts a pane node -> session manager spawns or duplicates session -> terminal adapter binds the new view.
2. **Attention notification:** PTY output emits OSC attention or other signal -> notification service marks pane unread -> desktop notification fires if focus suppression rules allow it.
3. **Tab switch:** Shell selects workspace -> layout engine loads active split tree -> session manager rebinds visible surfaces -> badge state is cleared or updated.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Single user / local app | Monolithic desktop app with an internal Rust core is sufficient. |
| Power user with many concurrent agents | Add bounded buffering, explicit session backpressure, and better log retention for PTY diagnostics. |
| Automation-heavy future | Split the automation/socket API into its own crate and formalize event contracts before exposing public scripting. |

### Scaling Priorities

1. **First bottleneck:** PTY rendering and resize churn under many live panes - solve with backpressure, coalesced redraws, and explicit buffering.
2. **Second bottleneck:** Layout/state complexity once workspaces and persistence arrive - solve with deterministic reducers and schema versioning.

## Anti-Patterns

### Anti-Pattern 1: UI-Driven Session Ownership

**What people do:** Let each pane widget own the process directly.  
**Why it's wrong:** Closing, moving, or reparenting panes becomes fragile and restart/recovery logic gets duplicated.  
**Do this instead:** Keep sessions in a central manager and let panes be projections of session state.

### Anti-Pattern 2: Mixing Browser-Like Views Into the Core Too Early

**What people do:** Design the shell around future browser/workspace parity before terminal fidelity is proven.  
**Why it's wrong:** The app stops optimizing for terminal correctness and v1 scope balloons.  
**Do this instead:** Make the core about sessions, panes, and tabs first; add browser/API layers only after the terminal core is stable.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| ConPTY / process APIs | Direct Win32 calls from Rust | Core platform dependency; test aggressively around resize, exit, and shutdown paths. |
| Windows app notifications | Native Windows App SDK notification APIs | Best match on native path; supports Action Center / Notification Center behavior. |
| Tauri plugins | Optional shell-layer integration | Only relevant if the fallback shell is chosen. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Shell <-> layout core | Commands + state snapshots | Keep this boundary explicit so shell choice stays reversible. |
| Layout core <-> session manager | Typed commands / events | Needed for splits, tab moves, and future persistence. |
| Session manager <-> notifications | Attention events | Must stay generic so the app works with Claude, Codex, or any terminal process. |

## Sources

- https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session - ConPTY architecture and threading requirements.
- https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tab-view - Native tab shell control guidance for WinUI 3.
- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/ - Windows notification model for desktop apps.
- https://www.cmux.dev/docs/concepts - Upstream workspace / pane / surface hierarchy used as product-shape reference.
- https://www.cmux.dev/ - Upstream feature and philosophy reference.
- https://wezterm.org/features.html - Reference point for table-stakes multiplexer capabilities.
- https://wezterm.org/config/lua/wezterm.mux/index.html - Reference point for workspaces, windows, tabs, and panes as first-class multiplexer concepts.

---
*Architecture research for: Windows-first desktop terminal multiplexer for AI coding workflows*
*Researched: 2026-03-06*
