---
phase: 2
slug: tabbed-multiplexer-shell
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-07
audited: 2026-03-08
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
| 02-01-01 | 01 | 1 | TABS-01 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CreateTab" -v q` | ✅ TabStoreTests.cs | ✅ green (6 tests) |
| 02-01-02 | 01 | 1 | TABS-02 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.SwitchTab" -v q` | ✅ TabStoreTests.cs | ✅ green (5 tests) |
| 02-01-03 | 01 | 1 | TABS-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CloseTab" -v q` | ✅ TabStoreTests.cs | ✅ green (8 tests) |
| 02-01-04 | 01 | 1 | TABS-01 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CloseTab_LastTab" -v q` | ✅ TabStoreTests.cs | ✅ green (2 tests) |
| 02-01-05 | 01 | 1 | TABS-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CloseTab_LastTab_FiresLastTabClosed" -v q` | ✅ TabStoreTests.cs | ✅ green |
| 02-02-01 | 02 | 2 | SESS-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~PathHelperTests" -v q` | ✅ PathHelperTests.cs | ✅ green (13 tests) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `tests/Wcmux.Tests/Layout/TabStoreTests.cs` — 27 tests for TABS-01, TABS-02, TABS-03 (core state)
- [x] `tests/Wcmux.Tests/Layout/PathHelperTests.cs` — 13 tests covering SESS-03 display logic

*All Wave 0 tests created during TDD execution of Plan 01.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Tab switching visual state | TABS-02 | WebView2 visibility requires running app | Switch tabs, verify inactive tab content not lost |
| Pane title display | SESS-03 | Visual overlay rendering | Open pane, navigate to directory, verify title updates |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved

---

## Validation Audit 2026-03-08

| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

**Notes:** VALIDATION.md was created pre-execution with Wave 0 stubs. Plan 01 TDD execution created all 40 tests (27 TabStore + 13 PathHelper). All 6 task verification entries now map to existing, passing tests. CWD change event coverage also provided by 4 TerminalBridgeTests in Phase 1 test infrastructure.
