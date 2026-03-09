# Phase 06 - Deferred Items

## Tab Ghosting Bug (WebView2 visual artifact)

**Reported during:** 06-02 checkpoint verification
**Severity:** Medium (visual artifact, does not cause data corruption)

**Reproduction:**
1. In Tab 1 Pane 1, run `claude`
2. Split Pane 1 horizontally (two panes stacked)
3. Create a new Tab 2
4. In Tab 2, also split horizontally
5. Tab 2 Pane 1 shows the same content as Tab 1 Pane 1
6. If focused the ghost content disappears, if focus moves away it comes back

**Root Cause Analysis:**
This is a pre-existing WebView2 rendering issue unrelated to the sidebar implementation. All WorkspaceViews (one per tab) are children of the same TabContentArea Grid, with inactive tabs hidden via `Visibility.Collapsed`. WebView2 controls sharing a `CoreWebView2Environment` (per Phase 04 decision) can exhibit ghost rendering through the compositor when multiple instances exist in the same visual tree.

The sidebar change did not introduce this bug -- it existed before with the horizontal tab bar, but may not have been noticed because the tab bar did not encourage the specific multi-tab multi-pane workflow described.

**Potential Fixes (for future phase):**
1. **Remove/re-add from visual tree**: Instead of Collapsed/Visible, remove the WorkspaceView from TabContentArea.Children when switching away, and re-add when switching back. Risk: WebView2 may need re-initialization.
2. **Separate CoreWebView2Environment per tab**: Isolates rendering but increases memory usage (contradicts Phase 04 decision).
3. **Detach/re-attach WebView2 sessions on tab switch**: More complex but cleanest fix.

**Decision:** Deferred to a future phase focused on WebView2 lifecycle management.

## Rename TextBox Light Theme Override

**Reported during:** 06-02 checkpoint verification
**Severity:** Low (cosmetic)

**Issue:** When renaming a tab via right-click context menu, the TextBox renders with a light-themed background despite `RequestedTheme="Dark"` set both at the element level and at the Application level in App.xaml. WinUI's TextBox control template appears to override theme resources in this specific context (inline-created TextBox inside a dynamically-built StackPanel).

**Attempted fixes:**
1. Manual brush overrides (Background, Foreground, BorderBrush) — overridden by WinUI focus states
2. `RequestedTheme = ElementTheme.Dark` on TextBox — no effect
3. `RequestedTheme="Dark"` on Application element in App.xaml — no effect

**Potential fixes (for future phase):**
1. Custom TextBox ControlTemplate with hardcoded dark colors for all visual states
2. Use a custom TextBox style defined in App.xaml resources with dark theme overrides
3. Use a ContentDialog or Flyout for rename instead of inline TextBox

**Decision:** Deferred — cosmetic issue, rename still functions correctly.
