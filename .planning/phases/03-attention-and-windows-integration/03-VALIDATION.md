---
phase: 3
slug: attention-and-windows-integration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-08
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 |
| **Config file** | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "Category=Attention" -x` |
| **Full suite command** | `dotnet test tests/Wcmux.Tests` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests --filter "Category=Attention" -x`
- **After every plan wave:** Run `dotnet test tests/Wcmux.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | NOTF-01a | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~BellDetection"` | ❌ W0 | ⬜ pending |
| 03-01-02 | 01 | 1 | NOTF-01b | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | ❌ W0 | ⬜ pending |
| 03-01-03 | 01 | 1 | NOTF-01c | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | ❌ W0 | ⬜ pending |
| 03-01-04 | 01 | 1 | NOTF-01d | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | ❌ W0 | ⬜ pending |
| 03-01-05 | 01 | 1 | NOTF-01e | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | ❌ W0 | ⬜ pending |
| 03-01-06 | 01 | 1 | NOTF-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~BellDetection"` | ❌ W0 | ⬜ pending |
| 03-02-01 | 02 | 2 | NOTF-02 | manual | N/A | N/A | ⬜ pending |
| 03-02-02 | 02 | 2 | NOTF-02b | manual | N/A | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Wcmux.Tests/Runtime/AttentionStoreTests.cs` — stubs for NOTF-01b, NOTF-01c, NOTF-01d, NOTF-01e
- [ ] `tests/Wcmux.Tests/Terminal/BellDetectionTests.cs` — stubs for NOTF-01a, NOTF-03

*Existing infrastructure covers framework setup (xunit already in project).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Toast fires when window unfocused | NOTF-02 | Requires OS-level focus state | 1. Run app, open background pane. 2. Minimize/alt-tab away. 3. Trigger bell in background pane. 4. Verify toast appears. |
| Toast deep-link navigates to tab/pane | NOTF-02b | Requires toast click interaction | 1. Trigger toast per above. 2. Click toast notification. 3. Verify app activates and focuses correct tab/pane. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
