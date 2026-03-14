# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

---

## Milestone: v1.1 — UI/UX Overhaul

**Shipped:** 2026-03-14
**Phases:** 5 (4-8) | **Plans:** 9 | **Timeline:** 6 days (2026-03-08 → 2026-03-14)

### What Was Built

- Custom dark WinUI 3 title bar with InputNonClientPointerSource replacing default Windows chrome
- Shared CoreWebView2Environment singleton across all panes (memory optimization)
- Per-pane title bars with live ToolHelp32-based process name detection, and close/split/browser action buttons
- Browser pane hosting (WebView2 with address bar, navigation, shared environment)
- Vertical tab sidebar with title, cwd, and bell-based attention blink animation replacing horizontal tab bar
- Mouse drag pane resize (EW/NS cursors), Ctrl+Alt+Shift+Arrow keyboard swap, drag-to-rearrange with blue directional overlay
- Phase 8 tech debt: ring buffer CPU gated by PreviewEnabled flag, resize handle pixel fix, TabBarView and dead code deleted

### What Worked

- **Pure reducer pattern payoff:** Adding SetSplitRatio, SwapPanes, MovePaneToTarget in Phase 7 was straightforward because the layout tree was already pure functions. Zero regressions from the Phase 1 architecture decision.
- **Store pattern observable state:** AttentionStore, LayoutStore, and TabStore subscription chains made Phase 6 sidebar wiring clean — just subscribe to existing events, no new plumbing.
- **WinUI 3 + ConPTY stack proved solid:** No fundamental architectural issues surfaced during 5 phases of UI work on top of the v1.0 foundation.
- **Phase 8 gap closure:** Explicitly planning a tech debt cleanup phase worked — known issues from the audit were closed systematically rather than accumulating.

### What Was Inefficient

- **SIDE-02 preview display removed mid-phase:** The ring buffer infrastructure was built in Phase 6-01, then the display was removed during 06-02 implementation per user preference. 22 unit tests now cover infrastructure that isn't exercised in production. Could have scoped Phase 6 more narrowly if the preference was known earlier.
- **ROADMAP checkbox inconsistencies:** Phase 5 and 6 plan checkboxes in ROADMAP.md weren't updated during execution (stayed `[ ]`). Minor but caused confusion in audit.
- **Phase 6 VERIFICATION.md status discrepancy:** Frontmatter said `passed` but body said `human_needed` after re-verification — not caught before audit.
- **Nyquist VALIDATION.md files left in draft:** All v1.1 phase VALIDATION.md files ended in `nyquist_compliant: false` status. Should run `/gsd:validate-phase` earlier in the milestone lifecycle.

### Patterns Established

- **`RequestedTheme = ElementTheme.Dark` on all programmatic controls** — WinUI 3 doesn't inherit dark theme automatically for dynamically-created controls. Documented in CLAUDE.md.
- **InputNonClientPointerSource for custom title bar** — more robust than SetTitleBar for interactive controls in the non-client area.
- **Sentinel session ID pattern for non-terminal panes** — `"browser:{uuid}"` prefix enables clean PaneKind dispatch without requiring special-case logic in session management.
- **Phase 8 gap-closure phase as explicit milestone deliverable** — auditing before archiving and closing gaps in a named phase keeps tech debt visible and actionable.

### Key Lessons

1. **Plan for user preference discovery during UI phases.** SIDE-02 built full capture infrastructure before the user decided the display wasn't wanted. For UI features, consider a minimal prototype step before full infrastructure investment.
2. **Nyquist validation should be part of phase execution, not deferred.** All v1.1 VALIDATION.md files ended up in draft state. Run `/gsd:validate-phase` at phase completion, not just at milestone audit time.
3. **ROADMAP.md plan checkboxes need a clear update convention.** Several plan checkboxes stayed `[ ]` even after plans completed. A post-execution checklist step should include ROADMAP checkbox updates.
4. **The pure reducer architecture scales well.** Five phases of UI work added complexity without touching the layout foundation. This pattern should be protected in v2.0 work.

### Cost Observations

- Model mix: primarily Sonnet 4.6 (orchestrator + execution); Sonnet used for integration checker
- Sessions: ~8 sessions across 6 days
- Notable: Phase 7 UI work (mouse drag, keyboard accelerators, drag-to-rearrange) required the most iteration — WinUI 3 pointer event handling has subtle ordering constraints

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.0 | 3 | 7 | Established ConPTY + WebView2 + store pattern foundation |
| v1.1 | 5 | 9 | UI overhaul on stable foundation; added gap closure phase (Phase 8) |

### Cumulative Quality

| Milestone | Automated Tests | Notes |
|-----------|----------------|-------|
| v1.0 | ~94 tests | Runtime, layout, bridge |
| v1.1 | ~190 tests | Added attention, pane interaction, ANSI stripping, ring buffer, env cache |

### Top Lessons (Verified Across Milestones)

1. **Pure reducer layout transitions are the right call for a multiplexer.** Tested both at v1.0 (split/focus/resize) and v1.1 (ratio/swap/move) — pure functions make the complex layout math testable and composable.
2. **Tech debt accumulates predictably in UI phases.** Both milestones ended with a gap or cleanup task. Building in an explicit cleanup phase (Phase 8 for v1.1) is better than deferring indefinitely.
