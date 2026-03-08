# Phase 3: Attention And Windows Integration - Research

**Researched:** 2026-03-08
**Domain:** Terminal bell detection, in-app attention indicators, Windows toast notifications, FlashWindowEx
**Confidence:** HIGH

## Summary

This phase adds attention signaling for background terminal panes. The bell character (0x07) is detected in `TerminalSurfaceBridge`'s output batch loop, stripped before reaching xterm.js, and published as a `SessionBellEvent` through the existing `SessionManager.SessionEventReceived` event bus. An `AttentionTracker` in Wcmux.Core manages per-pane attention state with cooldown, focus-aware suppression, and clearance on focus. The UI layer renders attention with pane dimming/opacity for inactive panes, blinking blue borders for attention panes, and blinking tab text. When the wcmux window lacks OS-level focus, Windows toast notifications fire via `AppNotificationManager` (already available in the Windows App SDK 1.8 dependency), and `FlashWindowEx` flashes the taskbar icon.

The project's existing architecture -- immutable records, event-based state, pure reducers, code-behind rendering -- maps cleanly to this feature. The bell detection fits into the existing output batching loop, attention state fits as a new store alongside `LayoutStore`/`TabStore`, and UI updates follow the established `DispatcherQueue.TryEnqueue` pattern.

**Primary recommendation:** Add `AttentionStore` to Wcmux.Core with pure state transitions, detect bell in `TerminalSurfaceBridge.RunOutputBatchLoop`, and use `AppNotificationManager` from Windows App SDK (already referenced) for toast notifications.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Bell character (0x07) is the only attention trigger -- no output-after-silence or pattern matching.
- Bell is detected in TerminalSurfaceBridge during output batching, before data reaches xterm.js.
- Bell character is stripped from output before sending to xterm.js -- wcmux owns the full notification experience.
- 5-second cooldown per pane between repeated bell triggers to debounce rapid bells.
- Bells from the currently focused pane are suppressed entirely -- attention only matters for background panes.
- Non-active panes are dimmed (reduced opacity on entire pane content including terminal) to make the active pane visually prominent.
- Panes with attention state get a blinking blue border -- blinks 3-5 times, then holds steady blue.
- Active pane is indicated by being undimmed (no colored border needed since dimming handles differentiation).
- Attention border clears on pane focus -- pane becomes the undimmed active pane.
- Tabs with any attention pane get blinking tab text -- same blink-then-steady pattern as pane borders.
- Tab attention indicator clears only when ALL attention panes in that tab have been individually focused and cleared.
- Tab blinking stops after 3-5 blinks, then text stays in attention state (steady) until cleared.
- Windows toast notifications fire only when the wcmux window does not have OS-level focus.
- Toast content shows tab name + pane title (e.g., "wcmux -- Tab: project-x -- ~/src/app").
- Clicking a toast deep-links: activates wcmux window, switches to the correct tab, focuses the specific pane.
- Taskbar icon flashes via FlashWindowEx alongside the toast notification.
- Pending toasts in Windows Action Center auto-dismissed when wcmux regains OS focus.
- Attention state on a pane clears the moment the pane receives focus.
- Only the focused pane clears -- other attention panes in the same tab retain their state until individually focused.
- Tab-level attention persists until all its attention panes are cleared.
- Taskbar flashing stops naturally on window activation (standard FlashWindowEx behavior).

### Claude's Discretion
- Exact blink animation timing and easing (CSS/XAML animation details).
- Exact opacity level for dimmed non-active panes.
- Exact blue color value for attention border.
- Toast notification icon and styling.
- Whether to play a system sound alongside the toast.
- Internal architecture of the attention tracker (event types, state storage).

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| NOTF-01 | User can see when a pane or tab needs attention through in-app unread or attention indicators. | Bell detection in TerminalSurfaceBridge, AttentionStore state management, pane opacity/border rendering in WorkspaceView, tab text rendering in TabBarView |
| NOTF-02 | User receives a Windows desktop notification when a non-focused session needs attention. | AppNotificationManager from Windows App SDK, FlashWindowEx P/Invoke, Window.Activated event for focus tracking |
| NOTF-03 | User can receive attention notifications from generic terminal sessions rather than from a single hard-coded AI tool. | Bell character (0x07) is a standard terminal escape -- works with any program that rings the bell, no AI-tool-specific logic |
</phase_requirements>

## Standard Stack

### Core (Already in Project)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.WindowsAppSDK | 1.8.260209005 | Toast notifications via `AppNotificationManager`, window management | Already referenced in Wcmux.App.csproj |
| Microsoft.Web.WebView2 | 1.0.3179.45 | Terminal rendering surface | Already referenced |
| xunit | 2.9.2 | Test framework | Already referenced in Wcmux.Tests.csproj |

### Supporting (No New Dependencies Needed)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Windows.AppNotifications` | (in WindowsAppSDK) | `AppNotificationManager`, `AppNotificationBuilder` | Toast notifications |
| `WinRT.Interop.WindowNative` | (in WindowsAppSDK) | Get HWND from WinUI Window | FlashWindowEx P/Invoke |
| `user32.dll` (P/Invoke) | Win32 | `FlashWindowEx`, `GetForegroundWindow` | Taskbar flash |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AppNotificationManager | Windows.UI.Notifications (UWP) | UWP API is older, AppNotificationManager is the Windows App SDK replacement -- use the modern API |
| FlashWindowEx P/Invoke | None | No managed alternative exists for taskbar flashing |

**Installation:** No new packages required. All APIs are available through the existing `Microsoft.WindowsAppSDK` reference.

## Architecture Patterns

### Recommended Project Structure
```
src/Wcmux.Core/
  Runtime/
    SessionEvent.cs           # Add SessionBellEvent record
    AttentionStore.cs          # NEW: per-pane attention state, cooldown, clearance
  Terminal/
    TerminalSurfaceBridge.cs   # Add bell detection + stripping in RunOutputBatchLoop

src/Wcmux.App/
  Notifications/
    NotificationService.cs     # NEW: toast notifications, FlashWindowEx, activation handling
  Views/
    WorkspaceView.xaml.cs      # Add opacity dimming + attention border rendering
    TabBarView.xaml.cs         # Add attention text styling
  MainWindow.xaml.cs           # Add window focus tracking, notification init/teardown

tests/Wcmux.Tests/
  Runtime/
    AttentionStoreTests.cs     # NEW: cooldown, suppression, clearance logic
  Terminal/
    BellDetectionTests.cs      # NEW: bell detection and stripping in bridge
```

### Pattern 1: Bell Detection in Output Batch Loop
**What:** Scan batched output for 0x07 bytes before sending to surface. Strip bells from output. Fire a callback/event when bell found.
**When to use:** Every output batch cycle in `RunOutputBatchLoop`.
**Example:**
```csharp
// In TerminalSurfaceBridge.RunOutputBatchLoop, after draining queue into sb:
var batch = sb.ToString();
bool hasBell = batch.Contains('\u0007');
if (hasBell)
{
    batch = batch.Replace("\u0007", "");
    BellDetected?.Invoke();  // event Action
}
if (batch.Length > 0)
{
    await _writeToSurface(batch);
}
```

### Pattern 2: AttentionStore (Pure State in Core)
**What:** Immutable attention state per pane, managed by a store following LayoutStore/TabStore patterns.
**When to use:** Central attention state tracking.
**Example:**
```csharp
// Wcmux.Core.Runtime.AttentionStore
public sealed class AttentionStore
{
    private readonly Dictionary<string, AttentionState> _paneAttention = new();
    private readonly Dictionary<string, DateTimeOffset> _lastBellTime = new();
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    public event Action<string>? AttentionChanged; // paneId

    public bool HasAttention(string paneId)
        => _paneAttention.TryGetValue(paneId, out var state) && state.IsActive;

    public bool TabHasAttention(IEnumerable<string> paneIds)
        => paneIds.Any(HasAttention);

    public void RaiseBell(string paneId, string activePaneId, DateTimeOffset now)
    {
        if (paneId == activePaneId) return; // suppress focused pane
        if (_lastBellTime.TryGetValue(paneId, out var last) && now - last < Cooldown) return;
        _lastBellTime[paneId] = now;
        _paneAttention[paneId] = new AttentionState(true, now);
        AttentionChanged?.Invoke(paneId);
    }

    public void ClearAttention(string paneId)
    {
        if (_paneAttention.Remove(paneId))
            AttentionChanged?.Invoke(paneId);
    }
}

public record AttentionState(bool IsActive, DateTimeOffset RaisedAt);
```

### Pattern 3: Window Focus Tracking
**What:** Track whether the wcmux window has OS-level focus using `Window.Activated` event.
**When to use:** Deciding whether to fire toast notifications.
**Example:**
```csharp
// In MainWindow constructor or init:
Activated += (sender, args) =>
{
    _isWindowFocused = args.WindowActivationState != WindowActivationState.Deactivated;
    if (_isWindowFocused)
    {
        // Dismiss pending toasts from Action Center
        _ = AppNotificationManager.Default.RemoveAllAsync();
    }
};
```

### Pattern 4: Toast Notification with Deep-Link Arguments
**What:** Build toast with arguments encoding tab ID and pane ID for deep-linking on click.
**When to use:** When raising attention while window is unfocused.
**Example:**
```csharp
// NotificationService.cs
public void ShowAttentionToast(string tabId, string tabLabel, string paneId, string paneTitle)
{
    var notification = new AppNotificationBuilder()
        .AddArgument("action", "focusPane")
        .AddArgument("tabId", tabId)
        .AddArgument("paneId", paneId)
        .AddText($"wcmux -- Tab: {tabLabel}")
        .AddText(paneTitle)
        .BuildNotification();

    notification.Tag = paneId;    // allows removal by pane
    notification.Group = tabId;   // allows removal by tab

    AppNotificationManager.Default.Show(notification);
}
```

### Pattern 5: FlashWindowEx P/Invoke
**What:** Flash the taskbar icon when sending a toast notification.
**When to use:** Alongside toast notification when window is unfocused.
**Example:**
```csharp
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

[StructLayout(LayoutKind.Sequential)]
private struct FLASHWINFO
{
    public uint cbSize;
    public IntPtr hwnd;
    public uint dwFlags;
    public uint uCount;
    public uint dwTimeout;
}

private const uint FLASHW_ALL = 3;       // flash caption + taskbar
private const uint FLASHW_TIMERNOFG = 12; // flash until foreground

public static void FlashTaskbar(IntPtr hwnd)
{
    var fi = new FLASHWINFO
    {
        cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
        hwnd = hwnd,
        dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
        uCount = 0,
        dwTimeout = 0
    };
    FlashWindowEx(ref fi);
}
```

### Pattern 6: Pane Opacity and Attention Border Animation
**What:** Dim non-active panes, add blinking blue border for attention panes.
**When to use:** When rendering pane layout and on attention state changes.
**Example:**
```csharp
// In WorkspaceView - update pane visual state
private void UpdatePaneVisualState(string paneId, Border border, Grid container)
{
    var isActive = paneId == _viewModel?.LayoutStore.ActivePaneId;
    var hasAttention = _attentionStore?.HasAttention(paneId) ?? false;

    // Opacity: active=1.0, inactive=0.5 (Claude's discretion: 0.4-0.6 range)
    container.Opacity = isActive ? 1.0 : 0.5;

    if (isActive)
    {
        border.BorderBrush = new SolidColorBrush(
            Windows.UI.Color.FromArgb(0, 0, 0, 0)); // no border
    }
    else if (hasAttention)
    {
        border.BorderBrush = new SolidColorBrush(
            Windows.UI.Color.FromArgb(255, 50, 130, 240)); // attention blue
        StartBlinkAnimation(border); // blink 3-5 times, then steady
    }
    else
    {
        border.BorderBrush = _inactiveBorderBrush;
    }
}
```

### Anti-Patterns to Avoid
- **Polling for bell character:** Do not add a separate polling mechanism. Bell detection must happen inline in the existing `RunOutputBatchLoop`.
- **Storing attention state in views:** Attention state belongs in Wcmux.Core (testable), not in the UI layer.
- **Hard-coding AI tool names:** Bell is a generic terminal feature. Never check process name or output patterns.
- **Firing toasts without cooldown:** Always respect the 5-second cooldown or users get toast storms.
- **Using DispatcherTimer for blink:** Use code-behind animation loop or CSS animation in WebView. DispatcherTimer adds unnecessary complexity.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Toast notifications | Custom Win32 notification window | `AppNotificationManager` + `AppNotificationBuilder` | Handles Action Center integration, deep-link activation, auto-dismiss, icon sourcing |
| Taskbar flashing | Custom overlay or tray icon | `FlashWindowEx` P/Invoke | OS-native, stops automatically on window activation |
| Notification activation routing | Custom IPC/protocol handler | `AppNotificationManager.NotificationInvoked` event | Handles both cold-start and in-process activation |
| Toast dismissal on focus | Manual toast tracking + removal | `AppNotificationManager.RemoveAllAsync()` on window activation | Single async call clears all pending toasts |

**Key insight:** Windows App SDK 1.8 (already referenced) includes the complete `Microsoft.Windows.AppNotifications` namespace. No new NuGet packages needed. The unpackaged app path requires only `Register()` / `Unregister()` calls -- no manifest changes needed since `WindowsPackageType` is `None`.

## Common Pitfalls

### Pitfall 1: Toast Registration Order
**What goes wrong:** Notifications silently fail or launch new app instances.
**Why it happens:** `NotificationInvoked` must be subscribed before calling `Register()`. Calling `Register()` first means click-activation spawns a new process.
**How to avoid:** In `App.xaml.cs` startup: subscribe `NotificationInvoked`, then call `Register()`. On shutdown: call `Unregister()`.
**Warning signs:** Clicking a toast opens a second wcmux window.

### Pitfall 2: Bell Character in Base64 Output
**What goes wrong:** Bell character passes through to xterm.js and triggers browser-level beep.
**Why it happens:** Output is batched as string, then base64-encoded for WebView2. If bell is not stripped before encoding, xterm.js will process it.
**How to avoid:** Strip 0x07 from the batched string in `RunOutputBatchLoop` before calling `_writeToSurface`.
**Warning signs:** System beep sounds from the app when bells fire.

### Pitfall 3: UI Thread Marshaling for Attention Updates
**What goes wrong:** Attention state changes raise events on background threads, causing UI thread violations.
**Why it happens:** `TerminalSurfaceBridge.RunOutputBatchLoop` runs on a `LongRunning` task. Bell detection fires from that thread.
**How to avoid:** Fire `BellDetected` from the batch loop thread. The subscriber (wiring in MainWindow/WorkspaceView) must marshal to UI thread via `DispatcherQueue.TryEnqueue` before updating visuals.
**Warning signs:** `System.Runtime.InteropServices.COMException` on UI updates.

### Pitfall 4: Multiple Deactivation Events
**What goes wrong:** Toast dismissal fires multiple times, or focus state oscillates.
**Why it happens:** WinUI 3 may raise multiple `Deactivated` events without corresponding `Activated` events (documented behavior during alt-tab, virtual desktop switches).
**How to avoid:** Track `_isWindowFocused` as a boolean. Only act on transitions (false->true for dismiss, true->false for enable toasts).
**Warning signs:** Toast removal fails with "already removed" or duplicate toasts appear.

### Pitfall 5: Unpackaged App Toast Icon
**What goes wrong:** Toast shows default Windows app icon instead of wcmux icon.
**Why it happens:** Unpackaged apps source toast icons from the shortcut, then from the exe resource. Without either, the default icon is used.
**How to avoid:** Ensure the exe has an embedded icon resource (already likely via `app.manifest` and Assets). Optionally set `SetAppLogoOverride` with a file:// URI to an icon in the output directory.
**Warning signs:** Generic Windows icon on toast notifications.

### Pitfall 6: Cooldown State for Closed Panes
**What goes wrong:** Memory leak from accumulating cooldown timestamps for closed panes.
**Why it happens:** `AttentionStore` tracks `_lastBellTime` per pane ID but never cleans up.
**How to avoid:** When a pane is closed (`ClosePane`), also call `AttentionStore.RemovePane(paneId)` to clean up all state.
**Warning signs:** Growing memory over long sessions.

## Code Examples

### Bell Detection in TerminalSurfaceBridge (Verified Pattern)
```csharp
// Source: Existing RunOutputBatchLoop pattern in TerminalSurfaceBridge.cs
// Modified to detect and strip bell character

public event Action? BellDetected;

private async Task RunOutputBatchLoop(CancellationToken ct)
{
    var sb = new StringBuilder(4096);

    while (!ct.IsCancellationRequested)
    {
        try { await Task.Delay(BatchIntervalMs, ct); }
        catch (OperationCanceledException) { break; }

        sb.Clear();
        while (_outputQueue.TryDequeue(out var chunk))
            sb.Append(chunk);

        if (sb.Length == 0) continue;

        // Detect and strip bell character before surface delivery
        var batch = sb.ToString();
        if (batch.Contains('\u0007'))
        {
            batch = batch.Replace("\u0007", "");
            BellDetected?.Invoke();
        }

        TotalOutputBatches++;
        if (batch.Length > 0)
        {
            try { await _writeToSurface(batch); }
            catch { }
        }
    }
}
```

### SessionBellEvent Record (Following Existing Pattern)
```csharp
// Source: Existing SessionEvent.cs pattern
public sealed record SessionBellEvent(string SessionId) : SessionEvent(SessionId);
```

### Notification Registration for Unpackaged App
```csharp
// Source: Microsoft Learn - App notifications quickstart
// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart

using Microsoft.Windows.AppNotifications;

// In App.xaml.cs OnLaunched or MainWindow init:
var notificationManager = AppNotificationManager.Default;
notificationManager.NotificationInvoked += OnNotificationInvoked;
notificationManager.Register();

// On shutdown:
AppNotificationManager.Default.Unregister();

// Handler:
private void OnNotificationInvoked(AppNotificationManager sender,
    AppNotificationActivatedEventArgs args)
{
    // Parse arguments to deep-link
    var tabId = args.Arguments["tabId"];
    var paneId = args.Arguments["paneId"];
    // Marshal to UI thread, switch tab, focus pane
}
```

### Getting HWND for FlashWindowEx
```csharp
// Source: Microsoft Learn - Retrieve a window handle
// https://learn.microsoft.com/en-us/windows/apps/develop/ui/retrieve-hwnd
var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this); // 'this' = Window
```

### Dismiss All Toasts on Window Focus
```csharp
// Source: AppNotificationManager API
// https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.appnotificationmanager
await AppNotificationManager.Default.RemoveAllAsync();
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ToastNotificationManager` (UWP) | `AppNotificationManager` (Windows App SDK) | Windows App SDK 1.0 (2022) | Use `Microsoft.Windows.AppNotifications` namespace, not `Windows.UI.Notifications` |
| COM activator for unpackaged toasts | `AppNotificationManager.Register()` | Windows App SDK 1.0 | No COM registration needed for unpackaged apps using Windows App SDK |
| `ToastContentBuilder` (Community Toolkit) | `AppNotificationBuilder` (Windows App SDK) | Windows App SDK 1.0 | Built-in builder, no extra NuGet needed |

**Deprecated/outdated:**
- `Windows.UI.Notifications.ToastNotificationManager`: Use `Microsoft.Windows.AppNotifications.AppNotificationManager` instead.
- `Microsoft.Toolkit.Uwp.Notifications` NuGet: Replaced by built-in `AppNotificationBuilder`.

## Open Questions

1. **Blink Animation Implementation**
   - What we know: WinUI 3 supports `Storyboard` animations and `DispatcherTimer`. The pane borders are created in code-behind.
   - What's unclear: Whether `Storyboard` on dynamically created `Border` elements works reliably in WinUI 3 without XAML templates.
   - Recommendation: Use a simple `DispatcherTimer`-based approach: toggle border brush opacity 3-5 times at 500ms intervals, then leave steady. Simpler than Storyboard for code-behind-created elements.

2. **Toast Sound**
   - What we know: Toasts play system notification sound by default.
   - What's unclear: Whether users will find the sound annoying alongside the visual indicators.
   - Recommendation: Leave default sound on (Claude's discretion). Users can mute app notifications in Windows Settings.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 |
| Config file | tests/Wcmux.Tests/Wcmux.Tests.csproj |
| Quick run command | `dotnet test tests/Wcmux.Tests --filter "Category=Attention" -x` |
| Full suite command | `dotnet test tests/Wcmux.Tests` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| NOTF-01a | Bell detection strips 0x07 and fires event | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~BellDetection"` | No - Wave 0 |
| NOTF-01b | AttentionStore raises attention on bell, respects cooldown | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | No - Wave 0 |
| NOTF-01c | AttentionStore suppresses bell from focused pane | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | No - Wave 0 |
| NOTF-01d | AttentionStore clears attention on pane focus | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | No - Wave 0 |
| NOTF-01e | Tab-level attention persists until all panes cleared | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~AttentionStore"` | No - Wave 0 |
| NOTF-02 | Toast fires when window unfocused, suppressed when focused | manual-only | N/A - requires OS-level focus state | N/A |
| NOTF-02b | Toast deep-link navigates to correct tab and pane | manual-only | N/A - requires toast click interaction | N/A |
| NOTF-03 | Bell from any shell triggers attention (generic) | unit | `dotnet test tests/Wcmux.Tests --filter "FullyQualifiedName~BellDetection"` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Wcmux.Tests --filter "Category=Attention"` (once test files exist)
- **Per wave merge:** `dotnet test tests/Wcmux.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Wcmux.Tests/Runtime/AttentionStoreTests.cs` -- covers NOTF-01b, NOTF-01c, NOTF-01d, NOTF-01e
- [ ] `tests/Wcmux.Tests/Terminal/BellDetectionTests.cs` -- covers NOTF-01a, NOTF-03

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn - App notifications quickstart](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart) - AppNotificationManager registration, builder, activation handling, removal
- [Microsoft Learn - Retrieve a window handle](https://learn.microsoft.com/en-us/windows/apps/develop/ui/retrieve-hwnd) - HWND retrieval via WinRT.Interop.WindowNative
- [Microsoft Learn - Window.Activated Event](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.window.activated) - Window activation/deactivation tracking
- [Microsoft Learn - AppNotificationManager.RemoveAllAsync](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.appnotifications.appnotificationmanager.removeallasync) - Clearing notifications from Action Center
- [pinvoke.net - FlashWindowEx](https://www.pinvoke.net/default.aspx/user32.FlashWindowEx) - P/Invoke signature and flags
- Wcmux.App.csproj - Verified `Microsoft.WindowsAppSDK 1.8.260209005` and `WindowsPackageType=None` (unpackaged)

### Secondary (MEDIUM confidence)
- [Microsoft Learn - Unpackaged app toast notifications](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/send-local-toast-other-apps) - Unpackaged-specific registration path
- [WindowsAppSDK Notifications spec](https://github.com/microsoft/WindowsAppSDK/blob/main/specs/AppNotifications/AppNotifications-spec.md) - API design rationale

### Tertiary (LOW confidence)
- Blink animation approach (DispatcherTimer vs Storyboard) -- no authoritative source for code-behind WinUI 3 animation best practices; recommendation based on simplicity

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries already in project, APIs verified against official docs
- Architecture: HIGH - Follows established project patterns (event stores, immutable records, code-behind rendering)
- Bell detection: HIGH - 0x07 detection is trivial string operation, insertion point in existing batch loop is clear
- Toast notifications: HIGH - AppNotificationManager API verified for unpackaged apps with Windows App SDK 1.8
- FlashWindowEx: HIGH - Well-documented Win32 P/Invoke, widely used
- Blink animation: MEDIUM - Implementation approach is sound but exact WinUI 3 behavior untested
- Pitfalls: HIGH - Based on official docs warnings and established WinUI 3 patterns

**Research date:** 2026-03-08
**Valid until:** 2026-04-08 (stable APIs, no fast-moving dependencies)
