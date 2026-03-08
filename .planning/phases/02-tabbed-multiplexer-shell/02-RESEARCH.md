# Phase 2: Tabbed Multiplexer Shell - Research

**Researched:** 2026-03-07
**Domain:** WinUI 3 tabbed layout, state management, shell/core separation
**Confidence:** HIGH

## Summary

Phase 2 adds tabbed multiplexing to wcmux. The existing codebase has a clean separation: `LayoutStore` owns a single split tree, `WorkspaceViewModel` orchestrates pane commands and session creation, and `WorkspaceView` renders from computed `PaneRects`. The core architectural task is lifting the single-tab assumption so that each tab owns its own `LayoutStore` and pane-to-session mapping, while the shared `SessionManager` remains global.

The tab bar UI is a straightforward WinUI 3 custom control (not `TabView` from WinUI -- see rationale below). Pane metadata (cwd-based titles in borders) is already partially supported: `ISession.LastKnownCwd` and `TerminalSurfaceBridge.CwdChanged` exist, they just need to be surfaced in the border area of each pane. The shell/core boundary work means introducing a `TabStore` or `TabManager` in `Wcmux.Core` that holds the collection of per-tab layout stores and active tab tracking, keeping the WinUI App layer as a rendering shell.

**Primary recommendation:** Introduce a `TabStore` in `Wcmux.Core` that owns `Dictionary<string, LayoutStore>` plus active-tab ID, and a `TabViewModel` in `Wcmux.App` that wraps `TabStore` + `SessionManager` for tab lifecycle commands. The existing `WorkspaceViewModel` becomes per-tab (one instance per tab), and `MainWindow` switches between them.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- Tab bar sits at the top of the window, below the title bar, above the workspace area.
- Each tab shows a close (X) button directly on the tab -- always visible, not hover-only.
- A [+] button at the end of the tab row creates a new tab. Keyboard shortcut also available.
- Tab labels default to the working directory of the tab's first pane at creation time.
- Tab labels are static -- they do not update as panes change cwd or focus.
- Users can double-click a tab label to rename it inline. Once renamed, the custom name sticks.
- New tabs open with a single pane in the user's home directory (not inherited from the source tab).
- All sessions in inactive tabs keep running in the background -- fully alive, not detached.
- Closing the last pane in a tab closes the tab itself.
- Closing the last tab exits the app.
- Pane identity is displayed in the pane border area -- title text embedded in the border, no extra header bar.
- Pane titles show the current working directory of the shell, updating as the user changes directories.
- Long paths are truncated from the left (e.g., `.../deep/path`).
- Pane cwd does NOT propagate up to the tab label -- tab and pane titles are independent.

### Claude's Discretion
- Shell/core boundary decisions -- what moves to Wcmux.Core vs stays in Wcmux.App for tab state management.
- Exact keyboard shortcut scheme for tab operations (new, close, switch).
- Exact visual styling of the tab bar and pane border titles.
- How cwd tracking is implemented (VT OSC sequences, polling, or session event-based).
- Tab switching animation or transition behavior (if any).

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SESS-03 | User can see identifying metadata for each pane, including a useful title and current session context. | Pane border titles showing cwd via existing `TerminalSurfaceBridge.CwdChanged` event and `ISession.LastKnownCwd`. Border text overlay in `WorkspaceView` pane containers. |
| TABS-01 | User can create a new tab with its own independent pane layout. | `TabStore` in Core owns per-tab `LayoutStore` instances. `TabViewModel` creates new tab with fresh `WorkspaceViewModel` and home-directory session. |
| TABS-02 | User can switch between tabs without disrupting inactive tab layouts. | Tab switching hides/shows `WorkspaceView` instances. Inactive tabs keep their `LayoutStore`, `WorkspaceViewModel`, and all sessions alive. WebView2 panes detach from visual tree but sessions continue. |
| TABS-03 | User can close a tab without affecting sessions in other tabs. | Tab close disposes the tab's `WorkspaceViewModel` (which closes its sessions) but leaves other tabs untouched. `SessionManager` is shared and only closes sessions explicitly requested. |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| WinUI 3 (WinAppSDK) | existing | UI framework, controls, layout | Already in use, native Windows app |
| Wcmux.Core | N/A | Pure state management, no UI deps | Established pattern from Phase 1 |
| xunit | 2.9.2 | Unit testing | Already in use |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| N/A | - | - | No new dependencies needed for Phase 2 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom tab bar | WinUI `TabView` control | `TabView` has a browser-tab feel with drag-to-reorder, tear-off support, and styling that doesn't match a terminal multiplexer aesthetic. Custom tab bar gives full control over the lightweight tmux-like appearance the user wants. |
| Per-tab WorkspaceView instances | Single WorkspaceView with tree swapping | Per-tab instances are simpler: each tab has its own `WorkspaceView` with attached WebView2 panes. Swapping a single view's tree would require detaching/reattaching all WebView2 controls, which is more complex and error-prone. |

## Architecture Patterns

### Recommended Project Structure
```
src/Wcmux.Core/
├── Layout/
│   ├── LayoutNode.cs          # (existing) Split tree nodes
│   ├── LayoutReducer.cs       # (existing) Pure state transitions
│   ├── LayoutStore.cs         # (existing) Observable layout state
│   └── TabStore.cs            # NEW: Tab collection + active tab tracking
├── Runtime/                   # (existing, unchanged)
└── Terminal/                  # (existing, unchanged)

src/Wcmux.App/
├── Commands/
│   ├── PaneCommandBindings.cs # (existing, extended with tab commands)
│   └── TabCommandBindings.cs  # NEW: Keyboard accelerators for tab ops
├── ViewModels/
│   ├── WorkspaceViewModel.cs  # (existing, now per-tab)
│   └── TabViewModel.cs        # NEW: Tab lifecycle orchestration
├── Views/
│   ├── TabBarView.xaml/.cs    # NEW: Tab bar UI control
│   ├── WorkspaceView.xaml/.cs # (existing, minimal changes)
│   └── TerminalPaneView.xaml/.cs # (existing, extended with border title)
└── MainWindow.xaml/.cs        # Modified: hosts TabBarView + tab content area

tests/Wcmux.Tests/
├── Layout/
│   └── TabStoreTests.cs       # NEW: Tab state management tests
└── ...
```

### Pattern 1: TabStore as Core State Owner
**What:** `TabStore` in `Wcmux.Core` manages a collection of tabs, each identified by a string ID and owning a `LayoutStore`. It tracks the active tab ID and fires events on tab changes. It does NOT know about sessions or UI -- it is pure layout state.
**When to use:** All tab state transitions (create, switch, close, rename).
**Example:**
```csharp
// Wcmux.Core.Layout.TabStore
public sealed class TabStore
{
    private readonly Dictionary<string, TabState> _tabs = new();
    private readonly List<string> _tabOrder = new(); // insertion order
    private string _activeTabId;

    public event Action? TabsChanged;
    public event Action<string>? ActiveTabChanged;

    public record TabState(
        string TabId,
        LayoutStore Layout,
        string Label,
        bool IsCustomLabel);

    public string ActiveTabId => _activeTabId;
    public IReadOnlyList<string> TabOrder => _tabOrder.AsReadOnly();
    public TabState? GetTab(string tabId) => _tabs.GetValueOrDefault(tabId);

    public string CreateTab(string tabId, string initialPaneId,
        string initialSessionId, string defaultLabel)
    {
        var layout = new LayoutStore(initialPaneId, initialSessionId);
        _tabs[tabId] = new TabState(tabId, layout, defaultLabel, false);
        _tabOrder.Add(tabId);
        _activeTabId = tabId;
        ActiveTabChanged?.Invoke(tabId);
        TabsChanged?.Invoke();
        return tabId;
    }

    public void SwitchTab(string tabId) { /* ... */ }
    public void CloseTab(string tabId) { /* ... */ }
    public void RenameTab(string tabId, string newLabel) { /* ... */ }
}
```

### Pattern 2: TabViewModel as App-Shell Orchestrator
**What:** `TabViewModel` in `Wcmux.App` wraps `TabStore` + `SessionManager` and manages per-tab `WorkspaceViewModel` instances. It handles the session creation that `TabStore` cannot do (Core has no session deps beyond IDs).
**When to use:** All user-facing tab commands.
**Example:**
```csharp
// Wcmux.App.ViewModels.TabViewModel
public sealed class TabViewModel : IAsyncDisposable
{
    private readonly TabStore _tabStore;
    private readonly SessionManager _sessionManager;
    private readonly Dictionary<string, WorkspaceViewModel> _tabViewModels = new();

    public TabStore TabStore => _tabStore;
    public WorkspaceViewModel? ActiveWorkspace =>
        _tabViewModels.GetValueOrDefault(_tabStore.ActiveTabId);

    public async Task CreateNewTabAsync()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var spec = SessionLaunchSpec.CreateDefaultPowerShell(homeDir);
        var session = await _sessionManager.CreateSessionAsync(spec);
        var tabId = Guid.NewGuid().ToString("N");
        var paneId = Guid.NewGuid().ToString("N");
        var label = TruncatePath(homeDir);

        _tabStore.CreateTab(tabId, paneId, session.SessionId, label);

        var vm = new WorkspaceViewModel(_sessionManager, paneId, session);
        vm.LastPaneClosed += () => HandleLastPaneClosed(tabId);
        _tabViewModels[tabId] = vm;
    }

    public void SwitchTab(string tabId) => _tabStore.SwitchTab(tabId);
    public async Task CloseTabAsync(string tabId) { /* dispose VM, close tab */ }
}
```

### Pattern 3: Tab Switching via Visibility Toggle
**What:** Each tab has its own `WorkspaceView` instance with its WebView2 pane views. Switching tabs hides the current tab's `WorkspaceView` (Visibility.Collapsed) and shows the target tab's view. WebView2 controls remain alive in the visual tree but hidden.
**When to use:** Tab switch operations.
**Why:** WebView2 controls are expensive to create/destroy. Keeping them in the visual tree (collapsed) preserves their state. The underlying ConPTY sessions continue running regardless of UI visibility.

### Pattern 4: Pane Border Titles via TextBlock Overlay
**What:** The existing pane border in `WorkspaceView.CreatePaneViewAsync` already wraps each `TerminalPaneView` in a `Border`. Add a small `TextBlock` overlay at the top of that border showing the pane's cwd. Subscribe to `TerminalSurfaceBridge.CwdChanged` to update dynamically.
**When to use:** SESS-03 pane identity display.
**Example:**
```csharp
// In WorkspaceView.CreatePaneViewAsync, add to the border grid:
var titleBlock = new TextBlock
{
    Text = TruncateCwdFromLeft(session.LastKnownCwd ?? ""),
    FontSize = 11,
    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 200, 200, 200)),
    Margin = new Thickness(8, 0, 0, 0),
    VerticalAlignment = VerticalAlignment.Top,
    HorizontalAlignment = HorizontalAlignment.Left,
    IsHitTestVisible = false,
};
// Subscribe to cwd changes through bridge
```

### Anti-Patterns to Avoid
- **Single WorkspaceViewModel for all tabs:** The existing `WorkspaceViewModel` holds a `LayoutStore` and pane-session mapping. Trying to make it multi-tab by swapping its internal state would be fragile. Use one `WorkspaceViewModel` per tab.
- **Destroying/recreating WebView2 on tab switch:** WebView2 initialization is slow (~200-500ms). Hiding/showing is instant. Never destroy WebView2 controls on tab switch.
- **Putting session lifecycle in Core:** `SessionManager` creates ConPTY processes. `TabStore` in Core should only track IDs, not own sessions. Session creation/teardown stays in the App layer (`TabViewModel`).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Path truncation | Custom string slicing | Helper method with `...` prefix | Edge cases: UNC paths, drive letters, empty strings, single-segment paths |
| Tab ordering | Manual list management | `List<string>` with insertion-order semantics | Need consistent ordering for keyboard Ctrl+Tab navigation and tab bar rendering |
| Keyboard shortcut management | Direct KeyDown handlers | `KeyboardAccelerator` (existing pattern from `PaneCommandBindings`) | Proper modifier key handling, conflict resolution with xterm.js interceptors |

**Key insight:** The tab bar itself is simple enough to hand-build in WinUI (it is a horizontal `StackPanel` with templated tab items). The WinUI `TabView` control is over-engineered for this use case and fights the terminal-multiplexer aesthetic.

## Common Pitfalls

### Pitfall 1: WebView2 Focus Stealing on Tab Switch
**What goes wrong:** When making a previously-collapsed `WorkspaceView` visible, WebView2 controls may not automatically receive keyboard focus, or multiple WebView2 controls may fight for focus.
**Why it happens:** WebView2 focus model is complex -- `Focus(FocusState.Programmatic)` on the WebView2 control plus `term.focus()` inside xterm.js are both needed.
**How to avoid:** After tab switch, explicitly call `FocusTerminalAsync()` on the active pane's `TerminalPaneView` (already implemented). Add a short dispatcher delay if needed.
**Warning signs:** Keyboard input goes nowhere after switching tabs, or goes to wrong pane.

### Pitfall 2: Event Handler Leaks on Tab Close
**What goes wrong:** `WorkspaceViewModel` subscribes to `LayoutStore.LayoutChanged` and `SessionManager.SessionEventReceived`. If not properly unsubscribed on tab close, events from closed tabs fire into disposed objects.
**Why it happens:** C# events hold strong references. Disposal without unsubscription causes leaks and exceptions.
**How to avoid:** `WorkspaceViewModel.DisposeAsync()` already exists. Ensure `TabViewModel.CloseTabAsync()` calls it. Also ensure `WorkspaceView.DetachAsync()` is called before disposing the VM.
**Warning signs:** `ObjectDisposedException` after closing a tab while sessions in other tabs produce output.

### Pitfall 3: Last-Pane-Closed vs Last-Tab-Closed Race
**What goes wrong:** `WorkspaceViewModel.LastPaneClosed` currently triggers `MainWindow.Close()`. With tabs, closing the last pane in a tab should close the tab, not the app. Only closing the last tab should exit.
**Why it happens:** Phase 1 assumed single-tab. The event handler in `MainWindow` directly calls `Close()`.
**How to avoid:** Change `LastPaneClosed` handling: `TabViewModel` listens for it and closes the tab. `TabViewModel` fires its own `LastTabClosed` event that `MainWindow` listens to for app exit.
**Warning signs:** App exits when closing all panes in any tab, even when other tabs exist.

### Pitfall 4: Tab Label Encoding of Home Directory
**What goes wrong:** Tab labels default to the first pane's cwd. On Windows, `Environment.GetFolderPath(UserProfile)` returns something like `C:\Users\Jack`. Need to display a reasonable truncated form.
**Why it happens:** Raw Windows paths are long and ugly for tab labels.
**How to avoid:** Use a path display helper: if path equals home dir, show `~`. Otherwise show the last 2-3 segments. Tab labels are static after creation, so this is a one-time formatting decision.

### Pitfall 5: Container Size Updates on Tab Switch
**What goes wrong:** When a tab's `WorkspaceView` becomes visible after being collapsed, `SizeChanged` may not fire if the container size hasn't changed. Pane rects may be stale or zero-sized.
**Why it happens:** Collapsed controls don't participate in layout. When made visible, they may reuse cached sizes.
**How to avoid:** After making a `WorkspaceView` visible, explicitly call `UpdateContainerSize()` with the current container dimensions. The `SizeChanged` handler may also fire, which is fine (it is idempotent).
**Warning signs:** Panes render at wrong size or don't render at all after switching to a previously inactive tab.

## Code Examples

### Tab Keyboard Shortcuts (Recommended Scheme)
```csharp
// Ctrl+Shift+T: New tab
// Ctrl+Shift+W: Close pane (existing) -- when last pane, closes tab
// Ctrl+Tab: Next tab
// Ctrl+Shift+Tab: Previous tab
// Ctrl+1..9: Switch to tab by index
// Source: Follows Windows Terminal and tmux conventions
```

### Path Truncation Helper
```csharp
public static string TruncateCwdFromLeft(string path, int maxLength = 30)
{
    if (string.IsNullOrEmpty(path)) return "";

    // Replace home directory with ~
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    if (path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
    {
        path = "~" + path[home.Length..];
    }

    if (path.Length <= maxLength) return path;

    // Truncate from left: .../last/segments
    var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var result = segments[^1];
    for (int i = segments.Length - 2; i >= 0; i--)
    {
        var candidate = segments[i] + Path.DirectorySeparatorChar + result;
        if (candidate.Length + 4 > maxLength) // 4 for ".../"
        {
            return ".../" + result;
        }
        result = candidate;
    }
    return result;
}
```

### Tab Bar XAML Structure
```xml
<!-- Lightweight tab bar: StackPanel of tab items + add button -->
<Grid Height="32" Background="#252526">
    <ScrollViewer HorizontalScrollBarVisibility="Hidden"
                  VerticalScrollBarVisibility="Disabled"
                  HorizontalScrollMode="Enabled">
        <StackPanel x:Name="TabStrip" Orientation="Horizontal" />
    </ScrollViewer>
    <Button x:Name="AddTabButton" Content="+"
            HorizontalAlignment="Right"
            Width="32" Height="32"
            Background="Transparent" Foreground="#999" />
</Grid>
```

### CWD Tracking (Already Implemented)
```csharp
// ISession.LastKnownCwd already tracks cwd via OSC 7 sequences
// TerminalSurfaceBridge.CwdChanged event fires when cwd updates
// SessionCwdChangedEvent is dispatched through SessionManager.SessionEventReceived
// No new implementation needed -- just subscribe and display
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single LayoutStore in MainWindow | One LayoutStore per tab via TabStore | Phase 2 | Core state model becomes tab-aware |
| WorkspaceViewModel as singleton | WorkspaceViewModel per tab | Phase 2 | Each tab is an independent workspace |
| MainWindow hosts WorkspaceView directly | MainWindow hosts TabBar + tab content area | Phase 2 | Window layout gains tab management layer |

**Unchanged from Phase 1:**
- `LayoutReducer` pure functions: No changes needed. They operate on a single tree.
- `SessionManager`: Remains global. Sessions are shared resources; pane-to-session mapping is per-tab.
- `TerminalSurfaceBridge`: Remains per-pane. No tab awareness needed.
- `WebViewTerminalController`: Remains per-pane. No tab awareness needed.

## Open Questions

1. **WebView2 Memory Pressure with Many Tabs**
   - What we know: Each WebView2 instance uses a separate renderer process. Many panes across many tabs could consume significant memory.
   - What's unclear: At what point does WebView2 tab count become problematic on typical hardware.
   - Recommendation: Not a Phase 2 blocker. Monitor during testing. If needed, future phases could implement tab "hibernation" (detach WebView2 but keep session alive).

2. **Tab Drag Reordering**
   - What we know: The user did not request drag-to-reorder tabs.
   - What's unclear: Whether the custom tab bar should support this from the start.
   - Recommendation: Do not implement drag reordering in Phase 2. Keep tab order as insertion order. Simpler implementation, can add later.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 |
| Config file | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| Quick run command | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStore" --no-build -v q` |
| Full suite command | `dotnet test tests/Wcmux.Tests -v q` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TABS-01 | Create tab with independent layout | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CreateTab" -v q` | No - Wave 0 |
| TABS-02 | Switch tabs preserves inactive state | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.SwitchTab" -v q` | No - Wave 0 |
| TABS-03 | Close tab without affecting others | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.CloseTab" -v q` | No - Wave 0 |
| SESS-03 | Pane metadata (cwd title display) | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.PaneMetadata" -v q` | No - Wave 0 |
| TABS-01 | Last-pane-closes-tab lifecycle | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.LastPaneClosesTab" -v q` | No - Wave 0 |
| TABS-03 | Last-tab-closes-app lifecycle | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~TabStoreTests.LastTabClosesApp" -v q` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Wcmux.Tests --no-build -v q`
- **Per wave merge:** `dotnet test tests/Wcmux.Tests -v q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Wcmux.Tests/Layout/TabStoreTests.cs` -- covers TABS-01, TABS-02, TABS-03 (core state)
- [ ] Path truncation helper tests -- covers SESS-03 display logic

## Sources

### Primary (HIGH confidence)
- Existing codebase analysis: `LayoutStore.cs`, `WorkspaceViewModel.cs`, `WorkspaceView.xaml.cs`, `TerminalPaneView.xaml.cs`, `MainWindow.xaml.cs` -- direct code reading
- `ISession.LastKnownCwd` and `TerminalSurfaceBridge.CwdChanged` -- cwd tracking already implemented
- `SessionManager` -- shared session lifecycle already supports multi-consumer pattern

### Secondary (MEDIUM confidence)
- WebView2 visibility/collapse behavior -- based on WinUI 3 standard control lifecycle. Collapsed controls stop rendering but WebView2 process stays alive.
- Keyboard accelerator patterns -- verified through existing `PaneCommandBindings.cs` implementation.

### Tertiary (LOW confidence)
- WebView2 memory impact of many tabs -- needs runtime validation, no authoritative source consulted.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new dependencies, extending existing patterns
- Architecture: HIGH - clear decomposition from existing code structure
- Pitfalls: HIGH - derived from direct code analysis of event wiring, focus model, and lifecycle
- CWD tracking: HIGH - already implemented in TerminalSurfaceBridge, just needs UI surfacing

**Research date:** 2026-03-07
**Valid until:** 2026-04-06 (stable -- no external dependency changes expected)
