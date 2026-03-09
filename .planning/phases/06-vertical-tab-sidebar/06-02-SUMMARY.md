---
phase: 06-vertical-tab-sidebar
plan: 02
subsystem: ui
tags: [winui3, sidebar, tabs, webview2, attention-indicators]

requires:
  - phase: 06-01
    provides: AnsiStripper and ring buffer for output capture

provides:
  - Vertical tab sidebar replacing horizontal tab bar
  - Tab entries with title, cwd, and attention indicators
  - Blinking blue dot, title, and border for attention state
  - Right-click rename with dark-themed input
  - Close button on hover

affects: [07-polish]

tech-stack:
  added: []
  patterns: [programmatic-ui-construction, attention-blink-animation, dark-theme-default]

key-files:
  created:
    - src/Wcmux.App/Views/TabSidebarView.xaml
    - src/Wcmux.App/Views/TabSidebarView.xaml.cs
    - CLAUDE.md
  modified:
    - src/Wcmux.App/MainWindow.xaml
    - src/Wcmux.App/MainWindow.xaml.cs
    - src/Wcmux.App/Views/WorkspaceView.xaml.cs

key-decisions:
  - "Removed output preview text from sidebar tab entries per user preference"
  - "All UI elements must use dark theme (RequestedTheme=Dark) - documented in CLAUDE.md"
  - "Rename commits only on Enter, cancels on Escape or focus loss"
  - "Tab ghosting bug deferred - pre-existing WebView2 shared environment issue"
  - "Attention indicator includes blinking blue border around entire tab entry"

patterns-established:
  - "Dark theme default: all programmatic UI controls must set RequestedTheme=Dark"
  - "Attention border: blinking blue border wraps tab entries alongside dot/title indicators"

requirements-completed: [SIDE-01, SIDE-02, SIDE-03]

duration: 4min
completed: 2026-03-09
---

# Phase 6 Plan 2: Vertical Tab Sidebar Summary

**Vertical tab sidebar with title/cwd display, attention indicators (blinking blue dot + title + border), dark-themed rename, and hover-close buttons replacing horizontal tab bar**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-09T05:20:19Z
- **Completed:** 2026-03-09T05:24:28Z
- **Tasks:** 2 (1 implementation + 1 checkpoint with 5 post-verification fixes)
- **Files modified:** 6

## Accomplishments

- Created TabSidebarView with vertical tab entries showing title and current working directory
- Replaced horizontal TabBarView in MainWindow with column-based sidebar layout (260px sidebar + 1px divider + content)
- Added attention indicators: blinking blue dot, blue title text, and blinking blue border around entire tab entry
- Fixed dark theme styling on rename TextBox input
- Established project-wide dark theme guideline in CLAUDE.md
- Rename only commits on Enter key (not on focus loss)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create TabSidebarView and restructure MainWindow layout** - `205e1db` (feat)
2. **Fix 1: Remove output preview text** - `a89ba59` (fix)
3. **Fix 2: Dark theme rename input + CLAUDE.md** - `70770e3` (fix)
4. **Fix 3: Rename waits for Enter** - `7c62402` (fix)
5. **Fix 4: Tab ghosting bug documentation** - `4a9a135` (docs)
6. **Fix 5: Blinking blue border for attention** - `319f806` (feat)

## Files Created/Modified

- `src/Wcmux.App/Views/TabSidebarView.xaml` - Sidebar XAML shell with ScrollViewer and [+] button
- `src/Wcmux.App/Views/TabSidebarView.xaml.cs` - Programmatic tab entry construction, attention animation, rename flow
- `src/Wcmux.App/MainWindow.xaml` - Column-based layout with sidebar in Column 0
- `src/Wcmux.App/MainWindow.xaml.cs` - Sidebar wiring replacing TabBar references
- `src/Wcmux.App/Views/WorkspaceView.xaml.cs` - Added GetPreviewText method for sidebar access
- `CLAUDE.md` - Project guidelines including dark theme requirement

## Decisions Made

- Removed output preview text from sidebar tab entries per user preference (plan originally specified 2-line preview)
- All UI elements must use dark theme by default - documented in CLAUDE.md as project-wide rule
- Rename input commits only on Enter key press, cancels on Escape or focus loss (not auto-commit on LostFocus)
- Tab ghosting bug is a pre-existing WebView2 shared environment issue - deferred to future phase
- Attention indicator enhanced with blinking blue border wrapping entire tab entry (in addition to blue dot + blue title)

## Deviations from Plan

### Post-Checkpoint Fixes (User Feedback)

**1. Removed output preview text**
- **Issue:** User did not want the 2-line output preview shown in tab entries
- **Fix:** Removed preview TextBlock, preview fetching logic, and unused _previewForeground brush
- **Commit:** `a89ba59`

**2. Dark theme rename input**
- **Issue:** Rename TextBox appeared in light theme
- **Fix:** Added RequestedTheme=Dark, dark Background/Foreground/BorderBrush to TextBox
- **Commit:** `70770e3`

**3. Rename input behavior**
- **Issue:** Rename auto-committed on focus loss instead of waiting for Enter
- **Fix:** Changed LostFocus handler from CommitRename to CancelRename
- **Commit:** `7c62402`

**4. Tab ghosting bug (deferred)**
- **Issue:** WebView2 ghost rendering across tabs with shared CoreWebView2Environment
- **Fix:** Documented as deferred item - pre-existing architectural issue not caused by sidebar
- **Commit:** `4a9a135`

**5. Blinking blue border for attention**
- **Issue:** User wanted blinking blue border around entire tab entry, not just dot
- **Fix:** Wrapped tab entry Grid in Border that blinks in sync with dot/title animation
- **Commit:** `319f806`

---

**Total deviations:** 5 post-checkpoint adjustments (4 fixes, 1 deferred)
**Impact on plan:** All changes aligned with user intent. Preview text removal simplifies the sidebar. No scope creep.

## Issues Encountered

- Build could not copy output files due to running Wcmux.App instance (file lock). Verified compilation success by checking for CS errors only (no compilation errors found, only MSB copy failures).

## Deferred Items

- **Tab ghosting bug**: WebView2 ghost rendering when multiple tabs have split panes. See `.planning/phases/06-vertical-tab-sidebar/deferred-items.md` for full analysis and potential fixes.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Vertical tab sidebar fully functional with all user-requested adjustments
- Tab ghosting bug documented for future WebView2 lifecycle management phase
- CLAUDE.md established with dark theme guideline for all future UI work

---
*Phase: 06-vertical-tab-sidebar*
*Completed: 2026-03-09*
