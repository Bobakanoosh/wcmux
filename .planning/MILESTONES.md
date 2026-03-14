# Milestones

## v1.1 UI/UX Overhaul (Shipped: 2026-03-14)

**Phases completed:** 5 phases (4–8), 9 plans
**Files changed:** 69 files (+9,063 / -449 lines)
**Timeline:** 2026-03-08 → 2026-03-14 (6 days)

**Key accomplishments:**
- Custom dark title bar with InputNonClientPointerSource replaces default white Windows chrome (CHRM-01)
- Single shared CoreWebView2Environment singleton reduces memory overhead across all panes (CHRM-02)
- Per-pane title bars show live foreground process name via ToolHelp32 process tree walking (PBAR-01/02/03)
- Browser pane hosting with address bar, navigation, and shared WebView2 environment (PBAR-04)
- Vertical tab sidebar replaces horizontal tab bar with title, cwd, and attention blink animation (SIDE-01/03)
- Mouse drag resize (EW/NS cursors), keyboard pane swap (Ctrl+Alt+Shift+Arrow), drag-to-rearrange with blue directional preview overlay (PINT-01/02/03)
- Tech debt closed: ring buffer CPU gated by PreviewEnabled flag, resize handle pixel fix, TabBarView and dead code removed (Phase 8)

**Archive:** `.planning/milestones/v1.1-ROADMAP.md`, `.planning/milestones/v1.1-REQUIREMENTS.md`

---

## v1.0 MVP (Shipped: 2026-03-08)

**Phases completed:** 3 phases (1–3), 7 plans

**Key accomplishments:**
- Real Windows terminal sessions with ConPTY-backed hosting and full TUI fidelity
- Horizontal and vertical pane splitting with keyboard focus and resize controls
- Tabbed workspace with independent pane layouts per tab
- Windows toast notifications and in-app attention indicators for background session bell events

**Archive:** `.planning/milestones/` (see v1.0 files)

---
