# Phase 6: Vertical Tab Sidebar - Research

**Researched:** 2026-03-08
**Domain:** WinUI 3 vertical sidebar layout, terminal output capture, ANSI stripping
**Confidence:** HIGH

## Summary

Phase 6 replaces the existing horizontal `TabBarView` with a vertical sidebar (`TabSidebarView`) positioned as the leftmost column in MainWindow. The sidebar shows tab title, current working directory, a 2-line output preview, and attention indicators (blue dot + color change). The implementation touches three distinct areas: (1) layout restructuring of MainWindow from row-based to column-based below the title bar, (2) adding a ring buffer to `TerminalSurfaceBridge` that captures plain-text output by stripping ANSI/VT escape codes, and (3) building the new `TabSidebarView` UserControl with richer tab entries and right-click context menu for rename.

The codebase already has all the state management infrastructure needed. `TabStore` provides tab lifecycle events, `AttentionStore` provides per-pane attention tracking with `TabHasAttention()`, `ISession.LastKnownCwd` provides working directory, and `WorkspaceViewModel` provides `GetSessionForPane()`. The existing `TabBarView` code-behind pattern (programmatic UI construction, event-driven updates) should be replicated for the sidebar. The main new capability is output preview, which requires a ring buffer in the Core layer and a 2-second polling refresh in the sidebar view.

**Primary recommendation:** Implement in two waves: (1) Core ring buffer + ANSI stripping in `TerminalSurfaceBridge`, (2) UI sidebar view replacing TabBarView in MainWindow layout.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Fixed width ~260px sidebar, positioned below the 32px title bar (title bar spans full width)
- Subtle 1px vertical divider line (#333) separating sidebar from terminal content area
- [+] New Tab button at the top of the sidebar, above the tab list
- Sidebar background slightly darker than content area to create visual separation
- Stacked layout per tab entry: tab title (first line), cwd (second line, dimmer), 2 lines of output preview (dim gray)
- Active tab highlighted with lighter background (e.g., #2D2D2D vs darker sidebar bg), consistent with existing #3C3C3C active tab pattern
- Close button (X) appears on hover only
- For multi-pane tabs, show cwd and preview from the currently focused pane in that tab
- Tab rename via right-click context menu (replacing double-click inline rename from horizontal bar)
- Ring buffer in TerminalSurfaceBridge to retain last N lines of plain text as output flows through
- Strip all ANSI/VT escape codes -- plain monochrome text only, rendered in dim gray
- 2 lines of preview text per tab entry
- Refresh on 2-second interval, reusing the existing DispatcherTimer that polls foreground process names
- Blue dot next to tab title + tab title text turns blue (#3282F0) when attention fires
- 4-blink animation then steady blue, porting the existing attention animation pattern
- Preview text stays dim gray during attention -- only dot and title change color
- Cleared when tab is activated (same as existing behavior)
- AttentionStore already tracks per-pane attention with aggregation -- sidebar subscribes to AttentionChanged events

### Claude's Discretion
- Exact sidebar background color value
- Tab entry padding and spacing
- Preview text font size relative to title/cwd
- Right-click context menu items beyond "Rename" (if any make sense)
- Ring buffer size (how many lines to retain beyond the displayed 2)
- VT escape code stripping implementation approach
- ScrollViewer behavior when many tabs overflow the sidebar height

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SIDE-01 | User sees tabs in a vertical sidebar on the left showing tab title and cwd | MainWindow layout change from Row-based to Column-based below title bar; new TabSidebarView UserControl with stacked text per entry; ISession.LastKnownCwd already available |
| SIDE-02 | User sees the last 2-3 lines of terminal output as preview text per tab | Ring buffer added to TerminalSurfaceBridge captures plain text; ANSI/VT stripping regex; 2-second timer refresh in sidebar |
| SIDE-03 | User sees attention indicators on sidebar tabs when background panes ring the bell | AttentionStore.TabHasAttention() already exists; port blink animation pattern from TabBarView with blue dot + title color |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| WinUI 3 / WinAppSDK | 1.5+ | UI framework | Already used throughout project |
| .NET 9 | 9.0 | Runtime | Already used |
| xunit | 2.9.2 | Unit testing | Already in test project |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.RegularExpressions | built-in | ANSI escape stripping | For VT code removal in ring buffer |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Regex ANSI strip | Char-by-char state machine | More precise but more code; regex is sufficient for preview text |

**Installation:**
No new packages needed. All dependencies already present.

## Architecture Patterns

### Recommended Project Structure
```
src/
  Wcmux.Core/
    Terminal/
      TerminalSurfaceBridge.cs    # ADD: ring buffer + ANSI stripping
      OutputRingBuffer.cs         # NEW: ring buffer class (or inline in bridge)
  Wcmux.App/
    Views/
      TabSidebarView.xaml         # NEW: sidebar XAML shell
      TabSidebarView.xaml.cs      # NEW: sidebar code-behind
      TabBarView.xaml             # REMOVE: old horizontal tab bar
      TabBarView.xaml.cs          # REMOVE: old horizontal tab bar
    MainWindow.xaml               # MODIFY: column layout below title bar
    MainWindow.xaml.cs            # MODIFY: wire TabSidebarView instead of TabBar
tests/
  Wcmux.Tests/
    Terminal/
      OutputRingBufferTests.cs    # NEW: ring buffer + ANSI stripping tests
```

### Pattern 1: MainWindow Layout Restructuring
**What:** Change the area below the title bar from `TabBarView` (Row 1) + `TabContentArea` (Row 2) to a two-column layout: sidebar (Column 0, 260px fixed) + content (Column 1, star).
**When to use:** This is the core structural change.
**Example:**
```xml
<!-- MainWindow.xaml: Replace rows 1-2 with a single star row containing columns -->
<Grid Background="#1e1e1e">
    <Grid.RowDefinitions>
        <RowDefinition Height="32" />   <!-- Title bar -->
        <RowDefinition Height="*" />    <!-- Content area -->
    </Grid.RowDefinitions>

    <!-- Title bar (Row 0, unchanged, spans full width) -->
    <Grid x:Name="AppTitleBar" Grid.Row="0" ... />

    <!-- Main content (Row 1): sidebar + workspace -->
    <Grid Grid.Row="1">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="260" />  <!-- Sidebar -->
            <ColumnDefinition Width="Auto" />  <!-- Divider -->
            <ColumnDefinition Width="*" />     <!-- Content -->
        </Grid.ColumnDefinitions>

        <views:TabSidebarView x:Name="TabSidebar" Grid.Column="0" />
        <Border Grid.Column="1" Width="1" Background="#333333" />
        <Grid x:Name="TabContentArea" Grid.Column="2" />
    </Grid>
</Grid>
```

### Pattern 2: Tab Sidebar Entry (Programmatic Construction)
**What:** Each tab entry is a Grid with stacked TextBlocks, following the existing programmatic UI pattern from TabBarView.
**When to use:** For every tab rendered in the sidebar.
**Example:**
```csharp
// Stacked layout: title, cwd, preview (2 lines)
private Grid CreateSidebarTabEntry(string tabId, string title, string cwd,
    string preview, bool isActive, bool hasAttention)
{
    var entry = new Grid
    {
        Padding = new Thickness(12, 8, 8, 8),
        Background = new SolidColorBrush(isActive
            ? Windows.UI.Color.FromArgb(255, 45, 45, 45)    // #2D2D2D active
            : Windows.UI.Color.FromArgb(0, 0, 0, 0)),       // transparent inactive
    };

    entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // close button

    var textStack = new StackPanel();

    // Title row (with optional attention dot)
    var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
    if (hasAttention)
    {
        titlePanel.Children.Add(new TextBlock
        {
            Text = "\u25CF ",  // Blue dot
            Foreground = _attentionForeground,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
    }
    var titleBlock = new TextBlock
    {
        Text = title,
        FontSize = 12,
        Foreground = hasAttention ? _attentionForeground : _defaultForeground,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    titlePanel.Children.Add(titleBlock);
    textStack.Children.Add(titlePanel);

    // CWD line
    textStack.Children.Add(new TextBlock
    {
        Text = cwd,
        FontSize = 10,
        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
        TextTrimming = TextTrimming.CharacterEllipsis,
    });

    // Preview lines (2 lines max)
    textStack.Children.Add(new TextBlock
    {
        Text = preview,
        FontSize = 10,
        MaxLines = 2,
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 96, 96, 96)),
    });

    // ... close button on hover, click handlers, etc.
    return entry;
}
```

### Pattern 3: Ring Buffer in TerminalSurfaceBridge
**What:** Capture plain-text output lines in a circular buffer as data flows through the batch loop.
**When to use:** In the existing `RunOutputBatchLoop` after batching output and before delivering to surface.
**Example:**
```csharp
// In TerminalSurfaceBridge -- new fields
private readonly string[] _ringBuffer;
private int _ringHead;
private readonly int _ringCapacity;
private readonly object _ringLock = new();

// Strip ANSI and append to ring buffer
private void AppendToRingBuffer(string rawBatch)
{
    var plain = AnsiStripper.Strip(rawBatch);
    var lines = plain.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    lock (_ringLock)
    {
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (trimmed.Length == 0) continue;
            _ringBuffer[_ringHead % _ringCapacity] = trimmed;
            _ringHead++;
        }
    }
}

// Public accessor for sidebar to poll
public string[] GetRecentLines(int count)
{
    lock (_ringLock)
    {
        var result = new List<string>();
        var start = Math.Max(0, _ringHead - count);
        for (var i = start; i < _ringHead; i++)
        {
            var line = _ringBuffer[i % _ringCapacity];
            if (line is not null) result.Add(line);
        }
        return result.ToArray();
    }
}
```

### Pattern 4: ANSI/VT Escape Code Stripping
**What:** Regex-based removal of all ANSI escape sequences from terminal output.
**When to use:** Before storing text in the ring buffer.
**Example:**
```csharp
public static class AnsiStripper
{
    // Matches: ESC[ ... letter (CSI sequences), ESC] ... ST (OSC sequences),
    // ESC followed by single character, and standalone control chars
    private static readonly Regex AnsiPattern = new(
        @"\x1b\[[0-9;]*[a-zA-Z]"        // CSI: ESC[ params letter
        + @"|\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)"  // OSC: ESC] ... BEL or ST
        + @"|\x1b[()][0-9A-B]"          // Charset designation
        + @"|\x1b[a-zA-Z]"              // Two-char ESC sequences
        + @"|\x1b"                        // Bare ESC
        + @"|[\x00-\x08\x0b-\x0c\x0e-\x1f]",  // Control chars (keep \n \r \t)
        RegexOptions.Compiled);

    public static string Strip(string input) => AnsiPattern.Replace(input, "");
}
```

### Pattern 5: Right-Click Context Menu
**What:** WinUI 3 `MenuFlyout` attached to each tab entry for rename.
**When to use:** Replacing the double-click inline rename from TabBarView.
**Example:**
```csharp
var menu = new MenuFlyout();
var renameItem = new MenuFlyoutItem { Text = "Rename" };
renameItem.Click += (s, e) => StartRename(tabId, currentLabel);
menu.Items.Add(renameItem);
entry.ContextFlyout = menu;
```

### Anti-Patterns to Avoid
- **Do NOT use XAML ItemsRepeater/ListView for the tab list:** The existing pattern is programmatic code-behind construction (TabBarView.RenderTabs). Introducing data binding would create an inconsistency with the rest of the codebase and add complexity.
- **Do NOT read the entire xterm.js scrollback for preview:** The ring buffer intercepts output in the TerminalSurfaceBridge pipeline, never touching the WebView DOM.
- **Do NOT create a per-tab DispatcherTimer for preview refresh:** Reuse the existing 2-second shared timer pattern from WorkspaceView's process name polling.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ANSI escape stripping | Character-by-character parser handling all VT100/VT520 sequences | Single compiled Regex covering CSI, OSC, two-char ESC, and control chars | Preview text doesn't need perfect fidelity; regex covers 99% of real terminal output |
| Ring buffer | Custom linked list or resizable array | Fixed-size array with modular index (`head % capacity`) | Simple, O(1) write, bounded memory, well-understood pattern |
| Tab attention animation | Custom Storyboard or animation framework | `DispatcherTimer` with toggle counter (existing pattern from TabBarView) | Already proven in the codebase; 4-blink then steady |
| Context menu | Custom popup positioning logic | WinUI 3 `MenuFlyout` / `ContextFlyout` property | Built-in, handles positioning, dismissal, keyboard accessibility |
| Scrollable tab list | Manual scroll handling | WinUI 3 `ScrollViewer` wrapping the tab list StackPanel | Built-in scroll, handles touch/mouse/keyboard |

## Common Pitfalls

### Pitfall 1: Multi-Pane CWD/Preview Resolution
**What goes wrong:** Showing the wrong pane's cwd or preview for a multi-pane tab.
**Why it happens:** Each tab can have multiple panes. The sidebar must show the focused pane's info, not a random pane's.
**How to avoid:** For each tab, use `LayoutStore.ActivePaneId` to find the focused pane, then look up its session and bridge for cwd/preview. The CONTEXT.md explicitly says "for multi-pane tabs, show cwd and preview from the currently focused pane in that tab."
**Warning signs:** Sidebar shows stale cwd or preview from a pane the user isn't looking at.

### Pitfall 2: Ring Buffer Thread Safety
**What goes wrong:** Race condition between the batch loop writing to the ring buffer and the UI timer reading from it.
**Why it happens:** `RunOutputBatchLoop` runs on a background thread; the 2-second timer runs on the UI thread.
**How to avoid:** Use a lock around ring buffer reads and writes. The buffer is small and operations are fast, so contention is minimal.
**Warning signs:** Garbled preview text, occasional null reference.

### Pitfall 3: TerminalSurfaceBridge Ownership Per Session
**What goes wrong:** Can't find the right bridge to get preview text from.
**Why it happens:** `TerminalSurfaceBridge` is created in `TerminalPaneView.AttachAsync()` and not exposed back up the chain.
**How to avoid:** Either: (a) store the bridge reference in a lookup accessible from the sidebar (e.g., on WorkspaceViewModel or a new service), or (b) store preview lines directly on the bridge and expose via a method the sidebar can reach through `WorkspaceViewModel -> pane -> bridge`. The cleanest path is to have each `TerminalPaneView` expose its bridge or preview text through a property.
**Warning signs:** No preview text appears because the sidebar can't access the bridge.

### Pitfall 4: ANSI Regex Missing Sequences
**What goes wrong:** Preview text shows garbled escape sequences or partial escape codes.
**Why it happens:** Terminal applications emit a wide variety of VT sequences; a simple regex may miss some.
**How to avoid:** The regex must cover at minimum: CSI sequences (`ESC[...letter`), OSC sequences (`ESC]...BEL` or `ESC]...ST`), two-character ESC sequences, charset designations, and standalone control characters. Test with real output from PowerShell, bash, and common TUI apps.
**Warning signs:** Preview shows `[0m` or `]0;title` fragments.

### Pitfall 5: MainWindow Layout Change Breaking Title Bar Regions
**What goes wrong:** Custom title bar drag regions stop working after restructuring the MainWindow grid.
**Why it happens:** `SetRegionsForCustomTitleBar()` uses `AppTitleBar.XamlRoot` for scaling. If the title bar is moved or its grid structure changes, region calculations could break.
**How to avoid:** Keep `AppTitleBar` in Row 0 spanning full width. The sidebar goes in Row 1 alongside content. The title bar row structure is unchanged.
**Warning signs:** Window can't be dragged by the title bar, or caption buttons don't respond.

### Pitfall 6: Close Button Hover Visibility
**What goes wrong:** Close button is always visible or never visible.
**Why it happens:** WinUI 3 doesn't have CSS-style `:hover` pseudoclasses for showing/hiding child elements.
**How to avoid:** Use `PointerEntered`/`PointerExited` events on the tab entry Grid to toggle the close button's `Visibility` property between `Collapsed` and `Visible`.
**Warning signs:** Visual clutter from always-visible close buttons; or inability to close tabs.

## Code Examples

### Accessing CWD for a Tab's Active Pane
```csharp
// Given a tabId, get the cwd for the sidebar entry
private string GetCwdForTab(string tabId)
{
    var workspace = _viewModel?.GetWorkspace(tabId);
    if (workspace is null) return "";

    var activePaneId = workspace.LayoutStore.ActivePaneId;
    var session = workspace.GetSessionForPane(activePaneId);
    return session?.LastKnownCwd ?? "";
}
```

### Wiring the Sidebar in MainWindow.cs
```csharp
// Replace TabBar.Attach with TabSidebar.Attach
TabSidebar.Attach(_tabViewModel, _attentionStore);
TabSidebar.NewTabRequested += () => CreateNewTabWithViewAsync();

// Remove old TabBar references entirely
```

### Extending the 2-Second Timer for Preview Refresh
```csharp
// In TabSidebarView -- the timer callback re-renders all tab entries
// pulling fresh preview text from each tab's active pane bridge
private void OnRefreshTimerTick(object? sender, object e)
{
    RenderTabs(); // Full re-render picks up new preview text, process names, cwd
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Horizontal tab strip (TabBarView) | Vertical sidebar (TabSidebarView) | This phase | Richer tab info, better space usage |
| Double-click to rename tab | Right-click context menu | This phase | More discoverable, room for future menu items |
| No output preview | Ring buffer + 2-line preview | This phase | Users can see terminal activity at a glance |

## Open Questions

1. **Bridge accessibility from sidebar**
   - What we know: `TerminalSurfaceBridge` is created inside `TerminalPaneView.AttachAsync()` and currently has no upward reference path to the sidebar.
   - What's unclear: Best place to store/expose preview text so the sidebar can poll it without tight coupling.
   - Recommendation: Add a `GetRecentLines(int count)` method to `TerminalSurfaceBridge`, and expose each pane's bridge (or just its preview lines) through `TerminalPaneView` -> discoverable from `WorkspaceView` -> accessible from `MainWindow`. Alternatively, store a `Dictionary<string, TerminalSurfaceBridge>` keyed by paneId on `WorkspaceView` and expose a method `GetPreviewText(string paneId)`.

2. **Ring buffer capacity**
   - What we know: Only 2 lines displayed. Need a small buffer for robustness.
   - What's unclear: Exact size (10? 20? 50 lines?).
   - Recommendation: 20 lines -- provides enough history to always have 2 non-empty lines even with blank-line-heavy output, while keeping memory trivial (~2KB per pane).

3. **Sidebar background color**
   - What we know: Must be "slightly darker than content area." Content area is #1e1e1e.
   - Recommendation: #191919 for sidebar background. Subtle difference, visible under typical display conditions.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 |
| Config file | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| Quick run command | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBuffer"` |
| Full suite command | `dotnet test tests/Wcmux.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SIDE-01 | Tab sidebar shows title and cwd | manual-only | N/A (WinUI 3 visual) | N/A |
| SIDE-02 | Output preview shows last 2 lines | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBuffer" -x` | Wave 0 |
| SIDE-02 | ANSI stripping removes escape codes | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AnsiStripper" -x` | Wave 0 |
| SIDE-03 | Attention indicators on sidebar tabs | manual-only | N/A (visual animation) | N/A |

**Manual-only justification:** SIDE-01 and SIDE-03 are purely visual layout and animation behaviors in WinUI 3 code-behind. The project has no UI automation test harness. The underlying state (TabStore events, AttentionStore) is already unit-tested.

### Sampling Rate
- **Per task commit:** `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~OutputRingBuffer OR FullyQualifiedName~AnsiStripper" -x`
- **Per wave merge:** `dotnet test tests/Wcmux.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Wcmux.Tests/Terminal/OutputRingBufferTests.cs` -- covers ring buffer capacity, wrap-around, thread safety, GetRecentLines
- [ ] `tests/Wcmux.Tests/Terminal/AnsiStripperTests.cs` -- covers CSI, OSC, control chars, mixed content

## Sources

### Primary (HIGH confidence)
- Codebase analysis: TabBarView.xaml.cs, MainWindow.xaml, MainWindow.xaml.cs, TerminalSurfaceBridge.cs, AttentionStore.cs, TabStore.cs, TabViewModel.cs, WorkspaceView.xaml.cs, WorkspaceViewModel.cs, ISession.cs, LayoutStore.cs
- CONTEXT.md: User decisions from discussion phase

### Secondary (MEDIUM confidence)
- WinUI 3 MenuFlyout / ContextFlyout API (standard WinUI 3 pattern, well-documented)
- Regex-based ANSI stripping (common pattern, widely used in terminal tooling)

### Tertiary (LOW confidence)
- None -- all findings based on direct codebase analysis

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all patterns already in codebase
- Architecture: HIGH -- direct extension of existing TabBarView/MainWindow patterns
- Pitfalls: HIGH -- identified from concrete code paths and data flow analysis

**Research date:** 2026-03-08
**Valid until:** 2026-04-08 (stable, no external dependency changes expected)
