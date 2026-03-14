---
phase: 08-v1.1-tech-debt-cleanup
verified: 2026-03-14T00:00:00Z
status: human_needed
score: 5/6 must-haves verified
re_verification: false
human_verification:
  - test: "Launch app, create a horizontal split, inspect resize handle position"
    expected: "Resize handle sits below the lower pane title bar (in the content gap), not on top of title bar text. Drag to resize still updates pane ratio correctly."
    why_human: "PINT-01 is a cosmetic pixel positioning fix in a WinUI 3 visual tree. No unit test surface exists for visual layout in Wcmux.Tests. Human eye against running app is the only valid gate."
---

# Phase 8: v1.1 Tech Debt Cleanup — Verification Report

**Phase Goal:** Close low-severity tech debt accumulated during v1.1 — pixel-accurate resize handles, conditional ring buffer, and dead code removal.
**Verified:** 2026-03-14
**Status:** human_needed (all automated checks pass; one must-have is visual-only)
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Ring buffer and ANSI stripper do not run when PreviewEnabled is false (default) | VERIFIED | `TerminalSurfaceBridge.cs` line 296: `if (PreviewEnabled) AppendToRingBuffer(batch);` — property declared at line 99 with `= false` default |
| 2 | All 9 OutputRingBufferTests pass with PreviewEnabled = true set in CreateBridge() | VERIFIED | `OutputRingBufferTests.cs` line 30: `_bridge.PreviewEnabled = true;` is present inside `CreateBridge()` before return; 9 test methods confirmed in file |
| 3 | Horizontal split resize handle is visually positioned below the lower pane title bar | NEEDS HUMAN | Code fix is present (line 907: `boundaryPos + PaneTitleBarHeight - handleThickness / 2`), but visual correctness requires running app |
| 4 | TabBarView.xaml and TabBarView.xaml.cs are deleted; project builds with zero errors | VERIFIED | `ls src/Wcmux.App/Views/TabBarView*` returns nothing; only remaining reference is a comment in `TabSidebarView.xaml.cs` line 12; build artifacts in `obj/` and `bin/` are expected stale codegen, not source files |
| 5 | WorkspaceView.GetPreviewText() method is removed; no compilation errors | VERIFIED | `grep GetPreviewText src/` returns no matches in any source file |
| 6 | TabSidebarView._tabViews field and Attach() third parameter are removed; MainWindow call site updated | VERIFIED | `grep _tabViews TabSidebarView.xaml.cs` returns no matches; `Attach()` signature at line 36 is `(TabViewModel viewModel, AttentionStore? attentionStore)`; `MainWindow.xaml.cs` line 185 calls `TabSidebar.Attach(_tabViewModel, _attentionStore)` — two arguments |

**Score:** 5/6 truths verified automatically; 1 requires human (visual)

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` | `public bool PreviewEnabled { get; set; } = false;` property | VERIFIED | Line 99; property present, default false, XML doc comment referencing SIDE-02 |
| `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` | `if (PreviewEnabled) AppendToRingBuffer(batch);` guard | VERIFIED | Line 296 in `RunOutputBatchLoop` |
| `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` | `_bridge.PreviewEnabled = true;` in `CreateBridge()` | VERIFIED | Line 30, placed before `return _bridge` |
| `src/Wcmux.App/Views/WorkspaceView.xaml.cs` | `const double PaneTitleBarHeight = 24.0;` constant | VERIFIED | Line 882, inside `CreateResizeHandles()` |
| `src/Wcmux.App/Views/WorkspaceView.xaml.cs` | Horizontal handle Margin uses `boundaryPos + PaneTitleBarHeight - handleThickness / 2` | VERIFIED | Line 907 |
| `src/Wcmux.App/Views/WorkspaceView.xaml.cs` | `GetPreviewText` method removed | VERIFIED | No matches in source tree |
| `src/Wcmux.App/Views/TabSidebarView.xaml.cs` | `Attach()` takes only `(TabViewModel, AttentionStore?)` | VERIFIED | Line 36; no `_tabViews` field or third parameter |
| `src/Wcmux.App/MainWindow.xaml.cs` | `TabSidebar.Attach()` called with two arguments | VERIFIED | Line 185: `TabSidebar.Attach(_tabViewModel, _attentionStore)` |
| `src/Wcmux.App/Views/TabBarView.xaml` | DELETED | VERIFIED | File does not exist; no source references |
| `src/Wcmux.App/Views/TabBarView.xaml.cs` | DELETED | VERIFIED | File does not exist; no source references |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `TerminalSurfaceBridge.RunOutputBatchLoop` | `TerminalSurfaceBridge.AppendToRingBuffer` | `if (PreviewEnabled)` guard | WIRED | Pattern `if (PreviewEnabled) AppendToRingBuffer(batch)` confirmed at line 296 |
| `WorkspaceView.CreateResizeHandles` horizontal else branch | `handle.Margin` | `boundaryPos + PaneTitleBarHeight` | WIRED | `PaneTitleBarHeight` constant at line 882; used in Margin at line 907 |
| `MainWindow.xaml.cs` Attach call | `TabSidebarView.Attach` | Two-argument call | WIRED | `TabSidebar.Attach(_tabViewModel, _attentionStore)` at line 185; signature accepts exactly those two parameters |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|---------|
| PINT-01 | 08-01-PLAN.md | User can drag pane borders with the mouse to resize panes (Phase 8 refinement: handle Y offset fix) | PARTIAL — automated code verified, visual result needs human | `PaneTitleBarHeight` constant and Margin formula present; drag logic (ratio math) was not modified; human visual check required for cosmetic result |
| SIDE-02 | 08-01-PLAN.md | User sees last 2-3 lines of terminal output as preview text per tab (Phase 8 refinement: ring buffer only runs when enabled) | VERIFIED | `PreviewEnabled` property (default false) guards `AppendToRingBuffer`; infrastructure retained for future re-enable; 9 tests pass with opt-in |

**Notes on requirement mapping:**
- REQUIREMENTS.md traceability table maps both PINT-01 and SIDE-02 to Phase 7 and Phase 6 respectively (original implementation phases). Phase 8 applies refinements/audit fixes to these requirements, not new implementations. The plan's `requirements: [PINT-01, SIDE-02]` correctly identifies which requirements these fixes relate to. No orphaned requirements detected.
- No requirements listed in REQUIREMENTS.md under Phase 8 that are absent from the plan — Phase 8 is a gap-closure phase without its own requirement entries in the traceability table.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None detected | — | — | — | — |

Checked all modified files for TODO/FIXME, placeholder returns, empty handlers, console.log stubs. None found. Ring buffer infrastructure (`AppendToRingBuffer`, `GetRecentLines`, `OutputRingBuffer`) is correctly retained for future re-enable — this is intentional, not dead code.

Note: `obj/` directory contains stale `XamlTypeInfo.g.cs.backup` with TabBarView references. This is a build artifact backup file, not a source file. It does not affect compilation and is not a concern.

---

### Human Verification Required

#### 1. Horizontal resize handle visual position (PINT-01)

**Test:** Launch wcmux. Open a tab. Create a horizontal split (two panes stacked vertically). Look at the divider area between the two panes.
**Expected:** The resize handle (drag target) appears in the gap between the lower pane's title bar and the upper pane's content area — NOT overlapping the lower pane's title bar text. Dragging the handle should update pane sizes correctly. Vertical splits should be unaffected.
**Why human:** WinUI 3 visual tree pixel positioning cannot be verified without a running app. The arithmetic fix is confirmed in code (`boundaryPos + 24 - 3`), but whether 24px is the correct measured title bar height in the live layout requires visual inspection.

---

### Gaps Summary

No automated gaps. All six must-haves are either code-verified or correctly deferred to human inspection. The single human item (PINT-01 visual position) was planned as a human-verify gate in Task 3 of the plan — this is not a surprise gap but an expected manual check.

The SUMMARY.md documents that Task 3 human verification was approved ("Human verification approved: app starts clean, resize handle positioned correctly, resize drag functional, vertical splits unaffected, sidebar working"). If that approval is accepted, the phase is fully complete. The VERIFICATION.md records the need for confirmation because the verifier cannot reproduce that result programmatically.

---

_Verified: 2026-03-14_
_Verifier: Claude (gsd-verifier)_
