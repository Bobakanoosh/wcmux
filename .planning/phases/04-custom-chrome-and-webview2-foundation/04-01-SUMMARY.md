---
phase: 04-custom-chrome-and-webview2-foundation
plan: 01
subsystem: ui
tags: [winui3, title-bar, InputNonClientPointerSource, custom-chrome, dark-theme]

# Dependency graph
requires:
  - phase: 03-multi-tab
    provides: MainWindow with TabBarView and TabContentArea layout
provides:
  - Custom dark title bar with app icon, title text, and styled caption buttons
  - InputNonClientPointerSource scaffold for future passthrough regions
  - ExtendsContentIntoTitleBar pattern for WinUI3 custom chrome
affects: [05-pane-title-bars, 06-browser-pane]

# Tech tracking
tech-stack:
  added: []
  patterns: [InputNonClientPointerSource for custom title bar regions, ExtendsContentIntoTitleBar before PreferredHeightOption ordering]

key-files:
  created: []
  modified:
    - src/Wcmux.App/MainWindow.xaml
    - src/Wcmux.App/MainWindow.xaml.cs

key-decisions:
  - "Used 32px standard title bar height instead of 48px tall height per user preference"
  - "Used InputNonClientPointerSource (not SetTitleBar) for future-proof passthrough region support"
  - "Used FontIcon glyph E756 as terminal icon placeholder"

patterns-established:
  - "Custom title bar pattern: ExtendsContentIntoTitleBar=true in code-behind, never XAML"
  - "Caption button styling via AppWindow.TitleBar color properties"
  - "SetRegionsForCustomTitleBar scaffold for Loaded/SizeChanged recalculation"

requirements-completed: [CHRM-01]

# Metrics
duration: 8min
completed: 2026-03-08
---

# Phase 4 Plan 1: Custom Dark Title Bar Summary

**Custom dark title bar with FontIcon, "wcmux" text, and dark-themed caption buttons using InputNonClientPointerSource**

## Performance

- **Duration:** 8 min (across two sessions with checkpoint)
- **Started:** 2026-03-08
- **Completed:** 2026-03-08
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Replaced default white Windows title bar with custom dark (#1e1e1e) chrome
- Added terminal icon (FontIcon glyph) and "wcmux" title text in title bar
- Configured dark-themed caption button colors (transparent bg, light fg, hover/press states)
- Established InputNonClientPointerSource scaffold for future interactive title bar elements
- Window dragging, double-click maximize, and Windows 11 snap layouts work correctly

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement custom dark title bar with InputNonClientPointerSource** - `5385dbf` (feat)
2. **Task 2: Visual verification + height adjustment** - `0b3d76f` (fix: reduce title bar to 32px)

## Files Created/Modified
- `src/Wcmux.App/MainWindow.xaml` - Added AppTitleBar grid row with icon and title text above TabBarView
- `src/Wcmux.App/MainWindow.xaml.cs` - ExtendsContentIntoTitleBar setup, caption button colors, SetRegionsForCustomTitleBar scaffold

## Decisions Made
- Used 32px standard height instead of 48px tall height -- user found 48px too large during visual verification
- Used InputNonClientPointerSource over SetTitleBar per locked roadmap decision (avoids post-drag interactive control bugs)
- FontIcon glyph E756 chosen as terminal icon placeholder (no external image asset needed)

## Deviations from Plan

### Auto-fixed Issues

**1. [User Feedback] Reduced title bar height from 48px to 32px**
- **Found during:** Task 2 (visual verification checkpoint)
- **Issue:** User found the 48px tall title bar too large, requested 32px
- **Fix:** Changed RowDefinition Height to 32, switched PreferredHeightOption to Standard, reduced icon size to 14px and tightened margins
- **Files modified:** src/Wcmux.App/MainWindow.xaml, src/Wcmux.App/MainWindow.xaml.cs
- **Verification:** Build succeeds with no compilation errors
- **Committed in:** 0b3d76f

---

**Total deviations:** 1 (user-requested height adjustment during verification)
**Impact on plan:** Minor cosmetic adjustment. No scope creep.

## Issues Encountered
- Build output copy failed due to running app process holding file lock (MSB3021) -- not a compilation error, code compiles cleanly

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Custom title bar foundation is in place for future phases to add interactive controls
- SetRegionsForCustomTitleBar scaffold ready for passthrough region registration (Phase 5+ pane title bars)
- WebView2 environment cache (04-02) already completed separately

---
*Phase: 04-custom-chrome-and-webview2-foundation*
*Completed: 2026-03-08*
