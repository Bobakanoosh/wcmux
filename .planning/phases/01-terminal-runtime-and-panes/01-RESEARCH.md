# Phase 1 Research: Terminal Runtime And Panes

**Phase:** 1
**Goal:** Deliver real Windows terminal sessions with reliable horizontal and vertical pane splitting.
**Requirements:** `SESS-01`, `SESS-02`, `LAYT-01`, `LAYT-02`, `LAYT-03`
**Status:** Ready for planning
**Research date:** 2026-03-06

## Planning Answer

Plan Phase 1 around one hard truth: terminal credibility is the phase, and pane UX only counts if it sits on a correct ConPTY runtime. The planner should optimize for a Windows-first shell with a real ConPTY-backed session host, a proven terminal renderer, and a layout model that stays independent from UI widgets.

The best primary path is:

1. Native Windows shell with `.NET + WinUI 3 / Windows App SDK`
2. ConPTY-backed session runtime implemented behind a shell-agnostic host boundary
3. `WebView2 + xterm.js` as the terminal surface inside the native shell
4. A reducer-style split-tree layout model that owns pane focus and resize semantics

This keeps the app Windows-first and Electron-free, avoids hand-rolling terminal emulation, and gives Phase 1 a realistic route to shipping `pwsh`, `vim`, `fzf`, resize, and pane splitting without betting the schedule on a custom renderer.

## Phase Scope

### In Scope

- Launch one ready-to-use default shell on app startup
- Host the shell through ConPTY, not plain pipes
- Keep interactive TUIs working during input, resize, and exit
- Split the active pane horizontally or vertically
- Create each new pane as a fresh session in the source pane's last known working directory
- Support keyboard focus movement and keyboard pane resizing
- Support mouse focus and a top-right split affordance per pane

### Explicitly Out of Scope

- Tabs
- Rich pane metadata and labeling beyond what Phase 1 needs internally
- Notifications
- Browser surfaces
- Public automation APIs
- Broad shell UX polish beyond the default-shell-first path

## Standard Stack

### Primary Recommendation

| Area | Recommendation | Why |
|------|----------------|-----|
| Desktop shell | `WinUI 3 + Windows App SDK` | Native Windows windowing, lifecycle, packaging path, and future notification support |
| Runtime | `.NET` | Lowest friction for a first native Windows app while still allowing direct Win32 interop |
| PTY/session host | ConPTY via Win32 interop | Required for real terminal semantics on Windows |
| Terminal renderer | `xterm.js` hosted in `WebView2` | Proven VT renderer with good TUI compatibility; much lower Phase 1 risk than building a native renderer |
| App architecture | Shell-agnostic core plus native shell adapter | Preserves a future Tauri fallback without making Phase 1 cross-platform |
| Concurrency | Dedicated background pumps plus channels/events | Matches ConPTY servicing requirements and avoids UI-thread deadlocks |
| Validation | Domain unit tests plus real-session integration harness | Phase 1 success depends on lifecycle and fidelity, not only UI behavior |

### Why This Is The Best Phase 1 Path

- It stays Windows-first without adopting Electron.
- It keeps the window chrome and app shell native while using a renderer that already understands terminal escape semantics.
- It lets the planner split the work cleanly into runtime, renderer integration, and layout plans.
- It avoids the highest-risk Phase 1 trap: inventing a terminal control and a ConPTY runtime at the same time.

### Realistic Stack Options

| Option | Description | Upside | Downside | Recommendation |
|--------|-------------|--------|----------|----------------|
| A | `WinUI 3 + .NET + custom/native terminal renderer + ConPTY` | Most native end state | Highest risk; Phase 1 becomes terminal-emulator work | Reject for Phase 1 |
| B | `WinUI 3 + .NET + WebView2 + xterm.js + ConPTY` | Native Windows shell with proven renderer and manageable implementation risk | Uses a webview for the terminal surface | Primary path |
| C | `Tauri 2 + Rust + xterm.js + ConPTY backend` | Lower UI cost and clean Rust backend story | Gives up the native-first shell as the default path and still uses a webview | Keep as fallback only |

### Decision

Use Option B for planning. Keep the architecture clean enough that Option C remains possible later, but do not plan Phase 1 around a shell swap.

## Architecture Patterns

### Pattern 1: Session Host Separate From Pane UI

A pane references a `SessionId`. It never owns a process directly. Process lifecycle belongs to the session host, and pane state belongs to the layout domain.

### Pattern 2: Terminal Surface Adapter

The native shell talks to a terminal surface adapter, not directly to `xterm.js`. Phase 1 can then treat `WebView2 + xterm.js` as an implementation detail behind a small interface:

- `attach(sessionId, surfaceId)`
- `write(data)`
- `setFocus()`
- `getCellSize()`
- `dispose()`

This is the seam that keeps the shell replaceable later.

### Pattern 3: Reducer-Style Layout State

Pane splits, focus movement, close behavior, and resize should be pure state transitions over a split tree. Views render the tree; they do not define it.

### Pattern 4: Evented Session Runtime

ConPTY input, output, exit, and resize acknowledgements should be emitted as typed events into the app core. The UI consumes state; it does not poll handles.

### Pattern 5: PowerShell-First, Shell-Agnostic Session Spec

Phase 1 should default to PowerShell, but the runtime API should already accept a general `SessionLaunchSpec`:

- executable path
- args
- initial cwd
- environment overrides
- profile kind

That keeps `cmd` or `wsl.exe` expansion from becoming a rewrite later.

## Terminal Runtime Architecture

### Recommended Runtime Components

| Component | Responsibility |
|-----------|----------------|
| `SessionManager` | Create, track, close, and observe sessions |
| `ConPtyHost` | Own pseudoconsole handles, child process launch, and resize |
| `IoPump` | Separate input and output loops per session |
| `ShellBootstrap` | Resolve default shell and inject PowerShell-first cwd tracking |
| `TerminalSurfaceBridge` | Move VT output to the renderer and user input back to ConPTY |
| `LayoutStore` | Own pane tree, focus state, and split/resize commands |

### Session Lifecycle

#### Startup

1. App launches and creates one root pane immediately.
2. `SessionManager` resolves the default shell profile.
3. `ShellBootstrap` builds a launch spec using the app startup cwd.
4. `ConPtyHost` creates pipes, creates the pseudoconsole, and launches the child process with `STARTUPINFOEX`.
5. Input and output pumps start before the UI begins interactive use.
6. The terminal surface attaches to the session and begins rendering the VT stream.
7. The session publishes `Ready`.

#### Split Creation

1. User requests horizontal or vertical split on the active pane.
2. `LayoutStore` creates a sibling leaf in the split tree.
3. `SessionManager` launches a new shell session.
4. The new session's `initial cwd` comes from the source pane's `lastKnownCwd`.
5. If no cwd has been observed yet, fall back to the source session's launch cwd.
6. Focus moves to the new pane after attach succeeds.

#### Exit and Close

1. If the process exits naturally, emit `SessionExited` and keep the pane close behavior deterministic.
2. If the user closes a pane, request graceful shutdown first.
3. If the session does not exit in a short grace window, terminate it explicitly and release handles.
4. Collapse the split tree and restore focus to the most recently related pane.

### Session Invariants

- A pane never reuses another live process.
- A split creates a new session; it does not clone process state.
- Each session owns exactly one ConPTY host.
- Session exit is observable even if the pane UI is already gone.
- Handle cleanup is part of the normal lifecycle, not best-effort cleanup.

## Render And Input Integration

### Recommended Terminal Surface Strategy

Use `xterm.js` inside `WebView2` for Phase 1. The WinUI shell should host one surface per pane and communicate with it through a narrow bridge.

### Output Flow

1. ConPTY emits VT output bytes.
2. `IoPump` forwards chunks to `TerminalSurfaceBridge`.
3. The bridge batches writes onto the pane's `xterm.js` instance.
4. The surface renders without the core depending on DOM details.

### Input Flow

1. Active pane surface captures keyboard input, paste, and mouse interactions relevant to terminal use.
2. The bridge sends raw terminal input back to the session host.
3. `IoPump` writes to the ConPTY input pipe.

### Important Rules

- Keyboard focus belongs to exactly one pane at a time.
- Pane-level shortcuts must be intercepted above the terminal surface so split, focus, and resize commands do not depend on shell bindings.
- Mouse clicks inside a pane should focus the pane first, then forward the event to the terminal surface when appropriate.
- The top-right split affordance should live in native pane chrome, not inside the terminal content area.

### PowerShell Working Directory Tracking

Phase 1 needs new splits to inherit the source pane's cwd. Do not try to infer cwd through process inspection. Instead:

1. Launch PowerShell through a small bootstrap command or profile snippet.
2. Emit cwd changes through a stable terminal-side signal such as `OSC 7`.
3. Parse and persist the latest reported cwd per session.
4. Use that stored cwd for future splits.

This is concrete, PowerShell-first, and much less fragile than Win32 introspection hacks.

## Pane Layout Model

### Recommended State Shape

Use a binary split tree.

```ts
type PaneId = string;
type SessionId = string;

type LayoutNode =
  | {
      kind: "leaf";
      paneId: PaneId;
      sessionId: SessionId;
    }
  | {
      kind: "split";
      axis: "horizontal" | "vertical";
      ratio: number;
      first: LayoutNode;
      second: LayoutNode;
    };
```

Keep separate state for:

- `activePaneId`
- `focusHistory`
- `paneRects`
- `pendingSessionAttach`

### Focus Model

Use directional focus commands backed by actual rendered pane rectangles, not only tree order. That produces correct behavior for uneven layouts.

Recommended behavior:

- Keyboard focus moves to the nearest pane in the requested direction
- Mouse click focuses the clicked pane
- After split, the new pane becomes active
- After close, focus returns to the most recently related pane, then falls back to the surviving sibling if needed

### Resize Model

Pane resizing should mutate ancestor split ratios. Do not resize leaf panes directly. The reducer should:

1. Find the closest ancestor split matching the requested resize axis.
2. Adjust its ratio.
3. Clamp the result to a minimum pane size.
4. Recompute pixel rects, then recompute terminal rows and columns.

Recommended minimum size:

- `20` columns
- `6` rows

Use cell-based minimums, not pixel-based minimums.

## Resize And Focus Behavior

### Window And Pane Resize Pipeline

1. Window changes size.
2. Layout tree recomputes pane rectangles in pixels.
3. Each pane surface reports measured cell width and height.
4. The app converts pane rectangles to terminal rows and columns.
5. Only changed row/column values trigger `ResizePseudoConsole`.

### Resize Rules

- Debounce rapid pixel resize changes before calling `ResizePseudoConsole`.
- Never send redundant resize calls when cols/rows are unchanged.
- The renderer and host must agree on the live cell size before the session is marked ready.
- TUI validation must include aggressive resize scenarios, not only normal window drags.

### Focus And Shortcut Rules

- Keyboard split commands are explicit: one action for horizontal, one for vertical.
- Keyboard focus movement is directional.
- Keyboard pane resizing uses directional nudges with repeat support.
- Pane close never chooses an arbitrary survivor when a relationship can be inferred.

## Validation Architecture

Phase 1 can support Nyquist-style validation if the planner treats validation as architecture, not a cleanup task. The plan should require explicit seams that let each requirement be tested below the full UI level.

### Validation Layers

| Layer | What It Validates | Recommended Technique |
|-------|-------------------|-----------------------|
| Domain tests | Split tree transitions, focus movement, close semantics, resize ratio math | Pure unit tests against reducer/state transitions |
| Session host integration | ConPTY launch, input/output, resize, exit, cleanup | Real-process integration tests against `pwsh` in a test harness |
| Terminal bridge tests | VT chunk forwarding, input routing, cwd signal parsing | Surface adapter tests with scripted fixtures |
| Acceptance checks | End-user fidelity for TUIs and pane interactions | Manual UAT scripts plus a small repeatable smoke harness |

### Requirement-To-Validation Mapping

| Requirement | Primary Validation |
|-------------|--------------------|
| `SESS-01` | Session host integration proves shell launch and interactive IO through ConPTY |
| `SESS-02` | TUI acceptance matrix proves input, resize, alternate screen, and exit behavior |
| `LAYT-01` | Domain and acceptance tests prove horizontal split creation |
| `LAYT-02` | Domain and acceptance tests prove vertical split creation |
| `LAYT-03` | Domain tests plus smoke checks prove focus movement and pane resizing |

### Phase 1 Smoke Matrix

Every Phase 1 plan should preserve this smoke set:

- Launch app and get one ready `pwsh` pane
- Run `vim` and exit cleanly
- Run `fzf` or another full-screen selector and confirm keyboard input stays correct
- Resize the window aggressively while a TUI is active
- Split horizontally and vertically from a pane that changed directories
- Close panes in uneven trees and confirm focus restoration
- Repeat open/split/resize/close loops without hangs or orphaned processes

### Nyquist Support Recommendation

Create a small requirement-to-test map during planning and keep it versioned with the phase. Each of the three plans should add or extend automated checks for its own area before the next plan builds on it.

## Don't Hand-Roll

- Do not build a custom terminal emulator for Phase 1.
- Do not treat plain process pipes as an acceptable terminal backend.
- Do not store pane layout only in WinUI controls or visual tree state.
- Do not inspect process internals to guess cwd when a shell-side signal can provide it.
- Do not make Tauri the default plan unless the native shell path fails a concrete feasibility checkpoint.

## Common Pitfalls

### Fake Terminal Hosting

If the runtime is just a spawned process with stdout in a text box, `SESS-01` and `SESS-02` are already missed.

### ConPTY Deadlocks

If input, output, resize, and teardown are not serviced independently, the app will hang intermittently and be hard to debug.

### Renderer And Runtime Tightly Coupled

If `xterm.js` details leak into core session logic, a shell swap or test harness becomes much harder later.

### Layout State In The UI Tree

If pane widgets define the split model, close semantics and future tabs will be fragile immediately.

### Resize Spam

If every pixel delta becomes a ConPTY resize, TUIs will flicker and input stability will degrade under live resizing.

## Code Examples

### Session Launch Contract

```ts
interface SessionLaunchSpec {
  executable: string;
  args: string[];
  cwd: string;
  env: Record<string, string>;
  profileKind: "pwsh" | "cmd" | "wsl";
}
```

### Session Events

```ts
type SessionEvent =
  | { kind: "ready"; sessionId: string }
  | { kind: "output"; sessionId: string; data: Uint8Array }
  | { kind: "cwdChanged"; sessionId: string; cwd: string }
  | { kind: "resized"; sessionId: string; cols: number; rows: number }
  | { kind: "exited"; sessionId: string; exitCode: number | null };
```

### Layout Commands

```ts
type LayoutCommand =
  | { kind: "splitActivePane"; axis: "horizontal" | "vertical" }
  | { kind: "focusDirection"; direction: "left" | "right" | "up" | "down" }
  | { kind: "resizePane"; direction: "left" | "right" | "up" | "down"; cells: number }
  | { kind: "closeActivePane" };
```

## Planner Handoff

The planner should derive exactly three executable plans from this research.

### Plan 01-01: Session Core And ConPTY Host

Deliver:

- Project bootstrap for the native Windows shell
- `SessionManager`, `ConPtyHost`, launch spec model, and lifecycle events
- Default-shell startup into a ready PowerShell pane
- Graceful exit and forced cleanup paths
- PowerShell-first cwd tracking contract

Validation gate:

- `pwsh` launches, accepts input, produces output, exits cleanly, and survives repeated open/close loops

### Plan 01-02: Terminal Surface, IO Bridge, And Fidelity

Deliver:

- `WebView2 + xterm.js` pane surface
- Output batching and input bridge between session host and renderer
- Initial size negotiation and stable row/column resize handling
- TUI smoke coverage for `vim`, `fzf`, paste, and aggressive resize

Validation gate:

- Full-screen TUIs remain usable during typing, paste, resize, and exit

### Plan 01-03: Split Tree, Pane Commands, Focus, And Resize

Deliver:

- Split-tree state model and reducers
- Horizontal and vertical split commands
- Pane creation from source cwd
- Directional focus movement, keyboard resize, mouse focus, and split affordance
- Deterministic close and focus restoration behavior

Validation gate:

- Uneven pane trees remain interactive, directional focus works, and pane close/resize behavior is deterministic

## Final Recommendation

Phase 1 should be planned as a native Windows shell wrapped around a proven terminal surface and a disciplined ConPTY core. The planner should treat terminal runtime correctness as the dependency for everything else, then land renderer fidelity, then add pane mechanics on top of that stable base.
