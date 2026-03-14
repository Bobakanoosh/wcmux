---
phase: 8
slug: v1.1-tech-debt-cleanup
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-14
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 |
| **Config file** | `tests/Wcmux.Tests/Wcmux.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBufferTests" -v q` |
| **Full suite command** | `dotnet test tests/Wcmux.Tests -v q` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests -v q` + `dotnet build src/Wcmux.App -v q`
- **After every plan wave:** Run full suite + build (single-wave phase)
- **Before `/gsd:verify-work`:** Full suite must be green + build clean
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 8-01-01 | 01 | 1 | PINT-01 | manual | N/A — visual inspection | N/A | ⬜ pending |
| 8-01-02 | 01 | 1 | SIDE-02 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBufferTests" -v q` | ✅ | ⬜ pending |
| 8-01-03 | 01 | 1 | — (dead code) | build | `dotnet build src/Wcmux.App -v q` | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

`OutputRingBufferTests.cs` exists with 9 passing tests. The only change required is adding `_bridge.PreviewEnabled = true` in the `CreateBridge()` helper to keep those tests passing after the ring buffer guard is introduced. This is part of task 8-01-02, not a separate Wave 0 step.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Horizontal split resize handle does not overlap lower pane title bar | PINT-01 | WinUI 3 visual tree; no unit test surface in Wcmux.Tests | 1. Launch app. 2. Open a tab, split horizontally. 3. Verify the resize handle appears in the gap between panes, not overlapping the lower pane's title bar. 4. Drag to resize — verify ratio updates correctly. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
