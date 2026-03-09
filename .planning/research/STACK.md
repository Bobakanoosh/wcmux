# Stack Research: v1.1 UI/UX Overhaul Additions

**Domain:** WinUI 3 terminal multiplexer -- custom chrome, vertical tabs, process detection, browser panes
**Researched:** 2026-03-08
**Confidence:** HIGH (all recommendations verified against official Microsoft docs)

## Scope

This document covers ONLY the new stack additions needed for v1.1. The existing validated stack (WinUI 3 / Windows App SDK 1.8, WebView2, ConPTY, .NET 9, xterm.js, immutable layout reducer) is not re-researched.

## Recommended Stack Additions

### 1. Custom Dark Title Bar

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `Microsoft.UI.Xaml.Controls.TitleBar` | Windows App SDK 1.8 (already installed) | Replace default system title bar with custom dark XAML title bar | New first-party XAML control introduced in SDK 1.7, stable in 1.8. Handles drag regions, caption buttons (min/max/close), icon, and title text automatically. Eliminates the boilerplate of the older `ExtendsContentIntoTitleBar` + `SetTitleBar` approach. |

**No new NuGet packages required.** The `TitleBar` control ships in `Microsoft.WindowsAppSDK 1.8.260209005` which is already referenced.

**Key properties:**
- `Title` -- window title text
- `IconSource` -- app icon
- `Content` -- center content area (could host a search box or breadcrumb later)
- `IsBackButtonVisible` / `IsPaneToggleButtonVisible` -- built-in nav affordances (disable both for wcmux)
- Inherits from `Control` so full template customization is possible via `ControlTemplate`

**Integration approach:**
Replace the current `<Window>` default chrome. In `MainWindow.xaml`, add a `<TitleBar>` as the first row of the root Grid. The control calls `Window.SetTitleBar` internally, so the old manual `ExtendsContentIntoTitleBar` pattern is not needed. Dark theming is achieved via `RequestedTheme="Dark"` on the Application or Window, and the TitleBar control respects it. Caption button colors can be further tuned through `AppWindow.TitleBar` color properties if needed.

**XAML sketch:**
```xml
<Window ...>
  <Grid Background="#1e1e1e">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" /> <!-- TitleBar -->
      <RowDefinition Height="*" />    <!-- Content -->
    </Grid.RowDefinitions>

    <controls:TitleBar x:Name="AppTitleBar"
                       Title="wcmux"
                       IsBackButtonVisible="False"
                       IsPaneToggleButtonVisible="False" />

    <!-- Existing layout below -->
  </Grid>
</Window>
```

**Alternative considered:** The older `ExtendsContentIntoTitleBar = true` + `Window.SetTitleBar(element)` manual approach. This still works but requires manually defining drag regions, handling DPI changes, and styling caption buttons. The new `TitleBar` control wraps all of this. Use the manual approach only if the TitleBar control proves too inflexible for a specific layout need (unlikely for wcmux).

### 2. Vertical Tab Sidebar (with Output Preview)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Custom XAML `UserControl` | N/A | Vertical tab list with preview thumbnails | No suitable built-in vertical tab control exists in WinUI 3. A custom `ListView`-based sidebar is the standard approach for VS Code-style vertical tabs. |
| `CoreWebView2.CapturePreviewAsync` | WebView2 SDK 1.0.3179.45 (already installed) | Capture WebView2 terminal content as thumbnail for output preview | WinUI 3's `RenderTargetBitmap` does NOT capture WebView2 content (renders as black). Use WebView2's own capture API instead. |

**No new NuGet packages required.**

**Critical detail:** `RenderTargetBitmap` cannot capture WebView2 content. This is a well-documented WinUI 3 limitation. For output previews of terminal panes, use `CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream)` to get a snapshot, then display the resulting PNG in an `Image` control in the sidebar.

**Sidebar structure:**
- Custom `UserControl` with a `ListView` bound to the `TabStore`
- Each list item: tab title, last-line-of-output text, cwd path, attention indicator dot
- Positioning: left side of the window, collapsible
- Replaces the current horizontal `TabBarView`

**Output preview implementation options:**

| Approach | Pros | Cons | Recommendation |
|----------|------|------|----------------|
| `CapturePreviewAsync` thumbnail | Actual visual snapshot of terminal | Async, costs GPU, needs periodic refresh | Use on hover or for active tab only |
| Last N lines of text from output pump | Fast, no GPU cost, always current | Not a visual preview, just text | Use for all tabs in sidebar (cheap, always fresh) |

Use text-based preview (last line of output + cwd from `SessionCwdChangedEvent`) for all tabs in the sidebar. Optionally add a `CapturePreviewAsync` thumbnail on hover. This keeps the sidebar performant with many tabs.

**Data source:** The existing `SessionOutputEvent` stream in `ConPtySession` already provides all terminal output. Add a small ring buffer (last ~5 lines) per session to power the text preview without additional infrastructure.

### 3. Foreground Process Detection

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| ToolHelp32 API via P/Invoke | kernel32.dll (all Windows) | Walk the process tree from the ConPTY child PID to find the most-recently-spawned descendant | This is the same approach used by WezTerm and Windows Terminal. No library exists for this; it is a ~50-line P/Invoke pattern. |

**No new NuGet packages required.** This is pure Win32 interop using APIs already available on all supported Windows versions.

**How it works (WezTerm/Windows Terminal pattern):**

1. The ConPTY session already stores the shell process via `Process.GetProcessById(processInfo.dwProcessId)` in `ConPtySession.cs` line 395.
2. To find the foreground process, walk the process tree downward from that PID:
   a. Call `CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)` to snapshot all processes.
   b. Iterate with `Process32First` / `Process32Next` to build a `Dictionary<uint, List<PROCESSENTRY32>>` mapping parent PID to children.
   c. From the shell PID, recursively follow children. The deepest (most recently created) descendant is assumed to be the foreground process.
   d. Read the `szExeFile` field of the leaf `PROCESSENTRY32` for the process name (e.g., "node.exe", "claude.exe", "python.exe").
3. Strip the `.exe` suffix for display in the pane title bar.

**Why ToolHelp32 over alternatives:**

| Approach | Speed | Allocations | Reliability | Verdict |
|----------|-------|-------------|-------------|---------|
| `CreateToolhelp32Snapshot` + `Process32First/Next` | ~1ms | Minimal (stack structs) | Very high, stable Win32 API | **Use this** |
| WMI `Win32_Process` query | 50-200ms | Heavy (COM, managed objects) | WMI service can be slow/unavailable | Avoid |
| `NtQueryInformationProcess` per-PID | ~0.1ms per call but needs handle open | Moderate | Semi-documented API | Overkill for this use case |
| `Process.GetProcesses()` + LINQ | ~10-30ms | Heavy (Process objects for ALL processes) | High | Acceptable fallback |

**Polling strategy:** Poll every 2 seconds per visible pane on a background thread. ToolHelp32 is fast enough that polling all visible panes in a single snapshot is fine. Do NOT use `ManagementEventWatcher` (WMI event subscription) -- it is heavyweight and unreliable.

**ISession interface addition needed:**
```csharp
// Add to ISession:
int ShellProcessId { get; }
```

The `ConPtySession` already has `_process` with the PID (line 395). Expose it so the UI layer can query the process tree.

### 4. Browser Pane Hosting

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `Microsoft.Web.WebView2` | 1.0.3179.45 (already installed) | Render browser panes within the split-tree layout | Already used for terminal rendering. A browser pane is simply a WebView2 navigated to a URL instead of `TerminalWeb/index.html`. |

**No new NuGet packages required.**

**Shared environment:** The app currently calls `EnsureCoreWebView2Async()` without a shared `CoreWebView2Environment` (line 49 of `WebViewTerminalController.cs`). For efficiency with multiple WebView2 instances (terminals + browser panes), create a single shared environment at app startup:

```csharp
// Create once at app startup, store on App or a service
var env = await CoreWebView2Environment.CreateAsync(
    userDataFolder: Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "wcmux", "WebView2Data"));

// Pass to each WebView2 instance
await webView.EnsureCoreWebView2Async(env);
```

This ensures all WebView2 instances share the browser process and renderer processes, reducing memory overhead. The current null-environment approach already shares implicitly, but being explicit avoids edge cases with process cleanup on dispose (a known issue with multiple WebView2 instances in WinUI 3).

**Browser pane vs terminal pane:**
- Terminal panes use `WebViewTerminalController` navigating to `TerminalWeb/index.html`
- Browser panes navigate to a user-provided URL with standard browser settings
- Both are leaf nodes in the same split-tree layout
- The layout reducer needs a pane type discriminator (add a `PaneKind` enum: `Terminal | Browser`)

**Browser pane WebView2 settings (differs from terminal):**

| Setting | Terminal Pane | Browser Pane |
|---------|-------------|-------------|
| `AreDefaultContextMenusEnabled` | `false` | `true` |
| `IsStatusBarEnabled` | `false` | `true` |
| `IsZoomControlEnabled` | `false` | `true` |
| `AreDevToolsEnabled` | `false` (release) | `true` |

**Browser pane title bar content:**
- Back / Forward / Reload buttons
- URL display (read-only or editable)
- Current page title from `CoreWebView2.DocumentTitle`

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `CommunityToolkit.WinUI` | Large dependency for minor utilities. Nothing in v1.1 requires it. | Build the small custom controls (sidebar, pane title bar) directly in XAML. |
| `Microsoft.UI.Xaml.Controls.TabView` | Horizontal-only tab strip. Cannot be rotated to vertical. Replacing the existing custom `TabBarView` with it would constrain the sidebar layout. | Custom `ListView`-based vertical sidebar. |
| `System.Management` (WMI) for process detection | `ManagementObjectSearcher` is 50-200ms per query, allocates heavily, and depends on the WMI service being responsive. | ToolHelp32 snapshot via P/Invoke (~1ms, stack-allocated structs). |
| Third-party process tree libraries | Unnecessary abstraction over ~50 lines of P/Invoke. Adds dependency risk. | Direct kernel32 P/Invoke: `CreateToolhelp32Snapshot` / `Process32First` / `Process32Next`. |
| Separate WebView2 user data folders per pane | Creates process isolation but multiplies memory (each folder spawns new browser process tree). | Single shared `CoreWebView2Environment` for all instances. |
| `RenderTargetBitmap` for terminal thumbnails | Cannot capture WebView2 content (renders as black rectangle). Well-documented WinUI 3 limitation. | `CoreWebView2.CapturePreviewAsync` for visual snapshots, or text-based preview from output buffer. |

## Installation

No new packages needed. The existing `.csproj` already has everything:

```xml
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.260209005" />
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.3179.45" />
```

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| Windows App SDK 1.8 | .NET 9, Windows 10 19041+ | `TitleBar` control requires SDK 1.7+. Already on 1.8. |
| WebView2 SDK 1.0.3179.45 | Evergreen WebView2 Runtime | `CapturePreviewAsync` available since early WebView2 versions. Runtime auto-updates on Windows 10/11. |
| ToolHelp32 APIs (`kernel32.dll`) | All Windows versions | Stable Win32 API since Windows 95. No version concerns. |

## Sources

- [TitleBar Class API Reference (Windows App SDK 1.8)](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.titlebar?view=windows-app-sdk-1.8) -- HIGH confidence, official Microsoft docs, updated 2026-02-14
- [Title bar customization guide (Windows App SDK)](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar) -- HIGH confidence, official Microsoft docs, updated 2026-02-28
- [WebView2 Process Model](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-model) -- HIGH confidence, official Microsoft docs
- [WezTerm foreground process detection](https://wezterm.org/config/lua/pane/get_foreground_process_info.html) -- HIGH confidence, documents the "most recently spawned descendant" pattern used on Windows
- [NtQueryInformationProcess (Win32)](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntqueryinformationprocess) -- HIGH confidence, official Win32 docs
- [WebView2 multiple instance issues in WinUI 3](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1433) -- MEDIUM confidence, community-reported, widely corroborated
- [RenderTargetBitmap WebView2 limitation](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1433) -- MEDIUM confidence, community-reported limitation

---
*Stack research for: wcmux v1.1 UI/UX Overhaul*
*Researched: 2026-03-08*
