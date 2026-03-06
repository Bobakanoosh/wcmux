# wcmux

## What This Is

`wcmux` is a Windows-first clone of `cmux`: a composable terminal multiplexer for local AI coding workflows that stays as unopinionated as possible. It is primarily for Windows developers who want to run Claude, Codex, and other terminal tools in real terminal sessions with pane and tab management instead of being forced into a wrapped web-style interface.

## Core Value

Users can manage multiple real Windows terminal sessions in one place without losing terminal fidelity or being pushed into an opinionated workflow.

## Requirements

### Validated

(None yet - ship to validate)

### Active

- [ ] Users can open real Windows terminal sessions with the closest possible native integration.
- [ ] Users can split terminal layouts horizontally and vertically.
- [ ] Users can keep multiple tabbed terminal layouts open at once.
- [ ] Users receive Windows notifications when an attached tool needs attention.

### Out of Scope

- Full `cmux` browser parity in v1 - terminal management matters more than embedded browsing.
- Full `cmux` workspace/session-management parity in v1 - useful, but secondary to proving the terminal core.
- Electron-based implementation - explicitly rejected in favor of native Windows or Tauri.

## Context

The project is inspired directly by `manaflow-ai/cmux`, especially its emphasis on composable primitives over a prescribed agent workflow. Feature parity with `cmux` is a long-term goal, but v1 should be narrower and centered on Windows-native terminal multiplexing for AI-assisted coding. The user has not built a native Windows desktop app before, so the project needs early research into viable native Windows UI stacks, terminal-hosting approaches, and whether Tauri is an acceptable fallback if a fully native path is impractical.

## Constraints

- **Platform**: Windows-first desktop app - the project exists specifically to fill the gap for Windows users.
- **Implementation**: No Electron - explicitly ruled out.
- **Architecture**: Research native Windows first, Tauri second - native is preferred if it can preserve terminal fidelity.
- **UX**: Unopinionated terminal-first design - avoid turning the app into a prescriptive agent shell.
- **Scope**: Terminal-first v1 - browser, workspace, and deeper API parity can wait until the core experience works.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Build a Windows `cmux` analogue rather than a brand-new workflow tool | The goal is to preserve `cmux` principles while making them viable on Windows | - Pending |
| Prioritize real terminal integration over surface-level UI breadth | Terminal fidelity is the defining requirement for credibility | - Pending |
| Research native Windows implementation before committing to Tauri | Native is preferred, but feasibility is still unknown | - Pending |
| Keep v1 focused on terminals, panes, tabs, and notifications | These are the minimum features that make the tool useful day to day | - Pending |

---
*Last updated: 2026-03-06 after initialization*
