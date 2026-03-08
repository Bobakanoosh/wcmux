---
phase: 2
slug: tabbed-multiplexer-shell
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-07
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 |
| **Config file** | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStore" --no-build -v q` |
| **Full suite command** | `dotnet test tests/Wcmux.Tests -v q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests --no-build -v q`
- **After every plan wave:** Run `dotnet test tests/Wcmux.Tests -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | TABS-01 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CreateTab" -v q` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | TABS-02 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.SwitchTab" -v q` | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 1 | TABS-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CloseTab" -v q` | ❌ W0 | ⬜ pending |
| 02-01-04 | 01 | 1 | TABS-01 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.LastPaneClosesTab" -v q` | ❌ W0 | ⬜ pending |
| 02-01-05 | 01 | 1 | TABS-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.LastTabClosesApp" -v q` | ❌ W0 | ⬜ pending |
| 02-02-01 | 02 | 2 | SESS-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.PaneMetadata" -v q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Wcmux.Tests/Layout/TabStoreTests.cs` — stubs for TABS-01, TABS-02, TABS-03 (core state)
- [ ] Path truncation helper tests — covers SESS-03 display logic

*Existing test infrastructure (xunit, project file) already in place.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Tab switching visual state | TABS-02 | WebView2 visibility requires running app | Switch tabs, verify inactive tab content not lost |
| Pane title display | SESS-03 | Visual overlay rendering | Open pane, navigate to directory, verify title updates |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
