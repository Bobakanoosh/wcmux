---
phase: 7
slug: pane-interaction
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 7 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 |
| **Config file** | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~Layout" --no-build -v q` |
| **Full suite command** | `dotnet test tests/Wcmux.Tests -v q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~Layout" --no-build -v q`
- **After every plan wave:** Run `dotnet test tests/Wcmux.Tests -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | PINT-01 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~SetSplitRatio" -v q` | ❌ W0 | ⬜ pending |
| 07-01-02 | 01 | 1 | PINT-02 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~SwapPanes" -v q` | ❌ W0 | ⬜ pending |
| 07-01-03 | 01 | 1 | PINT-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~MovePaneToTarget" -v q` | ❌ W0 | ⬜ pending |
| 07-02-01 | 02 | 1 | PINT-01 | manual | Visual: drag border, observe resize | N/A | ⬜ pending |
| 07-02-02 | 02 | 1 | PINT-02 | manual | Visual: Ctrl+Alt+Shift+Arrow swaps panes | N/A | ⬜ pending |
| 07-02-03 | 02 | 2 | PINT-03 | manual | Visual: drag title bar, observe blue overlay and rearrange | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Wcmux.Tests/Layout/PaneInteractionTests.cs` — stubs for PINT-01 (SetSplitRatio), PINT-02 (SwapPanes), PINT-03 (MovePaneToTarget)
- [ ] No new framework install needed — xUnit already configured

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Mouse resize visual feedback | PINT-01 | Requires visual verification of cursor changes and smooth resize | Drag pane border, verify resize cursor appears and pane resizes fluidly |
| Blue preview overlay during drag | PINT-03 | Requires visual verification of overlay positioning and direction indication | Drag pane title bar over another pane, verify blue directional overlay appears |
| Keyboard swap visual confirmation | PINT-02 | Requires visual verification that pane content moves to expected position | Press Ctrl+Alt+Shift+Arrow, verify panes swap visually |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
