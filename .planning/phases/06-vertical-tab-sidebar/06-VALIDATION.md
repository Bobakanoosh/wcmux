---
phase: 6
slug: vertical-tab-sidebar
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-08
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 |
| **Config file** | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBuffer OR FullyQualifiedName~AnsiStripper"` |
| **Full suite command** | `dotnet test tests/Wcmux.Tests` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBuffer OR FullyQualifiedName~AnsiStripper"`
- **After every plan wave:** Run `dotnet test tests/Wcmux.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | SIDE-02 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBuffer"` | ❌ W0 | ⬜ pending |
| 06-01-02 | 01 | 1 | SIDE-02 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AnsiStripper"` | ❌ W0 | ⬜ pending |
| 06-02-01 | 02 | 2 | SIDE-01 | manual | N/A (WinUI 3 visual layout) | N/A | ⬜ pending |
| 06-02-02 | 02 | 2 | SIDE-03 | manual | N/A (visual animation) | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` — stubs for SIDE-02 (ring buffer capacity, wrap-around, thread safety, GetRecentLines)
- [ ] `tests/Wcmux.Tests/Terminal/AnsiStripperTests.cs` — stubs for SIDE-02 (CSI, OSC, control chars, mixed content)

*Existing test infrastructure (xunit, Wcmux.Tests project) covers framework needs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Tab sidebar shows title and cwd vertically | SIDE-01 | WinUI 3 visual layout — no UI automation harness | Launch app, create tabs, verify sidebar shows title + cwd per tab |
| Attention indicators (blue dot + title color) | SIDE-03 | Visual animation behavior | Trigger bell in background tab, verify blue dot appears + 4-blink animation |
| Close button appears on hover only | SIDE-01 | Hover state interaction | Mouse over sidebar tab entries, verify X button visibility toggle |
| Right-click context menu rename | SIDE-01 | Interactive UI behavior | Right-click tab entry, verify "Rename" menu item works |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
