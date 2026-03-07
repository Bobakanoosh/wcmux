---
phase: 1
slug: terminal-runtime-and-panes
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-03-07
---

# Phase 1 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | `xUnit + dotnet test` |
| **Config file** | `none - Wave 0 installs` |
| **Quick run command** | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter "Category!=Manual"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~90 seconds |

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
| 1-01-01 | 01 | 1 | `SESS-01` | build | `dotnet build Wcmux.sln` | no - Wave 0 | pending |
| 1-01-02 | 01 | 1 | `SESS-01` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~SessionHost` | no - Wave 0 | pending |
| 1-01-03 | 01 | 1 | `SESS-01` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~SessionLifecycle` | no - Wave 0 | pending |
| 1-02-01 | 02 | 2 | `SESS-02` | build | `dotnet build Wcmux.sln` | no - later wave | pending |
| 1-02-02 | 02 | 2 | `SESS-02` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~TerminalBridge` | no - later wave | pending |
| 1-02-03 | 02 | 2 | `SESS-02` | hybrid | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~ResizePipeline` | no - later wave | pending |
| 1-03-01 | 03 | 3 | `LAYT-01` | unit | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~LayoutReducer` | no - later wave | pending |
| 1-03-02 | 03 | 3 | `LAYT-02`, `LAYT-03` | hybrid | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~SplitCommands` | no - later wave | pending |
| 1-03-03 | 03 | 3 | `LAYT-03` | integration | `dotnet test tests/Wcmux.Tests/Wcmux.Tests.csproj --filter FullyQualifiedName~PaneFocusAndResize` | no - later wave | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `tests/Wcmux.Tests/Wcmux.Tests.csproj` - bootstrap the phase test project
- [ ] `tests/Wcmux.Tests/Runtime/SessionHostIntegrationTests.cs` - ConPTY launch and lifecycle harness
- [ ] `tests/Wcmux.Tests/Runtime/SessionLifecycleTests.cs` - repeated session open-close, cwd signal capture, and teardown coverage
- [ ] `dotnet new xunit` plus solution wiring - if no framework exists

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `vim` launch, input, resize, and exit | `SESS-02` | Full-screen TUI fidelity is not credible from mocks alone | Launch app, open `vim`, type, resize repeatedly, exit, confirm terminal remains usable |
| `fzf` or similar alternate-screen selector | `SESS-02` | Alternate-screen and keyboard behavior need real renderer validation | Run `fzf`, navigate, confirm selection and exit behavior after resize |
| Mouse focus plus top-right split affordance | `Context: Pane interactions` | Native chrome plus hosted terminal interaction crosses UI layers | After Plan 03, click an inactive pane to focus it, use the pane split button, verify the new pane inherits cwd and focus moves correctly |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
