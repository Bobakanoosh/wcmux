# Phase 1: Terminal Runtime And Panes - Context

**Gathered:** 2026-03-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver real Windows terminal sessions with reliable horizontal and vertical pane splitting. This phase covers terminal fidelity, session launch behavior, and pane interactions. Tabs, richer pane metadata, notifications, browser surfaces, and automation stay out of scope for this phase.

</domain>

<decisions>
## Implementation Decisions

### Session boot
- Phase 1 optimizes for PowerShell first rather than trying to promise broad shell coverage immediately.
- `wcmux` should launch directly into one ready-to-use shell pane rather than showing an empty shell or a picker.
- New splits should open a fresh shell in the same current working directory as the source pane.
- Phase 1 should use one configured default shell rather than prompting for shell choice on each new pane.

### Terminal fidelity bar
- Phase 1 should feel closest to Windows Terminal in baseline behavior and reliability expectations.
- The product must feel solid for general terminal work plus AI CLIs, not only for agent demos.
- Credible support for full-screen terminal tools like `vim` and `fzf` is required in Phase 1.
- Resize bugs, copy/paste issues, and output lag under load are all unacceptable for Phase 1.

### Pane interactions
- Split behavior should use explicit horizontal and vertical actions rather than a smart/guessed split mode.
- Pane navigation should treat keyboard and mouse as equally first-class in the first usable version.
- Closing a pane should return focus to the most recently related pane rather than an arbitrary survivor.
- Pane boundaries should be visually clear, with an obvious active-pane highlight while the interaction model is still being proven.
- Mouse-driven splitting should exist through a button in the top-right area of each pane.

### Claude's Discretion
- Exact keyboard shortcut scheme for split, focus, and resize actions.
- Exact visual styling of pane borders and active-state treatment.
- Exact acceptance-test fixture list beyond the user-specified fidelity bar.
- Exact non-PowerShell shell expansion strategy after the default Phase 1 path is stable.

</decisions>

<specifics>
## Specific Ideas

- The product should feel like a real Windows terminal first, not like a terminal-shaped wrapper.
- New panes should behave like new terminal sessions that inherit context, not cloned live process state.
- The mouse split affordance belongs in the top-right area of each pane and can become the home for more pane actions later.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- None yet - the repository is effectively greenfield for Phase 1.

### Established Patterns
- None yet - planning and research artifacts exist, but there is no application code to constrain the implementation.

### Integration Points
- New Phase 1 code will establish the project's first runtime, UI shell, and session-management boundaries.

</code_context>

<deferred>
## Deferred Ideas

- Broader shell support beyond the initial PowerShell-first baseline - likely later in Phase 1 hardening or a future phase if it expands scope.
- Additional pane action buttons beyond split, including browser-opening actions - future phases only.
- Tabs, pane metadata polish, notifications, browser surfaces, and automation remain in later roadmap phases.

</deferred>

---
*Phase: 01-terminal-runtime-and-panes*
*Context gathered: 2026-03-06*
