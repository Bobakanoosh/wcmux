# Project Research Summary

**Project:** wcmux
**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Executive Summary

This project is best understood as a Windows-native terminal multiplexer with agent-aware notifications, not as a generic desktop wrapper around shell commands. The upstream `cmux` philosophy matters here: the product should expose composable primitives such as terminals, panes, tabs, notifications, and later automation, without forcing a specific agent workflow.

The main architectural finding is that Windows has a real terminal primitive for this job: ConPTY. If `wcmux` wants credible terminal fidelity, it needs to own ConPTY session lifecycle, VT stream handling, resize propagation, and cleanup. There is no supported "embed the stock console window" path that preserves the product goal. From a product-stack perspective, the cleanest native-first implementation is a WinUI 3 shell on the Windows App SDK with ConPTY behind it. Tauri remains a viable fallback, but only if the project accepts a webview-rendered terminal surface and still builds a ConPTY backend.

The biggest risks are terminal-hosting correctness, premature scope expansion toward full `cmux` parity, and notification behavior drifting between development and installed builds. The roadmap should therefore prove the terminal core first, then add panes/tabs, then wire notifications, and only then move into automation and parity work.

## Key Findings

### Recommended Stack

For the native-first path, the best fit is `.NET 10 + Windows App SDK 1.8.5 + WinUI 3 + ConPTY`. That stack aligns with the user's preference for a native Windows app, uses Microsoft's current desktop stack, and avoids forcing the first version through a browser-shaped UI. Packaging should be treated as a product concern early because Windows notification behavior is tied to app identity and install flow.

For the fallback path, `Tauri 2 + Rust + ConPTY + @xterm/xterm` is viable and keeps Electron off the table. The important caveat is that Tauri does not remove the need for a true PTY backend. It changes the frontend and packaging story, not the terminal-hosting requirement.

**Core technologies:**
- **Windows App SDK 1.8.5:** native windowing, lifecycle, and notification APIs
- **.NET 10:** current LTS runtime and the least painful native-first developer experience
- **ConPTY:** Windows-supported pseudoterminal host for real shell sessions

### Expected Features

The table-stakes set is narrow and clear: real ConPTY-backed terminal sessions, horizontal and vertical splits, multiple tabs, shell profile launch, and Windows notifications for attention events. These are enough to validate the concept and match the user's stated goals.

**Must have (table stakes):**
- Real terminal sessions - users expect actual shell fidelity
- Pane splitting - core composition primitive
- Multiple tabs - required for concurrent layouts
- Windows notifications - part of the product thesis, not optional polish

**Should have (competitive):**
- Agent-aware unread state by pane/tab - differentiator for AI coding workflows
- Later CLI/API automation - preserves the cmux primitive model

**Defer (v2+):**
- In-app browser - useful for parity, not needed to validate the terminal core
- Full live session restore - much harder than layout restore

### Architecture Approach

The recommended architecture is a native desktop shell over a domain/app core that owns layout, commands, and persistence, plus a dedicated terminal-host subsystem that owns ConPTY and process lifecycle. Notifications should be a separate service that consumes attention events and raises Windows notifications without being entangled with pane rendering.

**Major components:**
1. **UI shell** - windows, tabs, panes, shortcuts, settings
2. **App core** - commands, layout tree, profile registry, persistence, unread state
3. **Terminal host** - ConPTY session creation, I/O pumps, resize, teardown

### Critical Pitfalls

1. **Fake terminal backend** - avoid by using ConPTY from the start
2. **ConPTY deadlocks and teardown bugs** - avoid with dedicated pump threads and lifecycle tests
3. **UI-owned layout/process state** - avoid with session/view separation and domain-driven layout state
4. **Notification identity drift** - avoid by validating installed-build notification behavior early
5. **Premature full parity scope** - avoid with a strict terminal-first MVP

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Terminal Foundation
**Rationale:** nothing else matters if terminal fidelity is weak
**Delivers:** ConPTY session host, shell profile launch, one working terminal surface
**Addresses:** real sessions, shell profiles
**Avoids:** fake-terminal and ConPTY lifecycle pitfalls

### Phase 2: Layout Primitives
**Rationale:** panes and tabs are the next indispensable primitives once one session works
**Delivers:** horizontal/vertical splits, multiple tabs, focus and resize behavior
**Uses:** app core + layout tree
**Implements:** session/view separation and pane state modeling

### Phase 3: Attention and Notifications
**Rationale:** notifications are part of the product identity and should land once session routing is stable
**Delivers:** unread state, pane/tab attention markers, Windows notifications, jump-to-attention flow
**Uses:** Windows App SDK notifications or Tauri notification plugin in fallback builds
**Implements:** notification service and signal routing

### Phase 4: Persistence and Automation
**Rationale:** only worth adding after the daily-driver terminal workflow is believable
**Delivers:** layout restore, settings polish, early CLI/API primitives

### Phase Ordering Rationale

- ConPTY and session correctness are hard dependencies for every later feature.
- Pane/tab work should sit on top of a stable terminal-host abstraction, not precede it.
- Notifications depend on session identity and pane/tab routing to feel actionable.
- Automation should reuse the same command layer that keyboard/UI actions already use.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** terminal rendering details, PTY interop edge cases, and shell compatibility tests
- **Phase 3:** packaging/identity choices for notifications in installed builds

Phases with standard patterns (skip research-phase):
- **Phase 2:** pane trees, focus routing, and tab management are established product patterns

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | Official docs are strong, but the native-vs-Tauri choice is still a product tradeoff |
| Features | MEDIUM | User priorities are clear; wider Windows-terminal expectations are inferred from Windows Terminal and cmux |
| Architecture | MEDIUM | ConPTY and app-shell boundaries are well supported, but renderer specifics still need implementation spikes |
| Pitfalls | HIGH | The biggest failure modes are clear from official docs and the product shape |

**Overall confidence:** MEDIUM

### Gaps to Address

- **Terminal renderer specifics:** confirm the exact native rendering approach during Phase 1 spike
- **Packaging strategy:** decide packaged vs unpackaged distribution before Phase 3 notification work hardens
- **Automation surface:** decide how much CLI/API scope belongs in v1.x after the core proves itself

## Sources

### Primary (HIGH confidence)
- Microsoft Learn: ConPTY, Windows App SDK, Windows Terminal, app notifications
- Tauri official docs: Tauri 2 overview, frontend model, shell plugin, notification plugin
- `cmux` upstream README and "The Zen of cmux"

### Secondary (MEDIUM confidence)
- xterm.js official repo and package documentation

### Tertiary (LOW confidence)
- None used materially for core recommendations

---
*Research completed: 2026-03-06*
*Ready for roadmap: yes*
