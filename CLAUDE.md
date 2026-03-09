# wcmux - Project Guidelines

## UI Theme

All UI elements must be dark themed by default. When creating TextBox, ComboBox, or any input controls programmatically, always set `RequestedTheme = ElementTheme.Dark` and use dark background/foreground colors consistent with the app's dark palette (#191919 sidebar, #1e1e1e main background, #2D2D2D active elements, #CCCCCC text).

## Coding Conventions

- C# with WinUI 3 / WinAppSDK
- File-scoped namespaces
- Programmatic UI construction (not XAML-heavy) for dynamic elements
- Event-driven architecture with store pattern (TabStore, AttentionStore, LayoutStore)
- Async methods suffixed with Async
