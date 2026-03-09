# Phase 6: Vertical Tab Sidebar - Context

**Gathered:** 2026-03-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace the horizontal tab bar with a vertical sidebar on the left showing tab title, current working directory, terminal output preview, and attention state. The sidebar becomes the primary tab navigation surface. Pane interaction (drag resize, swap, drag-to-rearrange) is Phase 7.

</domain>

<decisions>
## Implementation Decisions

### Sidebar layout
- Fixed width ~260px, positioned below the 32px title bar (title bar spans full width)
- Subtle 1px vertical divider line (#333) separating sidebar from terminal content area
- [+] New Tab button at the top of the sidebar, above the tab list
- Sidebar background slightly darker than content area to create visual separation

### Tab entry structure
- Stacked layout per entry: tab title (first line), cwd (second line, dimmer), 2 lines of output preview (dim gray)
- Active tab highlighted with lighter background (e.g., #2D2D2D vs darker sidebar bg), consistent with existing #3C3C3C active tab pattern
- Close button (X) appears on hover only — keeps sidebar clean when not interacting
- For multi-pane tabs, show cwd and preview from the currently focused pane in that tab
- Tab rename via right-click context menu (replacing double-click inline rename from horizontal bar)

### Output preview capture
- Ring buffer in TerminalSurfaceBridge to retain last N lines of plain text as output flows through
- Strip all ANSI/VT escape codes — plain monochrome text only, rendered in dim gray
- 2 lines of preview text per tab entry
- Refresh on 2-second interval, reusing the existing DispatcherTimer that polls foreground process names

### Attention indicators
- Blue dot (●) next to tab title + tab title text turns blue (#3282F0) when attention fires
- 4-blink animation then steady blue, porting the existing attention animation pattern
- Preview text stays dim gray during attention — only dot and title change color
- Cleared when tab is activated (same as existing behavior)
- AttentionStore already tracks per-pane attention with aggregation — sidebar subscribes to AttentionChanged events

### Claude's Discretion
- Exact sidebar background color value
- Tab entry padding and spacing
- Preview text font size relative to title/cwd
- Right-click context menu items beyond "Rename" (if any make sense)
- Ring buffer size (how many lines to retain beyond the displayed 2)
- VT escape code stripping implementation approach
- ScrollViewer behavior when many tabs overflow the sidebar height

</decisions>

<specifics>
## Specific Ideas

- Sidebar visual hierarchy: tab title is primary (normal weight, white), cwd is secondary (dimmer), preview is tertiary (dimmest)
- The [+] button placement at top mirrors the existing horizontal tab bar's [+] position — familiar to users
- Preview text should feel like a "glance" at what's happening — not a mini-terminal

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `TabBarView`: Existing tab rendering logic (RenderTabs, CreateTabItem, attention animation) — can be adapted for vertical layout
- `TabStore`: Observable tab state with TabsChanged and ActiveTabChanged events — sidebar subscribes to same events
- `AttentionStore`: Per-pane attention tracking with 5s cooldown and aggregation — sidebar reuses directly
- `TerminalSurfaceBridge`: Output batching at 16ms intervals — ring buffer hooks into existing flush pipeline
- Shared 2s `DispatcherTimer` in WorkspaceView for process name polling — extend for preview refresh

### Established Patterns
- Dark background #1e1e1e with #3C3C3C for active elements
- Blue #3282F0 for attention indicators
- 4-blink Storyboard animation for attention (DoubleAnimation on Opacity)
- Programmatic UI construction (TabBarView builds tabs in code-behind, not XAML templates)
- Event-driven UI updates via store subscriptions

### Integration Points
- `MainWindow.xaml`: Replace Row 1 (TabBarView) with a column-based layout — sidebar in Column 0, content in Column 1
- `TabBarView` → replaced by new `TabSidebarView`
- `MainWindow.cs`: Tab switching, attention handling, and tab lifecycle already managed — sidebar wires into same ViewModel
- `ISession.LastKnownCwd`: Already available for cwd display
- `LayoutStore.ActivePaneId`: Identifies focused pane per tab for multi-pane preview selection

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 06-vertical-tab-sidebar*
*Context gathered: 2026-03-08*
