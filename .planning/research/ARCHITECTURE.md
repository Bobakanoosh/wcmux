# Architecture Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Standard Architecture

### System Overview

```text
+--------------------------------------------------------------+
| Native Desktop Shell                                         |
| WinUI 3 views, tabs, panes, command palette, settings        |
+-------------------------+------------------------------------+
                          |
                          v
+--------------------------------------------------------------+
| Application Core                                              |
| Commands, layout tree, focus model, profile registry         |
| notification router, persistence, event bus                  |
+-------------+--------------------------+---------------------+
              |                          |
              v                          v
+-----------------------------+   +---------------------------+
| Terminal Session Host       |   | Notification Service      |
| ConPTY lifecycle            |   | Windows app notifications |
| process launch              |   | alert dedupe + unread     |
| stdin/stdout pump           |   | per-pane/tab state        |
| resize + teardown           |   +---------------------------+
+-------------+---------------+
              |
              v
+--------------------------------------------------------------+
| Shell Processes                                               |
| pwsh, cmd, wsl, codex, claude, other CLI tools               |
+--------------------------------------------------------------+
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| UI shell | Owns windows, tabs, panes, keyboard shortcuts, settings, and rendering | WinUI 3 views and controls |
| App core | Owns commands, layout state, focus model, persistence, and orchestration | Plain application services and immutable-ish state transitions |
| Terminal session host | Owns ConPTY handles, process creation, I/O loops, resize, and cleanup | Win32 interop over `CreatePseudoConsole`, `CreateProcess`, and dedicated pump threads |
| Notification service | Turns attention events into local unread state and Windows notifications | Windows App SDK app notifications plus in-memory/evented state |
| Persistence layer | Stores profiles, layout snapshots, and preferences | Local JSON or SQLite, depending on how fast state grows |

## Recommended Project Structure

```text
src/
|-- app/                # bootstrap, DI, command routing
|-- domain/             # layout tree, tabs, panes, profiles, notifications
|-- terminal/           # ConPTY host, process lifecycle, VT stream handling
|-- ui/                 # WinUI views, view models, keybindings
|-- notifications/      # local app notification adapters
|-- persistence/        # settings and layout snapshot storage
|-- integrations/       # shell profile discovery, future CLI/API hooks
`-- tests/              # domain and integration coverage
```

### Structure Rationale

- **`domain/`:** keeps pane/tab/session rules independent from UI technology so Tauri fallback remains possible.
- **`terminal/`:** isolates the most failure-prone subsystem and makes it testable with focused integration tests.
- **`ui/`:** avoids polluting terminal/session code with view concerns.
- **`integrations/`:** gives future automation and hook protocols a clean home without infecting the core.

## Architectural Patterns

### Pattern 1: Session/View Separation

**What:** A pane references a session object instead of owning a process directly.
**When to use:** Always. This is the cleanest way to support splits, focus changes, duplication, and later restore/automation.
**Trade-offs:** Slightly more indirection, but it prevents the UI tree from becoming the source of truth for process state.

**Example:**
```typescript
type SessionId = string;
type PaneId = string;

interface PaneNode {
  id: PaneId;
  sessionId: SessionId;
}
```

### Pattern 2: Evented Terminal Pump

**What:** Dedicated terminal I/O workers publish structured events back into the app core.
**When to use:** Required with ConPTY because Microsoft explicitly warns about deadlocks when synchronous channels are not serviced separately.
**Trade-offs:** More lifecycle code, but much safer than tying reads/writes to the UI thread.

**Example:**
```typescript
type TerminalEvent =
  | { kind: "output"; sessionId: string; data: Uint8Array }
  | { kind: "exit"; sessionId: string; code: number | null }
  | { kind: "attention"; sessionId: string; source: "osc" | "cli" };
```

### Pattern 3: Command Bus over Direct View Actions

**What:** Keyboard shortcuts, menu actions, and future CLI/API calls all hit the same command layer.
**When to use:** From the start if the project wants to preserve the cmux "primitive, not a solution" philosophy.
**Trade-offs:** More upfront structure, but it avoids rewriting the app when automation arrives.

## Data Flow

### Request Flow

```text
[User action]
    |
    v
[UI command] -> [App core] -> [Terminal host / Notification service]
    |                |                     |
    v                v                     v
[View update] <- [State store] <- [Terminal events / alert events]
```

### State Management

```text
[State store]
    ^
    |
[Reducers / command handlers] <- [UI intents, terminal events, restore events]
    |
    v
[WinUI views subscribe and render]
```

### Key Data Flows

1. **Session startup:** user opens a tab or pane -> app core resolves shell profile -> terminal host creates ConPTY + child process -> UI subscribes to stream events.
2. **Pane resize:** view size changes -> app core computes terminal character size -> terminal host calls `ResizePseudoConsole` -> shell redraws.
3. **Attention event:** session emits OSC/CLI signal -> notification service marks pane/tab unread -> Windows notification is raised -> user jumps back into the pane.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1-20 concurrent sessions on one machine | Simple in-process app core is fine |
| 20-100 sessions / many tabs | Optimize render invalidation, scrollback storage, and event batching |
| Remote control / multi-window / CLI automation | Add a local IPC boundary without splitting the app into network services |

### Scaling Priorities

1. **First bottleneck:** terminal rendering and scrollback pressure - fix with efficient diffing, capped scrollback, and batched UI updates.
2. **Second bottleneck:** session lifecycle bugs - fix with a hardened ConPTY host and better teardown integration tests.

## Anti-Patterns

### Anti-Pattern 1: UI Tree Owns Process State

**What people do:** make each pane view create and own its shell process.
**Why it's wrong:** pane moves, restores, duplication, and background events become fragile immediately.
**Do this instead:** keep process/session ownership in the terminal host and let panes reference sessions.

### Anti-Pattern 2: Treat Shell Spawn as Terminal Hosting

**What people do:** spawn a process and stream stdout into a text widget.
**Why it's wrong:** this is not a PTY and it breaks terminal fidelity.
**Do this instead:** use ConPTY and route VT streams through a dedicated terminal host.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Windows app notifications | Windows App SDK API | Elevated apps are a known limitation. |
| Shell executables (`pwsh`, `cmd`, `wsl.exe`) | Direct process launch through ConPTY | Profile discovery should be explicit and user-editable. |
| Future automation CLI/API | Local command bus plus IPC shim | Keep optional so the core stays unopinionated. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `ui` <-> `domain` | commands + state subscription | Keep view models dumb |
| `domain` <-> `terminal` | typed commands/events | critical for testing and later automation |
| `domain` <-> `notifications` | typed attention events | lets notifications remain optional and replaceable |

## Native vs Tauri Impact

- **Native-first inference:** WinUI 3 is the better fit for the primary product because the user explicitly wants a native Windows app if possible, and Windows-specific windowing plus notifications are first-class in the Microsoft stack.
- **Tauri fallback inference:** Tauri is viable only if the team accepts a webview-rendered terminal surface. It still needs a ConPTY backend; the shell plugin alone is not enough for a real terminal.
- **Shared recommendation:** keep `domain` and `terminal` independent from the view layer so a Tauri fallback or hybrid shell remains possible without rewriting the session core.

## Recommended Build Order

1. **Terminal host spike:** prove ConPTY session creation, input/output pumps, resize, and clean teardown.
2. **Single-tab UI shell:** render one terminal session in a native window with profile launch.
3. **Layout engine:** add split panes, focus movement, close/resize actions, and tab state.
4. **Notification path:** wire OSC/CLI attention signals to unread state and Windows notifications.
5. **Persistence + automation:** add layout restore, settings, and later CLI/API surfaces.

## Sources

- Microsoft Learn: Creating a Pseudoconsole session
- Microsoft Learn: Windows App SDK overview and release channels
- Microsoft Learn: App notifications overview and desktop notification guides
- Microsoft Learn: Windows Terminal panes, tabs, and shell integration docs
- Tauri official docs: architecture, frontend hosting model, shell plugin, notification plugin
- `cmux` upstream README and "The Zen of cmux" section

---
*Architecture research for: Windows-first desktop terminal multiplexer*
*Researched: 2026-03-06*
