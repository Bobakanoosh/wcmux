---
phase: 5
slug: pane-title-bars-and-browser-panes
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-08
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 |
| **Config file** | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "Category=Unit" -v q` |
| **Full suite command** | `dotnet test tests/Wcmux.Tests -v q` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests --filter "Category=Unit" -v q`
- **After every plan wave:** Run `dotnet test tests/Wcmux.Tests -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | PBAR-01 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~ForegroundProcess" -v q` | ❌ W0 | ⬜ pending |
| 05-01-02 | 01 | 1 | PBAR-01 | manual | N/A - visual verification | N/A | ⬜ pending |
| 05-01-03 | 01 | 1 | PBAR-02 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~LayoutReducerTests" -v q` | ✅ | ⬜ pending |
| 05-01-04 | 01 | 1 | PBAR-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~SplitCommands" -v q` | ✅ | ⬜ pending |
| 05-02-01 | 02 | 1 | PBAR-04 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~PaneKind" -v q` | ❌ W0 | ⬜ pending |
| 05-02-02 | 02 | 1 | PBAR-04 | manual | N/A - requires WebView2 runtime | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Wcmux.Tests/Runtime/ForegroundProcessDetectorTests.cs` — stubs for PBAR-01 (process tree walking logic)
- [ ] `tests/Wcmux.Tests/Layout/PaneKindTests.cs` — stubs for PBAR-04 (PaneKind preserved through layout operations)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Pane title bar displays process name | PBAR-01 | Visual rendering requires WinUI 3 runtime | Launch app, open terminal, run `python` — title bar should show "python" |
| Close button removes pane | PBAR-02 | UI interaction requires WinUI 3 runtime | Click X button on pane title bar — pane should close |
| Split buttons create new panes | PBAR-03 | UI interaction requires WinUI 3 runtime | Click split-h/split-v buttons — new pane should appear |
| Browser pane renders web content | PBAR-04 | Requires WebView2 runtime + network | Click browser button, type URL, verify page renders |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
