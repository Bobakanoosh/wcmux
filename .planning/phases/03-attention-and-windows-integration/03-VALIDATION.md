---
phase: 3
slug: attention-and-windows-integration
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-08
audited: 2026-03-08
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 |
| **Config file** | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore|FullyQualifiedName~BellDetection" -v q` |
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
| 03-01-01 | 01 | 1 | NOTF-01a | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~BellDetection" -v q` | ✅ BellDetectionTests.cs | ✅ green (3 tests) |
| 03-01-02 | 01 | 1 | NOTF-01b | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStoreTests.RaiseBell" -v q` | ✅ AttentionStoreTests.cs | ✅ green (4 tests) |
| 03-01-03 | 01 | 1 | NOTF-01c | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStoreTests.RaiseBell_WithinCooldown|FullyQualifiedName~AttentionStoreTests.RaiseBell_FocusedPane" -v q` | ✅ AttentionStoreTests.cs | ✅ green (3 tests) |
| 03-01-04 | 01 | 1 | NOTF-01d | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStoreTests.ClearAttention" -v q` | ✅ AttentionStoreTests.cs | ✅ green (2 tests) |
| 03-01-05 | 01 | 1 | NOTF-01e | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStoreTests.TabHasAttention" -v q` | ✅ AttentionStoreTests.cs | ✅ green (2 tests) |
| 03-01-06 | 01 | 1 | NOTF-03 | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~BellDetection" -v q` | ✅ BellDetectionTests.cs | ✅ green (generic bell, not tool-specific) |
| 03-02-01 | 02 | 2 | NOTF-02 | manual | N/A | N/A | ✅ manual-only |
| 03-02-02 | 02 | 2 | NOTF-02b | manual | N/A | N/A | ✅ manual-only |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `tests/Wcmux.Tests/Runtime/AttentionStoreTests.cs` — 13 tests for NOTF-01b/c/d/e (attention state)
- [x] `tests/Wcmux.Tests/Terminal/BellDetectionTests.cs` — 3 tests for NOTF-01a, NOTF-03 (bell detection)

*All Wave 0 tests created during TDD execution of Plan 01.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Toast fires when window unfocused | NOTF-02 | Requires OS-level focus state | 1. Run app, open background pane. 2. Minimize/alt-tab away. 3. Trigger bell in background pane. 4. Verify toast appears. |
| Toast deep-link navigates to tab/pane | NOTF-02b | Requires toast click interaction | 1. Trigger toast per above. 2. Click toast notification. 3. Verify app activates and focuses correct tab/pane. |
| Taskbar flashing on bell | NOTF-02 | Requires OS-level FlashWindowEx | 1. Alt-tab away from app. 2. Trigger bell. 3. Verify taskbar icon flashes. |

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

**Notes:** VALIDATION.md was created pre-execution with Wave 0 stubs. Plan 01 TDD execution created all 16 tests (13 AttentionStore + 3 BellDetection). Plan 02 (toast notifications, taskbar flash, deep-link) is correctly manual-only — these require OS-level window focus and toast click interaction that cannot be unit tested. All 6 automated task entries now map to existing, passing tests.
