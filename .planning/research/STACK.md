# Stack Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET | 10 | Primary application runtime for a native-first Windows desktop app | Current LTS, strong Windows tooling, and a smoother path for a first-time native Windows app than C++ while still allowing Win32 interop for ConPTY. |
| Windows App SDK + WinUI 3 | 1.8.5 | Native Windows UI shell, windowing, lifecycle, and notifications | Official Microsoft desktop app stack for modern Windows apps; gives native windowing and app notifications while still working with desktop app scenarios. |
| ConPTY (Windows Pseudoconsole) | Windows 10 1809+ API | True terminal session hosting | This is the Windows-supported PTY layer. If terminal fidelity matters, this is the core primitive, not a shell command wrapper. |
| Rust + Tauri | 2.x | Fallback cross-platform shell if native-first becomes too costly | Tauri keeps Electron off the table, has solid Windows support, official shell/notification plugins, and a clean Rust boundary for system integration. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| WebView2 | Evergreen runtime | Embedded web renderer for any Tauri fallback or hybrid settings UI | Use only if the project chooses Tauri or a web-based auxiliary surface. Not needed for a pure WinUI terminal renderer. |
| `tauri-plugin-shell` | 2 | Process spawning and controlled command execution in Tauri fallback | Use in Tauri builds for launching commands, but not as a substitute for PTY hosting. |
| `tauri-plugin-notification` | 2 | Native desktop notifications in Tauri fallback | Use only in a Tauri variant; note that Windows support is installer-sensitive. |
| `@xterm/xterm` | 5.x | Web terminal renderer for a Tauri fallback | Use only if the product accepts a webview-rendered terminal surface. It is capable, but it is still a browser frontend, not a native terminal widget. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Visual Studio 2022 | Native Windows app development, debugging, packaging | Best fit for WinUI 3 + Windows App SDK workflows. |
| Windows Terminal | Reference product for pane/tab behavior and Windows shell ergonomics | Use as a behavior baseline for splits, tabs, profiles, and shell integration expectations. |
| WiX or MSIX packaging | Installer and app identity | Packaging matters early because Windows notifications and app identity are easier once install flow is real. |

## Installation

```bash
# Native-first path
winget install Microsoft.VisualStudio.2022.Community
winget install Microsoft.DotNet.SDK.10

# Tauri fallback path
cargo install create-tauri-app --locked
npm create tauri-app@latest
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| WinUI 3 + Windows App SDK | WPF | Only if the team wants a more mature .NET desktop stack and is willing to accept older UI primitives. |
| Native ConPTY host | Tauri + xterm.js | Use if shipping quickly matters more than staying fully native and a browser-rendered terminal is acceptable. |
| .NET 10 | C++/Win32 | Use only if the team needs maximum low-level control and is comfortable paying the complexity cost. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Electron | Explicitly rejected, and it conflicts with the project's native-first goal | WinUI 3 first, Tauri second |
| Plain process pipes without ConPTY | You lose real terminal semantics, full-screen TUIs, resize behavior, and shell fidelity | ConPTY-backed sessions |
| Treating `tauri-plugin-shell` as the terminal backend | It helps spawn commands, but it does not itself provide a PTY terminal host | ConPTY bridge behind the UI |
| Locking the architecture to a browser pane from day one | It expands scope before the terminal core is proven | Terminal-first v1, browser later |

## Stack Patterns by Variant

**If native-first remains the priority:**
- Use `.NET 10 + WinUI 3 + Windows App SDK + ConPTY`.
- Because this is the most direct path to a real Windows desktop app with official windowing and notification APIs.

**If delivery speed beats full nativeness:**
- Use `Tauri 2 + Rust + @xterm/xterm + ConPTY bridge`.
- Because it keeps Electron off the table while reducing the amount of custom native UI work.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| Windows App SDK 1.8.5 | Windows 10 version 1809+ and Windows 11 | Official stable channel as of 2026-03-06. |
| .NET 10 | Windows App SDK desktop apps | Natural fit for a C# WinUI 3 shell. |
| Tauri 2 plugins | Tauri 2 | Official plugins follow Tauri major versioning. |
| ConPTY | Windows 10 1809+ | Set project expectations accordingly; older Windows is out. |

## Sources

- Microsoft Learn: Windows App SDK release channels - verified stable channel `1.8.5` and servicing posture
- Microsoft Learn: Windows App SDK overview - verified WinUI 3, windowing, lifecycle, and notifications support
- Microsoft Learn: Creating a Pseudoconsole session - verified that ConPTY is the supported Windows PTY primitive and requires host-managed I/O and rendering
- Microsoft Learn: App notifications overview / desktop notification guides - verified packaged and unpackaged support plus elevated-app limitations
- Microsoft Learn: .NET releases and support / .NET download page - verified that .NET 10 is current LTS
- Tauri 2 official docs - verified Tauri architecture, plugin major versioning, shell plugin, and notification plugin constraints
- xterm.js official repo / npm package page - verified that xterm.js is a browser terminal frontend rather than a native terminal application

---
*Stack research for: Windows-first desktop terminal multiplexer*
*Researched: 2026-03-06*
