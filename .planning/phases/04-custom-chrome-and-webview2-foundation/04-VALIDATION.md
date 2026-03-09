---
phase: 4
slug: custom-chrome-and-webview2-foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-08
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 |
| **Config file** | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| **Quick run command** | `dotnet test tests/Wcmux.Tests --filter "Category!=Integration" -v q` |
| **Full suite command** | `dotnet test tests/Wcmux.Tests -v q` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Wcmux.Tests --filter "Category!=Integration" -v q`
- **After every plan wave:** Run `dotnet test tests/Wcmux.Tests -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 0 | CHRM-02 | unit | `dotnet test tests/Wcmux.Tests --filter "WebViewEnvironmentCache" -v q` | No — W0 | ⬜ pending |
| 04-01-02 | 01 | 1 | CHRM-01 | manual-only | N/A (visual, requires window) | N/A | ⬜ pending |
| 04-01-03 | 01 | 1 | CHRM-01 | manual-only | N/A (drag/maximize interaction) | N/A | ⬜ pending |
| 04-01-04 | 01 | 1 | CHRM-01 | manual-only | N/A (snap layouts, Windows 11 shell) | N/A | ⬜ pending |
| 04-02-01 | 02 | 1 | CHRM-02 | unit | `dotnet test tests/Wcmux.Tests --filter "WebViewTerminalController" -v q` | No — W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Wcmux.Tests/Terminal/WebViewEnvironmentCacheTests.cs` — stubs for CHRM-02 singleton behavior (thread safety, consistent return)
- [ ] Manual test checklist document for CHRM-01 visual/interaction verification

*Note: Testing `WebViewEnvironmentCache` as a pure unit test is limited because `CoreWebView2Environment.CreateAsync()` requires a real WebView2 runtime. Tests verify synchronization logic and caching pattern rather than actual environment creation.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Custom dark title bar visible with window controls | CHRM-01 | Visual rendering requires running WinUI 3 window | Launch app, verify dark title bar with minimize/maximize/close buttons |
| Drag to move and double-click maximize/restore | CHRM-01 | Requires window interaction with shell | Drag title bar to move; double-click to maximize then restore |
| Snap layouts on maximize hover | CHRM-01 | Requires Windows 11 shell integration | Hover maximize button, verify snap layout flyout appears |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
