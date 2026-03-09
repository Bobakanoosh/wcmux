# Architecture Research: v1.1 UI/UX Integration

**Domain:** WinUI 3 terminal multiplexer -- custom chrome, vertical tabs, pane title bars, browser panes
**Researched:** 2026-03-08
**Confidence:** HIGH (existing codebase fully analyzed, WinUI 3 APIs verified against official docs)

## Existing Architecture Summary

Before detailing what changes, here is the current component map:

```
MainWindow (Window)
 ├── TabBarView (UserControl) ─── horizontal tab strip, renders from TabStore
 ├── TabContentArea (Grid) ─── hosts one WorkspaceView per tab (visibility-toggled)
 │    └── WorkspaceView (UserControl) ─── renders split tree as positioned Grid children
 │         └── per-pane Grid
 │              ├── Border (active/attention highlight)
 │              │    └── TerminalPaneView (UserControl) ─── WebView2 + xterm.js
 │              ├── TextBlock (cwd title overlay)
 │              └── Button (split affordance, hover-revealed)
 │
 ├── TabViewModel ─── orchestrates TabStore + per-tab WorkspaceViewModel
 ├── WorkspaceViewModel ─── routes split/close/focus through LayoutStore
 ├── LayoutStore ─── immutable split tree, PaneRect computation, active pane tracking
 ├── SessionManager ─── creates/tracks ConPTY sessions, event bus
 └── AttentionStore ─── bell-based attention state per pane
```

Key architectural invariants to preserve:
- **LayoutStore owns geometry.** Views render from computed PaneRects; they never define layout.
- **SessionManager is the event bus.** All session lifecycle events flow through SessionEventReceived.
- **WorkspaceViewModel bridges layout and sessions.** Pane commands route through it, never directly to LayoutStore.
- **TabViewModel manages per-tab workspaces.** Tab switching is visibility-toggled, not recreated.

## New Components and Integration Points

### Feature 1: Custom Dark Title Bar

**What changes:** Replace the default Windows title bar with a custom dark title bar that matches the app aesthetic (#1e1e1e / #252526 palette).

**Integration point:** `MainWindow.xaml` and `MainWindow.xaml.cs` only.

**Approach:** Use the WinUI 3 `ExtendsContentIntoTitleBar` + `SetTitleBar` pattern. This is the established approach since Windows App SDK 1.4, verified in current Microsoft docs (updated 2026-02-28). A `TitleBar` control was added in SDK 1.7, but the manual `ExtendsContentIntoTitleBar` approach gives more control and avoids a dependency on newer SDK features when the app already targets SDK 1.8.

**New component:** None needed as a separate class. The title bar is a XAML element within MainWindow.

**Modified files:**
- `MainWindow.xaml` -- Add a title bar row to the root Grid with drag region, app title, and caption button awareness
- `MainWindow.xaml.cs` -- Set `ExtendsContentIntoTitleBar = true` in constructor, call `SetTitleBar()` on the drag region element

**Implementation pattern:**

```xml
<!-- MainWindow.xaml -->
<Grid Background="#1e1e1e">
    <Grid.RowDefinitions>
        <RowDefinition Height="32" />   <!-- Custom title bar -->
        <RowDefinition Height="*" />    <!-- App content (sidebar + workspace) -->
    </Grid.RowDefinitions>

    <!-- Title bar drag region -->
    <Grid x:Name="AppTitleBar" Grid.Row="0" Background="#252526">
        <TextBlock Text="wcmux" VerticalAlignment="Center"
                   Margin="16,0,0,0" FontSize="12" Foreground="#999" />
    </Grid>

    <!-- Rest of app content goes in Row 1 -->
</Grid>
```

```csharp
// MainWindow.xaml.cs constructor
ExtendsContentIntoTitleBar = true;
SetTitleBar(AppTitleBar);

// Caption button colors via AppWindow
AppWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(255, 37, 37, 38);
AppWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 153, 153, 153);
AppWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 60, 60, 60);
AppWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 37, 37, 38);
```

**Risk:** LOW. This is a well-documented, widely-used WinUI 3 pattern. The app already uses `WindowsPackageType=None` (unpackaged), which is fully supported.

---

### Feature 2: Vertical Tab Sidebar with Output Preview

**What changes:** Replace the horizontal `TabBarView` with a vertical sidebar on the left side of the window. Each tab entry shows: tab label, last known cwd, and a small output preview.

**Integration points:**
- `MainWindow.xaml` -- layout changes from Row-based (TabBar top, content bottom) to Column-based (sidebar left, content right)
- `TabBarView.xaml` + `TabBarView.xaml.cs` -- complete rewrite from horizontal strip to vertical panel
- `TabStore` / `TabViewModel` -- **no changes needed**, the view already renders from TabStore state

**New component:** `TabSidebarView` (replaces `TabBarView`). This is a rename + rewrite, not an addition, because the old horizontal tab bar is being removed entirely.

**Modified files:**
- `MainWindow.xaml` -- Change Grid layout from rows to columns
- New `Views/TabSidebarView.xaml` + `.cs` (replacing `TabBarView`)

**Output preview approach:**

The output preview requires capturing recent terminal output text per pane. Two options:

**Option A (recommended): Ring buffer in TerminalSurfaceBridge.** The bridge already receives all output text via `EnqueueOutput()`. Add a small ring buffer (last ~500 chars) that the sidebar can poll. This keeps the data in the Core layer and avoids coupling views.

**Option B: Screenshot via WebView2.** Use `CoreWebView2.CapturePreviewAsync()` to get a bitmap. This is heavyweight, requires the WebView2 to be rendered (visibility-toggled tabs have collapsed WebViews), and introduces image scaling complexity.

**Recommendation: Option A.** Store the last N characters of output in `TerminalSurfaceBridge`, expose via `ISession` or a new property on the bridge. The sidebar renders this as a monospace TextBlock with small font, clipped to the sidebar width. This is what terminal multiplexers like tmux do for preview -- show the last few lines of text output, not a pixel-perfect screenshot.

**Data flow for output preview:**
```
ConPTY output --> SessionOutputEvent --> TerminalSurfaceBridge.EnqueueOutput()
                                              | (new: also append to ring buffer)
                                         RingBuffer<string> LastOutput
                                              | (polled on timer or tab change)
                                         TabSidebarView renders preview text
```

**New Core type:** `OutputRingBuffer` in `Wcmux.Core.Terminal` -- a simple circular buffer of the last N characters of terminal output. Attached per-session or per-bridge.

**Modified Core files:**
- `TerminalSurfaceBridge.cs` -- Add `OutputRingBuffer` field, append in `EnqueueOutput()`
- `ISession.cs` -- Add `string? RecentOutput { get; }` property (or keep on bridge and access via WorkspaceViewModel)

**Sidebar layout:**
```
+----------------------+
| Tab 1 (active)       |
| ~/projects/wcmux     |  <-- cwd from session.LastKnownCwd
| +------------------+ |
| | $ npm run build  | |  <-- output preview (last ~4 lines)
| | > tsc && ...     | |
| +------------------+ |
+----------------------+
| Tab 2                |
| ~/projects/other     |
| +------------------+ |
| | $ git status     | |
| +------------------+ |
+----------------------+
|         [+]          |  <-- New tab button
+----------------------+
```

**Risk:** MEDIUM. The sidebar itself is straightforward XAML. The output ring buffer is simple. The risk is in the preview rendering -- stripping ANSI escape sequences to get clean text for the preview TextBlock. Need an ANSI-stripping utility.

---

### Feature 3: Pane Title Bars with Foreground Process Detection

**What changes:** Each pane gets a small title bar showing the foreground process name (e.g., "pwsh", "node", "claude"), with action buttons (close, split-h, split-v, open-browser).

**Integration points:**
- `WorkspaceView.cs` -- Modify `CreatePaneViewAsync()` to add a title bar row above the terminal
- New `ProcessDetector` service in `Wcmux.Core.Runtime`
- `ConPtySession` -- Expose the child process PID

**New components:**

1. **`PaneTitleBar`** -- A lightweight UserControl (or inline XAML in WorkspaceView) showing process name + action buttons. Not a standalone control because it is tightly coupled to pane lifecycle.

2. **`ProcessDetector`** (new, in `Wcmux.Core.Runtime`) -- Polls the process tree for the foreground process of a ConPTY session. Windows does not have an event-based API for "foreground process changed in a console," so polling is required.

**Foreground process detection approach:**

The ConPTY session launches a shell process (e.g., `pwsh.exe`). When the user runs `node app.js`, the shell spawns `node` as a child. The "foreground process" is the deepest child in the process tree rooted at the ConPTY shell PID.

**Detection method:** Walk the process tree from the known shell PID using `System.Diagnostics.Process` and WMI `Win32_Process` queries:

```csharp
// In ProcessDetector.cs
public static string? GetForegroundProcessName(int shellPid)
{
    try
    {
        // Get all processes with this parent
        using var searcher = new ManagementObjectSearcher(
            $"SELECT ProcessId, Name FROM Win32_Process WHERE ParentProcessId = {shellPid}");

        var children = searcher.Get().Cast<ManagementObject>().ToList();
        if (children.Count == 0)
            return Process.GetProcessById(shellPid)?.ProcessName;

        // Walk deepest child (last spawned, most likely the foreground process)
        var deepest = children.Last();
        var childPid = Convert.ToInt32(deepest["ProcessId"]);

        // Recurse to find the deepest descendant
        return GetForegroundProcessName(childPid)
            ?? deepest["Name"]?.ToString()?.Replace(".exe", "");
    }
    catch
    {
        return null;
    }
}
```

**Alternative (faster, no WMI):** Use `NtQueryInformationProcess` with `ProcessBasicInformation` to get parent PID, then enumerate `Process.GetProcesses()` and filter. This avoids WMI overhead but requires P/Invoke. Given the polling nature (every 1-2 seconds), WMI overhead (~5ms per query) is acceptable.

**Polling architecture:**

```
ProcessDetector (singleton, runs on background thread)
    | polls every 1-2 seconds
    | for each active session: walk process tree from shell PID
    | if process name changed:
SessionManager.RaiseEvent(new SessionProcessChangedEvent(sessionId, processName))
    |
WorkspaceView listens --> updates pane title bar text
```

**New event type:** `SessionProcessChangedEvent` in `Wcmux.Core.Runtime.SessionEvent`

**Modified files:**
- `ConPtySession.cs` -- Expose `int ShellProcessId` (the PID is already available from `_process.Id`)
- `ISession.cs` -- Add `int ShellProcessId { get; }` and `string? ForegroundProcessName { get; }`
- `SessionEvent.cs` -- Add `SessionProcessChangedEvent` record
- `SessionManager.cs` -- No changes (already has `RaiseEvent()`)
- `WorkspaceView.xaml.cs` -- Modify pane container creation to include title bar

**Pane container structure (modified):**
```
per-pane Grid (existing, repositioned by PaneRects)
 +-- Row 0: PaneTitleBar (NEW, ~24px height)
 |    +-- TextBlock "node" (foreground process name)
 |    +-- TextBlock "~/projects/app" (cwd, moved from overlay)
 |    +-- StackPanel (action buttons: [browser] [split-h] [split-v] [close])
 +-- Row 1: Border (existing active/attention highlight)
 |    +-- TerminalPaneView (existing WebView2 + xterm.js)
 +-- (split affordance button removed -- replaced by title bar actions)
```

**Risk:** MEDIUM. WMI process tree walking is well-understood but has edge cases: processes that exit between enumeration and PID lookup, race conditions with rapid process spawning. The polling interval needs tuning -- too fast wastes CPU, too slow feels laggy. 1 second is a good starting point (Windows Terminal uses similar polling).

---

### Feature 4: Browser Pane Hosting

**What changes:** Users can open a WebView2-based browser pane in the split tree, alongside terminal panes.

**Integration points:**
- `LayoutNode.cs` -- The `LeafNode` currently assumes all panes are terminal sessions (`SessionId`). Browser panes need a different leaf type or a discriminator.
- `WorkspaceView.cs` -- Must handle creating `BrowserPaneView` instead of `TerminalPaneView` based on pane type.
- `WorkspaceViewModel.cs` -- Needs a method to create browser panes (no session involved).

**Architecture decision: Extend LeafNode vs. new node type.**

**Recommendation: Add a `PaneKind` discriminator to LeafNode.** This is simpler than a separate `BrowserLeafNode` because the layout tree (split ratios, geometry computation, active pane tracking) is pane-type-agnostic. Only the view layer cares whether a pane is a terminal or browser.

```csharp
public enum PaneKind { Terminal, Browser }

public sealed record LeafNode : LayoutNode
{
    public required string PaneId { get; init; }
    public required string SessionId { get; init; }  // empty for browser panes
    public PaneKind Kind { get; init; } = PaneKind.Terminal;
    public string? BrowserUrl { get; init; }  // only set for browser panes
}
```

**New component:** `BrowserPaneView` (new UserControl in `Wcmux.App.Views`)

```
BrowserPaneView (UserControl)
 +-- Grid
 |    +-- Row 0: Address bar (TextBox + Go button, ~32px)
 |    +-- Row 1: WebView2 (navigates to BrowserUrl)
```

**Key constraint:** Each WebView2 instance uses a separate renderer process. The app already creates one WebView2 per terminal pane (for xterm.js). Browser panes add another WebView2 instance. Memory usage scales linearly. This is acceptable for typical use (2-4 browser panes), but should be documented as a known resource cost.

**WebView2 environment sharing:** All WebView2 instances in a WinUI 3 app share the same `CoreWebView2Environment` by default (same user data folder). This is fine -- browser panes and terminal panes can coexist without environment conflicts because they navigate to different content.

**Modified files:**
- `LayoutNode.cs` -- Add `PaneKind` enum and `BrowserUrl` property to `LeafNode`
- `WorkspaceViewModel.cs` -- Add `CreateBrowserPaneAsync(string url)` method
- `WorkspaceView.xaml.cs` -- In `CreatePaneViewAsync()`, branch on `LeafNode.Kind` to create `BrowserPaneView` or `TerminalPaneView`
- `LayoutStore.cs` -- Minor: `SplitActivePane` needs to accept `PaneKind` parameter (or the caller passes a full `LeafNode`)
- New: `Views/BrowserPaneView.xaml` + `.cs`

**Browser pane via title bar action:** The "open browser" button in the pane title bar (Feature 3) should split the active pane and create a browser pane in the new split. This reuses the existing split flow in WorkspaceViewModel.

**Risk:** LOW for basic hosting. MEDIUM for edge cases (WebView2 navigation failures, mixed focus between terminal and browser WebView2 instances, keyboard shortcut conflicts when browser pane is focused).

---

## System Overview After v1.1

```
+---------------------------------------------------------------------+
| MainWindow                                                          |
|  +--------------------------------------------------------------+   |
|  | Row 0: Custom Title Bar (AppTitleBar)                         |   |
|  |   "wcmux"  --- drag region ---  [_][o][X] (system caption)   |   |
|  +--------------------------------------------------------------+   |
|  | Row 1: Content Area                                           |   |
|  |  +-------------+----------------------------------------+    |   |
|  |  | TabSidebar  |  WorkspaceView (active tab)             |    |   |
|  |  |  View       |   +----------------+------------------+ |    |   |
|  |  |             |   | Pane Title Bar | Pane Title Bar   | |    |   |
|  |  |  Tab 1 *    |   | "node" [x]    | "pwsh" [x]       | |    |   |
|  |  |  preview... |   +----------------+------------------+ |    |   |
|  |  |             |   | Terminal       | Terminal         | |    |   |
|  |  |  Tab 2      |   | (WebView2     | (WebView2        | |    |   |
|  |  |  preview... |   |  + xterm.js)  |  + xterm.js)     | |    |   |
|  |  |             |   +----------------+------------------+ |    |   |
|  |  |  [+]        |                                         |    |   |
|  |  +-------------+-----------------------------------------+    |   |
|  +--------------------------------------------------------------+   |
+---------------------------------------------------------------------+
```

### Component Responsibilities (new and modified)

| Component | Responsibility | Status |
|-----------|----------------|--------|
| `MainWindow` | Custom title bar setup, layout columns, tab + workspace orchestration | MODIFIED |
| `TabSidebarView` | Vertical tab list with cwd, output preview, attention indicators | NEW (replaces TabBarView) |
| `PaneTitleBar` (inline in WorkspaceView) | Process name display, action buttons per pane | NEW |
| `BrowserPaneView` | WebView2 browser surface with address bar | NEW |
| `ProcessDetector` | Polls process tree per session, raises events on change | NEW |
| `OutputRingBuffer` | Circular buffer of recent terminal output for preview | NEW |
| `WorkspaceView` | Renders split tree with pane title bars and browser panes | MODIFIED |
| `LayoutNode` / `LeafNode` | Pane kind discriminator for terminal vs browser | MODIFIED |
| `WorkspaceViewModel` | Browser pane creation method | MODIFIED |
| `ISession` / `ConPtySession` | Expose shell PID, foreground process name | MODIFIED |
| `TerminalSurfaceBridge` | Output ring buffer integration | MODIFIED |
| All other components | **No changes** | UNCHANGED |

## Data Flow Changes

### New: Foreground Process Detection Flow

```
ProcessDetector (background timer, 1s interval)
    | for each tracked session:
    |   walk process tree from session.ShellProcessId
    |   compare to last known process name
    v if changed:
SessionManager.RaiseEvent(SessionProcessChangedEvent)
    |
    v
WorkspaceView.OnSessionEvent() --> update pane title bar TextBlock
```

### New: Output Preview Flow

```
ConPTY output --> SessionOutputEvent --> TerminalSurfaceBridge.EnqueueOutput()
    | (existing: batched delivery to xterm.js)
    | (new: also appends to OutputRingBuffer)

TabSidebarView (on tab render / periodic refresh)
    --> reads OutputRingBuffer.GetRecentText()
    --> strips ANSI escapes
    --> renders in monospace TextBlock
```

### Modified: MainWindow Layout Flow

```
MainWindow Grid:
  Row 0: AppTitleBar (spans full width, 32px)
  Row 1: Content Grid
    Column 0: TabSidebarView (~220px)
    Column 1: TabContentArea (fill)
```

The title bar spans the full window width (including over the sidebar) for proper drag behavior and visual continuity with the system caption buttons.

## Architectural Patterns

### Pattern 1: Polling with Event Emission

**What:** The `ProcessDetector` polls on a timer but emits events through the existing `SessionManager` event bus. Consumers (views) react to events, never poll directly.

**When to use:** When the underlying OS API is poll-based (no event notification for process tree changes) but the app architecture is event-driven.

**Trade-offs:** Adds a background timer thread. Polling interval is a latency-vs-CPU tradeoff. 1 second is the sweet spot (Windows Terminal uses similar).

### Pattern 2: View-Type Branching in WorkspaceView

**What:** `WorkspaceView.CreatePaneViewAsync()` checks `LeafNode.Kind` and creates either `TerminalPaneView` or `BrowserPaneView`. The layout tree remains type-agnostic.

**When to use:** When the layout algorithm (split ratios, geometry) is identical for all pane types, but the rendered content differs.

**Trade-offs:** Keeps LayoutStore simple. Adds a branch in the view layer. If more pane types emerge later (markdown preview, image viewer), this pattern scales by adding cases.

### Pattern 3: Ring Buffer for Preview Data

**What:** Store last N chars of output in a fixed-size buffer, overwriting oldest data. Consumers read a snapshot.

**When to use:** When you need recent history without unbounded memory growth.

**Trade-offs:** Fixed memory cost (~500 chars per session). Loses old data by design. Thread-safe reads require lock or concurrent collection.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Direct WebView2 Screenshot for Preview

**What people do:** Use `CapturePreviewAsync()` to get a bitmap of the terminal and display it in the sidebar.
**Why it's wrong:** Collapsed WebView2 instances (inactive tabs) don't render, so screenshots are blank. Memory-heavy for many tabs. Scaling/DPI issues.
**Do this instead:** Use text-based output ring buffer and render as monospace text.

### Anti-Pattern 2: Process Detection via Global Process Enumeration

**What people do:** Call `Process.GetProcesses()` and search all processes for children of the shell PID.
**Why it's wrong:** Enumerates thousands of processes. Slow (~50ms), high allocation pressure when polled every second.
**Do this instead:** Use targeted WMI query with `ParentProcessId` filter, or walk the tree from the known shell PID using `NtQueryInformationProcess`.

### Anti-Pattern 3: Putting Layout Logic in PaneTitleBar

**What people do:** Let the pane title bar's close/split buttons directly mutate the layout tree.
**Why it's wrong:** Breaks the existing pattern where all layout mutations go through WorkspaceViewModel.
**Do this instead:** Pane title bar actions should raise events that WorkspaceView routes through WorkspaceViewModel, same as existing keyboard shortcuts and split affordance clicks.

### Anti-Pattern 4: Separate WebView2 Environment for Browser Panes

**What people do:** Create a custom `CoreWebView2Environment` for browser panes to isolate them from terminal WebView2s.
**Why it's wrong:** Each environment creates a separate browser process tree. Doubles memory usage. Unnecessary because terminal and browser WebView2s don't share state (different URLs, no cookies to leak).
**Do this instead:** Let all WebView2 instances share the default environment.

## Build Order (Dependency-Driven)

The features have these dependencies:

```
Custom Title Bar --- no deps, standalone MainWindow change
         |
         v
Vertical Tab Sidebar --- depends on title bar (layout restructuring)
         |                needs OutputRingBuffer (for preview)
         |
Pane Title Bars --- depends on ProcessDetector
         |          changes pane container structure in WorkspaceView
         |          hosts browser pane action button
         |
         v
Browser Pane --- depends on pane title bar (action button trigger)
                 depends on LeafNode.PaneKind (layout model change)
```

**Recommended build order:**

1. **Custom Title Bar** -- Smallest scope, no downstream dependencies. Establishes the visual foundation.
2. **Pane Title Bars + Process Detection** -- Build the ProcessDetector and pane title bar UI. This changes the pane container structure in WorkspaceView, which must be done before the browser pane (which also needs the pane container structure).
3. **Vertical Tab Sidebar** -- Build OutputRingBuffer, ANSI stripper, and sidebar view. Replace TabBarView.
4. **Browser Pane** -- Add PaneKind to LeafNode, build BrowserPaneView, wire the action button.

**Rationale:** Title bar is isolated. Process detection and pane title bars are the most architecturally invasive (new background service, modified pane containers). Sidebar is mostly a view rewrite. Browser pane needs the pane title bar action button and the PaneKind model change.

## Scaling Considerations

| Concern | At 5 panes | At 20 panes | At 50 panes |
|---------|------------|-------------|-------------|
| WebView2 memory | ~200MB | ~800MB | ~2GB (impractical) |
| Process detection polling | negligible | ~20ms/poll | ~50ms/poll |
| Output ring buffers | ~2.5KB | ~10KB | ~25KB |
| Sidebar render | instant | instant | scroll performance |

The realistic ceiling is ~10-15 panes across 3-5 tabs. Beyond that, WebView2 memory is the bottleneck (one Chromium renderer per pane). This is inherent to the WebView2-based xterm.js approach and not specific to v1.1 changes.

## Sources

- [WinUI 3 Title Bar Customization - Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar) (updated 2026-02-28)
- [TitleBar Control - Windows App SDK 1.7+](https://learn.microsoft.com/en-us/windows/apps/design/controls/title-bar)
- [WebView2 in WinUI 3 - Microsoft Learn](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winui)
- [Creating a Pseudoconsole Session - Microsoft Learn](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
- [GetConsoleProcessList - Microsoft Learn](https://learn.microsoft.com/en-us/windows/console/getconsoleprocesslist)
- [Windows Terminal foreground process propagation - PR #19192](https://github.com/microsoft/terminal/pull/19192)
- [Win32_Process WMI class for process tree walking](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process)

---
*Architecture research for: wcmux v1.1 UI/UX integration*
*Researched: 2026-03-08*
