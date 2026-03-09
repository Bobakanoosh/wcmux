# Phase 5: Pane Title Bars and Browser Panes - Research

**Researched:** 2026-03-08
**Domain:** WinUI 3 pane chrome, Win32 process tree inspection, WebView2 browser hosting
**Confidence:** HIGH

## Summary

Phase 5 adds per-pane title bars showing foreground process names, close/split action buttons, and browser pane hosting alongside terminal panes. This requires three distinct technical domains: (1) foreground process detection via the Win32 ToolHelp32 API to walk the ConPTY process tree, (2) WinUI 3 UI composition to add a title bar strip above each pane with action buttons, and (3) WebView2 browser pane hosting using the existing shared environment from Phase 4.

The codebase already has all the foundations needed. The `LeafNode` record in the layout tree needs a `PaneKind` discriminator (terminal vs browser) so the renderer can instantiate either a `TerminalPaneView` or a new `BrowserPaneView`. The `WorkspaceView` already builds pane containers programmatically in `CreatePaneViewAsync`, which currently creates a `Grid` wrapping a `Border` around a `TerminalPaneView` -- this is where the pane title bar gets inserted. The `ConPtySession` has the process handle needed for ToolHelp32 parent-child walking.

**Primary recommendation:** Structure as two plans: (1) foreground process detection service + pane title bar UI with close/split buttons, (2) browser pane hosting with PaneKind model change and address bar UI.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PBAR-01 | User sees a title bar above each pane showing the foreground process name | ForegroundProcessDetector using ToolHelp32 P/Invoke, polled on timer, displayed in pane title bar Grid |
| PBAR-02 | User can close a pane via an X button in its title bar | Button in pane title bar Grid, routed through existing WorkspaceViewModel.ClosePaneAsync |
| PBAR-03 | User can split a pane horizontally or vertically via icon buttons in the pane title bar | Split buttons in title bar, routed through existing SplitActivePaneAsync (replaces current hover split affordance) |
| PBAR-04 | User can open a browser pane via a button in the pane title bar | PaneKind discriminator on LeafNode, new BrowserPaneView with WebView2 + address bar, split creates browser pane instead of terminal |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| WinUI 3 (WinAppSDK) | existing | UI framework for pane title bars and buttons | Already used throughout the project |
| WebView2 | existing | Browser pane hosting | Already integrated with shared environment (Phase 4) |
| kernel32.dll (ToolHelp32) | Win32 API | Foreground process detection via CreateToolhelp32Snapshot | Roadmap decision: use ToolHelp32 not WMI (~1ms vs 50-200ms) |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Segoe MDL2 Assets | built-in | Icon glyphs for close/split/browser buttons | All pane title bar action buttons |
| Segoe Fluent Icons | built-in | Alternative icon font if MDL2 lacks needed glyphs | Fallback for specific icons |
| DispatcherTimer | WinUI 3 built-in | Periodic foreground process polling | 1-2 second interval polling for process name updates |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ToolHelp32 | WMI (ManagementObjectSearcher) | WMI is 50-200ms per query vs ~1ms for ToolHelp32; unacceptable for per-pane polling |
| ToolHelp32 | NtQueryInformationProcess | Lower-level, undocumented API; ToolHelp32 is the documented Win32 approach |
| Polling timer | ConPTY output hooks | No reliable signal from ConPTY when foreground process changes; polling is necessary |

## Architecture Patterns

### Recommended Project Structure
```
src/
├── Wcmux.Core/
│   ├── Layout/
│   │   └── LayoutNode.cs          # Add PaneKind to LeafNode
│   └── Runtime/
│       └── ForegroundProcessDetector.cs  # NEW: ToolHelp32 P/Invoke wrapper
├── Wcmux.App/
│   ├── Views/
│   │   ├── PaneTitleBar.xaml/.cs   # NEW: Reusable pane title bar control
│   │   ├── BrowserPaneView.xaml/.cs # NEW: WebView2 browser pane
│   │   ├── TerminalPaneView.xaml/.cs # EXISTING: unchanged
│   │   └── WorkspaceView.xaml.cs   # MODIFIED: insert title bars, handle PaneKind
│   └── ViewModels/
│       └── WorkspaceViewModel.cs   # MODIFIED: add SplitAsBrowser method
```

### Pattern 1: Foreground Process Detection via ToolHelp32
**What:** Walk the process tree from ConPTY's shell PID to find the deepest child process (the foreground process). Uses CreateToolhelp32Snapshot + Process32First/Process32Next to build a parent-child map, then walks from shell PID to leaf.
**When to use:** Every 1-2 seconds per pane via a shared polling timer.
**Example:**
```csharp
// Source: Win32 ToolHelp32 API documentation
[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

[DllImport("kernel32.dll")]
static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

[DllImport("kernel32.dll")]
static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

// TH32CS_SNAPPROCESS = 0x00000002
// Take a single snapshot, build Dictionary<parentPid, List<childPid>>,
// then walk from shell PID to deepest child. Return process name sans .exe.
```

### Pattern 2: PaneKind Discriminator on LeafNode
**What:** Add an enum `PaneKind { Terminal, Browser }` to `LeafNode` so the renderer can decide which view to instantiate. The layout tree remains generic -- it just carries the kind tag.
**When to use:** When creating a new pane via split-as-browser action.
**Example:**
```csharp
public enum PaneKind { Terminal, Browser }

public sealed record LeafNode : LayoutNode
{
    public required string PaneId { get; init; }
    public required string SessionId { get; init; }
    public PaneKind Kind { get; init; } = PaneKind.Terminal; // backward compatible
}
```

### Pattern 3: Pane Title Bar as Composed Grid Row
**What:** Insert a 24px-high Grid row above each pane content area. The title bar contains: process name TextBlock (left), action buttons (right: split-h, split-v, browser, close). This replaces the existing inline `titleBlock` and `splitButton` created in `WorkspaceView.CreatePaneViewAsync`.
**When to use:** Every pane (terminal and browser) gets a title bar.
**Example:**
```csharp
// In WorkspaceView.CreatePaneViewAsync, replace current ad-hoc overlay with:
var grid = new Grid { Tag = paneId };
grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });  // title bar
grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content

var titleBar = CreatePaneTitleBar(paneId);
Grid.SetRow(titleBar, 0);

var contentBorder = new Border { Child = paneView, ... };
Grid.SetRow(contentBorder, 1);
```

### Pattern 4: Browser Pane with Address Bar
**What:** A new `BrowserPaneView` UserControl containing a WebView2 for web content (NOT xterm.js). It has an address bar (TextBox + Go button) and back/forward/reload buttons. Uses the shared `WebViewEnvironmentCache` from Phase 4.
**When to use:** When user clicks the browser button in a pane's title bar.
**Example:**
```csharp
// BrowserPaneView layout:
// Row 0: Address bar (28px) - Back, Forward, Reload buttons + URL TextBox
// Row 1: WebView2 browser content (*)
// Uses WebViewEnvironmentCache.GetOrCreateAsync() for shared environment
```

### Anti-Patterns to Avoid
- **Polling per-pane with separate timers:** Use a single DispatcherTimer that iterates all visible panes, not one timer per pane. Multiple timers are wasteful and harder to manage.
- **Storing process name in the layout tree:** The process name is volatile display state, not structural layout data. Keep it in a dictionary on the view layer, not in the immutable `LeafNode` record.
- **Creating a new WebView2 environment for browser panes:** The shared `WebViewEnvironmentCache` from Phase 4 MUST be reused. Browser panes and terminal panes share the same browser process group.
- **Mutating LeafNode.SessionId for browser panes:** Browser panes have no ConPTY session. Use a sentinel value or make SessionId nullable/optional for browser panes.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Process tree walking | Manual PID iteration with Process.GetProcesses() | ToolHelp32 snapshot API | ToolHelp32 is atomic snapshot, ~1ms; GetProcesses() allocates heavily, ~10ms+ |
| Icon buttons | Custom drawn icons | Segoe MDL2 Assets FontIcon | Built-in glyph font, resolution-independent, consistent with WinUI 3 |
| URL validation | Regex-based URL parser | `Uri.TryCreate` with `UriKind.Absolute` | Handles edge cases, protocol prefixing |
| WebView2 navigation events | Manual state tracking | CoreWebView2.NavigationCompleted / SourceChanged events | Built-in events handle redirects, errors, loading states |

**Key insight:** The ToolHelp32 API is the only practical way to detect the foreground process in a ConPTY tree. WMI is too slow for polling, and there is no event-based notification when a child process spawns under ConPTY.

## Common Pitfalls

### Pitfall 1: ToolHelp32 Handle Leak
**What goes wrong:** `CreateToolhelp32Snapshot` returns an `IntPtr` handle that must be closed with `CloseHandle`. Forgetting to close it leaks kernel handles.
**Why it happens:** P/Invoke returns raw IntPtr, no automatic cleanup.
**How to avoid:** Wrap in a `SafeHandle` subclass or use try/finally with `CloseHandle`. Since this runs every 1-2 seconds, a leak would accumulate fast.
**Warning signs:** Process Explorer showing increasing handle count on wcmux.

### Pitfall 2: Process Exited Between Snapshot and Name Query
**What goes wrong:** A process captured in the ToolHelp32 snapshot may have exited by the time you try to read its name. The snapshot itself contains the process name (`szExeFile`), so use that instead of opening the process handle separately.
**Why it happens:** Race condition inherent in process enumeration.
**How to avoid:** Read the process name from `PROCESSENTRY32.szExeFile` in the snapshot, not from `Process.GetProcessById().ProcessName`. The snapshot data is consistent.

### Pitfall 3: ConPTY Shell PID Not Exposed
**What goes wrong:** The `ConPtySession` class currently uses `Process.GetProcessById(processInfo.dwProcessId)` but doesn't expose the PID for external use. The foreground process detector needs the shell PID to walk the tree.
**Why it happens:** ISession interface doesn't include a ProcessId property.
**How to avoid:** Add a `ProcessId` property to `ISession` (or just to `ConPtySession` and cast where needed). The PID is available from `_process.Id` in ConPtySession.

### Pitfall 4: Browser Pane SessionId Mismatch
**What goes wrong:** The layout tree requires `SessionId` on `LeafNode`, but browser panes have no ConPTY session. Using empty string or null causes crashes in `WorkspaceViewModel._paneSessions` lookups.
**Why it happens:** The current model assumes every pane has a terminal session.
**How to avoid:** Use a dedicated sentinel pattern: browser panes get a unique ID prefixed with `browser:` and `WorkspaceViewModel` stores browser state in a separate dictionary. The close/split logic must handle both pane kinds.

### Pitfall 5: Title Bar Height Stealing Space from Terminal
**What goes wrong:** Adding a 24px title bar above each pane reduces the terminal content area. If the pane rect computation doesn't account for this, terminals get incorrect row counts.
**Why it happens:** PaneRect is computed from the full container; the title bar eats into that space.
**How to avoid:** The title bar is INSIDE the pane container (part of the Grid), so the WebView2 control naturally fills the remaining space via `GridLength.Star`. The terminal's fit addon will detect the smaller area and report the correct cols/rows. No change to PaneRect computation needed.

### Pitfall 6: Browser Pane WebView2 Steals Keyboard Focus
**What goes wrong:** WebView2 in browser mode aggressively captures keyboard focus, preventing pane-level keyboard shortcuts (Ctrl+Shift+H for split, etc.) from reaching the app.
**Why it happens:** WebView2 routes all keyboard input to the web content by default.
**How to avoid:** Browser pane's WebView2 needs `AcceleratorKeyPressed` event handling to intercept app-level shortcuts before they reach the web content. The terminal WebView2 already handles this via the `onCommand` callback from xterm.js -- browser panes need an equivalent handler.

## Code Examples

### ToolHelp32 Foreground Process Detection
```csharp
// Source: Win32 API documentation for CreateToolhelp32Snapshot
using System.Runtime.InteropServices;

public static class ForegroundProcessDetector
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Finds the deepest child process name under the given shell PID.
    /// Returns the exe name without extension (e.g., "python", "claude", "bash").
    /// </summary>
    public static string? GetForegroundProcessName(int shellPid)
    {
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return null;

        try
        {
            // Build parent -> children map
            var children = new Dictionary<uint, List<(uint pid, string name)>>();
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };

            if (!Process32First(snapshot, ref entry)) return null;

            do
            {
                if (!children.TryGetValue(entry.th32ParentProcessID, out var list))
                {
                    list = new List<(uint, string)>();
                    children[entry.th32ParentProcessID] = list;
                }
                list.Add((entry.th32ProcessID, entry.szExeFile));
            }
            while (Process32Next(snapshot, ref entry));

            // Walk from shellPid to deepest child
            uint current = (uint)shellPid;
            string name = "";

            while (children.TryGetValue(current, out var kids) && kids.Count > 0)
            {
                // Take first child (most recently spawned is usually the foreground)
                var child = kids[0];
                current = child.pid;
                name = child.name;
            }

            if (string.IsNullOrEmpty(name)) return null;

            // Strip .exe extension
            return Path.GetFileNameWithoutExtension(name);
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }
}
```

### Pane Title Bar Icon Glyphs (Segoe MDL2 Assets)
```csharp
// Useful glyphs for pane title bar buttons:
// Close (X):              \uE711 (Cancel)
// Split Horizontal:       \uE745 (DockBottom) or \uF156 (SplitVertical -- confusing name, looks like horizontal split)
// Split Vertical:         \uE746 (DockRight) or \uF157 (SplitHorizontal)
// Browser/Globe:          \uE774 (Globe)
// Alternatively use Unicode box drawing for split icons
```

### Browser Pane Address Bar Pattern
```csharp
// Address bar with navigation controls:
// [<] [>] [Reload] [________________________URL________________________] [Go]
//
// Key events to handle on CoreWebView2:
// - SourceChanged: update address bar text
// - NavigationCompleted: update back/forward button enabled state
// - CoreWebView2.CanGoBack / CanGoForward for button states
//
// URL normalization: if user types "example.com", prepend "https://"
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| WMI for process queries | ToolHelp32 P/Invoke | Always (WMI was never fast enough) | 50-200x faster process tree walking |
| Overlay text for pane titles | Dedicated title bar row | This phase | Proper hit targets for buttons, clear visual hierarchy |
| Terminal-only panes | PaneKind discriminator | This phase | Enables browser panes alongside terminals |

**Deprecated/outdated:**
- The current inline `titleBlock` overlay (small text at top-left of pane) and hover `splitButton` (bottom-right) in `WorkspaceView.CreatePaneViewAsync` will be replaced by the proper pane title bar.

## Open Questions

1. **Foreground process name for PowerShell**
   - What we know: PowerShell itself is the shell process; when the user runs `python script.py`, python.exe becomes a child process detectable via ToolHelp32.
   - What's unclear: When PowerShell runs a cmdlet (not an external process), there is no child process -- the foreground is still "pwsh". This is expected behavior but worth noting.
   - Recommendation: Show "pwsh" as the default when no child process is found. This matches terminal emulator conventions (Windows Terminal does the same).

2. **Browser pane initial URL**
   - What we know: The user clicks a browser button to open a browser pane.
   - What's unclear: Should it open to a blank page, a default URL, or prompt for a URL?
   - Recommendation: Open to `about:blank` with the address bar focused, ready for the user to type a URL. This is the simplest UX.

3. **Browser pane session model**
   - What we know: Browser panes need to exist in the layout tree but have no ConPTY session.
   - What's unclear: How to handle browser pane lifecycle in WorkspaceViewModel._paneSessions.
   - Recommendation: Make LeafNode.SessionId represent a generic content ID. For browser panes, generate a unique ID prefixed with `browser:` and skip session creation/teardown. The ViewModel needs a separate code path for browser pane creation (no SessionManager involvement).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 |
| Config file | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| Quick run command | `dotnet test tests/Wcmux.Tests --filter "Category=Unit" -v q` |
| Full suite command | `dotnet test tests/Wcmux.Tests -v q` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PBAR-01 | ForegroundProcessDetector walks process tree from shell PID to deepest child | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~ForegroundProcess" -x` | No - Wave 0 |
| PBAR-01 | Pane title bar displays process name (visual) | manual-only | N/A - visual verification | N/A |
| PBAR-02 | ClosePaneAsync removes pane from layout tree | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~LayoutReducerTests" -x` | Yes (existing) |
| PBAR-03 | SplitPane creates correct tree structure | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~SplitCommands" -x` | Yes (existing) |
| PBAR-04 | LeafNode PaneKind discriminator preserved through split/close operations | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~PaneKind" -x` | No - Wave 0 |
| PBAR-04 | Browser pane lifecycle (create, navigate, close) | manual-only | N/A - requires WebView2 runtime | N/A |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Wcmux.Tests --filter "Category=Unit" -v q`
- **Per wave merge:** `dotnet test tests/Wcmux.Tests -v q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Wcmux.Tests/Runtime/ForegroundProcessDetectorTests.cs` -- covers PBAR-01 (process tree walking logic, not P/Invoke itself)
- [ ] `tests/Wcmux.Tests/Layout/PaneKindTests.cs` -- covers PBAR-04 (PaneKind preserved through layout operations)

## Sources

### Primary (HIGH confidence)
- Codebase analysis: `LayoutNode.cs`, `WorkspaceView.xaml.cs`, `ConPtySession.cs`, `WebViewTerminalController.cs`, `WebViewEnvironmentCache.cs`
- Win32 ToolHelp32 API: `CreateToolhelp32Snapshot`, `Process32First`, `Process32Next` -- standard documented Win32 API
- Project decision from ROADMAP.md: "Use ToolHelp32 P/Invoke (not WMI) for foreground process detection (~1ms vs 50-200ms)"
- Project decision from STATE.md: "Merge browser pane hosting with pane title bars phase (both need PaneKind model change)"

### Secondary (MEDIUM confidence)
- Segoe MDL2 Assets glyph codes for UI icons
- WebView2 navigation API patterns (CoreWebView2.SourceChanged, NavigationCompleted)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all libraries already in use in the project
- Architecture: HIGH - direct extension of existing patterns visible in codebase
- Pitfalls: HIGH - derived from concrete code analysis (handle leaks, PID exposure, session model mismatch)
- Process detection: HIGH - ToolHelp32 is well-documented Win32 API, roadmap explicitly chose it

**Research date:** 2026-03-08
**Valid until:** 2026-04-08 (stable domain, no moving targets)
