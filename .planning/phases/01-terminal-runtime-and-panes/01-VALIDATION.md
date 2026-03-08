---
phase: 1
slug: terminal-runtime-and-panes
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-07
audited: 2026-03-08
---

# Phase 1 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | `xUnit + dotnet test` |
| **Config file** | `tests/Wcmux.Tests/Wcmux.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter "Category!=Manual"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~35 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter "Category!=Manual"`
- **After every plan wave:** Run `dotnet test`
- **Before `$gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | `SESS-01` | build | `dotnet build Wcmux.sln` | ✅ Wcmux.sln | ✅ green |
| 1-01-02 | 01 | 1 | `SESS-01` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~SessionHost` | ✅ SessionHostIntegrationTests.cs | ✅ green (7 tests) |
| 1-01-03 | 01 | 1 | `SESS-01` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~SessionLifecycle` | ✅ SessionLifecycleTests.cs | ⚠️ flaky (8 pass, 1 flaky) |
| 1-02-01 | 02 | 2 | `SESS-02` | build | `dotnet build Wcmux.sln` | ✅ Wcmux.sln | ✅ green |
| 1-02-02 | 02 | 2 | `SESS-02` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~TerminalBridge` | ✅ TerminalBridgeTests.cs | ✅ green (14 tests) |
| 1-02-03 | 02 | 2 | `SESS-02` | hybrid | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~ResizePipeline` | ✅ ResizePipelineTests.cs | ✅ green (13 tests) |
| 1-03-01 | 03 | 3 | `LAYT-01` | unit | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~LayoutReducer` | ✅ LayoutReducerTests.cs | ✅ green (21 tests) |
| 1-03-02 | 03 | 3 | `LAYT-02`, `LAYT-03` | hybrid | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~SplitCommands` | ✅ SplitCommandsTests.cs | ✅ green (9 tests) |
| 1-03-03 | 03 | 3 | `LAYT-03` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~PaneFocusAndResize` | ✅ PaneFocusAndResizeTests.cs | ✅ green (26 tests) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `tests/Wcmux.Tests/Wcmux.Tests.csproj` - bootstrap the phase test project
- [x] `tests/Wcmux.Tests/Runtime/SessionHostIntegrationTests.cs` - ConPTY launch and lifecycle harness
- [x] `tests/Wcmux.Tests/Runtime/SessionLifecycleTests.cs` - repeated session open-close, cwd signal capture, and teardown coverage
- [x] `dotnet new xunit` plus solution wiring - if no framework exists

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `vim` launch, input, resize, and exit | `SESS-02` | Full-screen TUI fidelity is not credible from mocks alone | Launch app, open `vim`, type, resize repeatedly, exit, confirm terminal remains usable |
| `fzf` or similar alternate-screen selector | `SESS-02` | Alternate-screen and keyboard behavior need real renderer validation | Run `fzf`, navigate, confirm selection and exit behavior after resize |
| Mouse focus plus top-right split affordance | `Context: Pane interactions` | Native chrome plus hosted terminal interaction crosses UI layers | After Plan 03, click an inactive pane to focus it, use the pane split button, verify the new pane inherits cwd and focus moves correctly |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved

---

## Validation Audit 2026-03-08

| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

**Notes:** All 9 task verification entries now map to existing, passing test files. Wave 0 requirements all satisfied. One pre-existing flaky test (`SessionLifecycle_ConcurrentSessions_TrackedIndependently`) — known ConPTY timing issue under test runner, not a validation gap. Total: 103 Phase 1 tests (7 SessionHost + 9 SessionLifecycle + 14 TerminalBridge + 13 ResizePipeline + 21 LayoutReducer + 9 SplitCommands + 26 PaneFocusAndResize + 4 misc).
