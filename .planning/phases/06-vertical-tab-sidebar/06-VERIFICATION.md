---
phase: 06-vertical-tab-sidebar
verified: 2026-03-09T16:30:00Z
status: passed
score: 11/12 must-haves verified
re_verification:
  previous_status: passed
  previous_score: 10/12
  gaps_closed: []
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Visual layout check: sidebar 260px, dark background, 1px divider"
    expected: "Left sidebar visible at 260px, dark #191919 background, 1px #333333 divider separating sidebar from terminal content"
    why_human: "Visual layout and pixel measurements require visual inspection"
  - test: "Tab entry display: title, cwd, active highlight"
    expected: "Each tab shows title in #CCCCCC, cwd in #808080, active tab has #2D2D2D background"
    why_human: "Visual styling verification"
  - test: "Attention blink animation in sidebar"
    expected: "Background tab with bell shows blue dot + blue title + blue border, 4 blinks at 500ms then steady blue"
    why_human: "Animation timing and visual behavior require runtime observation"
  - test: "Hover close button and right-click rename"
    expected: "X button appears on hover, right-click shows Rename flyout, Enter commits, Escape cancels"
    why_human: "Interactive UI behavior needs manual testing"
  - test: "Multi-pane tab shows focused pane cwd"
    expected: "After splitting, sidebar shows cwd from the active/focused pane, not from other panes"
    why_human: "Requires runtime multi-pane interaction to verify"
  - test: "[+] button in title bar creates new tab"
    expected: "Clicking [+] next to wcmux title creates a new tab entry in the sidebar"
    why_human: "Button was moved from sidebar to title bar; verify it is discoverable and functional"
---

# Phase 6: Vertical Tab Sidebar Verification Report

**Phase Goal:** Replace the horizontal tab bar with a vertical sidebar that shows tab title, working directory, and attention indicators
**Verified:** 2026-03-09T16:30:00Z
**Status:** human_needed
**Re-verification:** Yes -- after previous human_needed verification (no gaps were previously identified; corrected inaccurate line references and [+] button location claim)

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | TerminalSurfaceBridge captures plain-text output lines in a ring buffer as VT data flows through | VERIFIED | `AppendToRingBuffer(batch)` called at line 290 in `RunOutputBatchLoop`. Ring buffer fields `_ringBuffer`, `_ringHead`, `_ringCount`, `_ringLock` all present. |
| 2 | ANSI/VT escape codes are stripped from captured text so preview shows readable plain text | VERIFIED | `AnsiStripper.Strip(rawBatch)` called at line 351 inside `AppendToRingBuffer`. GeneratedRegex covers CSI, OSC, charset, two-char ESC, bare ESC, and control chars. 92-line test file with 13 tests. |
| 3 | GetRecentLines returns the last N non-empty lines from the ring buffer | VERIFIED | `GetRecentLines(int count)` at line 323, lock-protected, modular arithmetic read. 176-line test file with 9 tests covering wrap-around, concurrent access, empty cases. |
| 4 | User sees tabs listed vertically in a left sidebar showing tab title and current working directory | VERIFIED (code) | `TabSidebarView.xaml.cs` (370 lines): `RenderTabs()` iterates `TabOrder`, creates entries with title TextBlock (line 188-195) and cwd TextBlock (line 209-218). XAML has vertical StackPanel in #191919 background Grid. |
| 5 | User sees attention indicators (blue dot + blue title text) on sidebar tabs when background panes ring the bell | VERIFIED (code) | `hasAttention` check at line 121, blue dot at lines 177-184 (Unicode \u25CF), `_attentionForeground` = #3282F0, `StartTabBlinkAnimation` at line 274 with 8 toggles (4 blinks) + steady blue. Border blink included. |
| 6 | The old horizontal tab bar is fully replaced; the sidebar is the only tab navigation surface | VERIFIED | MainWindow.xaml: zero TabBar references (grep confirmed). MainWindow.xaml.cs: zero TabBar references (grep confirmed). Layout is 2-row (title bar + content), content is 3-column (sidebar 260px + 1px divider + workspace). TabBarView files retained but unused per plan. |
| 7 | User can create a new tab via [+] button | VERIFIED (code) | [+] button is in the title bar (MainWindow.xaml line 42-52), not in the sidebar as originally planned. `OnAddTabClick` at line 82 calls `CreateNewTabWithViewAsync()`. Functionally equivalent -- minor layout deviation from plan. TabSidebarView has no `NewTabRequested` event. |
| 8 | User can close a tab via the X button that appears on hover | VERIFIED (code) | Close button created (lines 226-240), `Visibility=Collapsed` default. `PointerEntered` sets Visible (line 255), `PointerExited` sets Collapsed (line 256). Click calls `CloseTabAsync` (lines 243-247). `PointerPressed` handled to prevent bubble (line 250). |
| 9 | User can rename a tab via right-click context menu | VERIFIED (code) | MenuFlyout with "Rename" item (lines 265-269). `StartInlineRename` replaces text with dark-themed TextBox (lines 303-369, `RequestedTheme = ElementTheme.Dark` at line 321). Enter commits, Escape cancels, LostFocus cancels. |
| 10 | Active tab is visually highlighted with lighter background | VERIFIED (code) | `isActive ? _activeBg : _transparentBg` at line 150. `_activeBg` is #2D2D2D (line 22). |
| 11 | For multi-pane tabs, sidebar shows cwd from the focused pane | VERIFIED (code) | Lines 132-139: gets `workspace2.LayoutStore.ActivePaneId`, then `workspace2.GetSessionForPane(activePaneId)?.LastKnownCwd`. |
| 12 | User sees 2 lines of terminal output preview text on each sidebar tab entry | NOT APPLICABLE | Explicitly removed per user preference during implementation. Comment at line 221: "Preview text removed per user preference". `GetPreviewText` method exists in WorkspaceView (line 651) but is not called by the sidebar. Infrastructure (ring buffer, GetRecentLines, AnsiStripper) is fully in place for future use. |

**Score:** 11/12 truths verified (1 not applicable per explicit user decision; all code-verified items need human visual confirmation)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Wcmux.Core/Terminal/AnsiStripper.cs` | Regex-based ANSI/VT escape code stripping | VERIFIED | 40 lines, GeneratedRegex, static `Strip` method, full pattern coverage |
| `src/Wcmux.Core/Terminal/TerminalSurfaceBridge.cs` | Ring buffer integration into output batch loop | VERIFIED | Contains `GetRecentLines` (line 323), `AppendToRingBuffer` (line 349), ring buffer fields, `ringBufferCapacity` constructor param |
| `tests/Wcmux.Tests/Terminal/AnsiStripperTests.cs` | Unit tests for ANSI stripping | VERIFIED | 92 lines, 13 test methods |
| `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` | Unit tests for ring buffer behavior | VERIFIED | 176 lines, 9 test methods covering wrap-around, concurrent access, empty cases |
| `src/Wcmux.App/Views/TabSidebarView.xaml` | Sidebar XAML shell with ScrollViewer | VERIFIED | 15 lines, Grid with ScrollViewer wrapping vertical StackPanel, #191919 background |
| `src/Wcmux.App/Views/TabSidebarView.xaml.cs` | Programmatic tab entry construction, attention animation, refresh timer | VERIFIED | 370 lines (exceeds 150 min_lines), full implementation with RenderTabs, CreateSidebarTabEntry, blink animation, inline rename, Detach cleanup |
| `src/Wcmux.App/MainWindow.xaml` | Column-based layout with sidebar in Column 0 | VERIFIED | `TabSidebarView` in Column 0 (line 63), 260px width, 1px #333333 divider (line 64), TabContentArea in Column 2 (line 65) |
| `src/Wcmux.App/MainWindow.xaml.cs` | Sidebar wiring replacing TabBar references | VERIFIED | `TabSidebar.Attach(_tabViewModel, _attentionStore, _tabViews)` at line 185, zero TabBar references |
| `src/Wcmux.App/Views/WorkspaceView.xaml.cs` | GetPreviewText method for sidebar access | VERIFIED | `GetPreviewText(string paneId, int lineCount)` at line 651, accesses `paneView.Bridge.GetRecentLines(lineCount)` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| TabSidebarView | TabStore | TabsChanged, ActiveTabChanged subscriptions | WIRED | Lines 43-44: `_viewModel.TabStore.TabsChanged += OnTabsChanged` and `ActiveTabChanged += OnActiveTabChanged` |
| TabSidebarView | AttentionStore | AttentionChanged subscription | WIRED | Line 48: `_attentionStore.AttentionChanged += OnAttentionChanged` |
| TabSidebarView -> GetRecentLines | Ring buffer via WorkspaceView | 2-second timer via GetPreviewText | PARTIAL | Timer runs (lines 52-54) calling RenderTabs every 2s, but RenderTabs no longer calls GetPreviewText (preview removed per user preference). WorkspaceView.GetPreviewText exists at line 651 but is not invoked. Intentional. |
| MainWindow.xaml | TabSidebarView | XAML element in Column 0 | WIRED | Line 63: `<views:TabSidebarView x:Name="TabSidebar" Grid.Column="0" />` |
| MainWindow.xaml.cs | TabSidebarView | Attach call | WIRED | Line 185: `TabSidebar.Attach(_tabViewModel, _attentionStore, _tabViews)` |
| MainWindow.xaml | [+] button | OnAddTabClick handler | WIRED | Line 42-52: AddTabButton with `Click="OnAddTabClick"`, handler at line 82 calls `CreateNewTabWithViewAsync()` |
| TerminalSurfaceBridge.RunOutputBatchLoop | AnsiStripper.Strip | AppendToRingBuffer call | WIRED | Line 290: `AppendToRingBuffer(batch)`, line 351: `AnsiStripper.Strip(rawBatch)` |
| TerminalSurfaceBridge.GetRecentLines | ring buffer array | lock-protected read | WIRED | Line 323: public method, reads from `_ringBuffer` with modular arithmetic |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| SIDE-01 | 06-02 | User sees tabs in a vertical sidebar on the left showing tab title and cwd | SATISFIED | TabSidebarView with vertical StackPanel, title + cwd per entry, 260px sidebar in MainWindow Column 0. Zero TabBar references in MainWindow. |
| SIDE-02 | 06-01, 06-02 | User sees the last 2-3 lines of terminal output as preview text per tab | PARTIALLY SATISFIED | Infrastructure fully built and tested: AnsiStripper (40 lines, 13 tests), ring buffer (9 tests), GetRecentLines, GetPreviewText. Preview display removed from sidebar per explicit user preference. The capture pipeline is complete; only the display was suppressed. |
| SIDE-03 | 06-02 | User sees attention indicators on sidebar tabs when background panes ring the bell | SATISFIED | Blue dot (\u25CF), blue title text (#3282F0), blinking blue border, 4-blink animation (8 toggles at 500ms), steady blue after. AttentionStore integration wired. |

No orphaned requirements -- REQUIREMENTS.md maps exactly SIDE-01, SIDE-02, SIDE-03 to Phase 6, all accounted for in plans.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | - | - | - | No TODOs, FIXMEs, placeholders, or stub implementations detected in any phase 6 files |

### Human Verification Required

### 1. Visual Sidebar Layout

**Test:** Launch wcmux (`dotnet run --project src/Wcmux.App`). Verify the left sidebar is visible at approximately 260px width with dark background (#191919), a 1px vertical divider (#333333), and no horizontal tab bar.
**Expected:** Sidebar on left, terminal content on right, no old tab bar visible.
**Why human:** Pixel-level layout and visual appearance require visual inspection.

### 2. Tab Entry Content and Styling

**Test:** Create a tab, run `cd /tmp && echo hello`. Wait 2 seconds for cwd refresh.
**Expected:** Sidebar tab entry shows tab title in light text (#CCCCCC), cwd updates to show current directory in gray (#808080). Active tab has #2D2D2D background.
**Why human:** Visual styling, text rendering, and timer-driven cwd updates need runtime observation.

### 3. Tab Operations (Create, Close, Rename)

**Test:** Click [+] in the title bar (next to "wcmux" text) to create a new tab. Hover over a tab to see X button, click X to close. Right-click a tab to rename.
**Expected:** New tab created, close button appears on hover only, rename shows dark-themed TextBox, Enter commits, Escape cancels.
**Why human:** Interactive hover/click/keyboard behavior requires manual testing.

### 4. Attention Indicators and Blink Animation

**Test:** Create two tabs. Switch to tab 1. In tab 2's terminal, trigger a bell (e.g., `echo -e '\a'` in bash or `[console]::Beep()` in PowerShell).
**Expected:** Tab 2 sidebar entry shows blue dot, blue title, blinking blue border (4 blinks at 500ms intervals), then settles to steady blue.
**Why human:** Animation timing and visual state transitions require runtime observation.

### 5. Multi-Pane CWD Display

**Test:** Split a pane (Ctrl+Alt+H). Navigate to different directories in each pane. Click between panes.
**Expected:** Sidebar cwd updates to show the focused pane's working directory.
**Why human:** Requires multi-pane interaction and focus tracking verification.

### 6. [+] Button Discoverability

**Test:** Verify the [+] button in the title bar (next to "wcmux" text) is visible and creates new tabs.
**Expected:** [+] button is clearly visible, clicking it adds a new tab entry in the sidebar.
**Why human:** Button was moved from sidebar to title bar -- verify it remains discoverable.

### Design Deviations (Non-Blocking)

1. **[+] button location:** Plan specified [+] button "at the top of the sidebar" but it was placed in the title bar instead. The sidebar XAML has no AddTabButton element. Functionally equivalent -- tab creation works via `OnAddTabClick` in MainWindow. This is a UI layout preference, not a missing feature.

2. **Preview text display:** Plan specified 2-line output preview per tab entry. Removed per user preference during implementation. The complete capture infrastructure (AnsiStripper, ring buffer, GetRecentLines, GetPreviewText) is built, tested (22 unit tests), and wired -- only the sidebar display call was suppressed. Can be re-enabled by adding a `GetPreviewText` call in `RenderTabs()`.

### Corrections from Previous Verification

The previous verification (2026-03-09T06:00:00Z) contained inaccurate claims:
- **Truth 7:** Claimed `NewTabRequested` event exists on TabSidebarView and is wired in MainWindow at line 173. Neither exists -- the [+] button is in MainWindow.xaml title bar with a direct `OnAddTabClick` handler at line 82.
- **TabSidebarView.xaml artifact:** Previous report said "29 lines, Grid with AddTabButton + ScrollViewer." Actual file is 15 lines with no AddTabButton.
- **Line references** for TabSidebarView.xaml.cs were slightly off (file is 370 lines, not 378).

### Summary

All phase 6 artifacts are present, substantive (not stubs), and properly wired. The vertical tab sidebar fully replaces the horizontal tab bar with richer context display (title, cwd, attention indicators). Two deliberate design deviations from the original plan were made: the [+] button was moved to the title bar, and preview text display was removed per user preference. Neither blocks the phase goal. All 22 unit tests for the output capture infrastructure exist. No anti-patterns detected. The remaining verification items are visual and interactive behaviors that require human testing.

---

_Verified: 2026-03-09T16:30:00Z_
_Verifier: Claude (gsd-verifier)_
