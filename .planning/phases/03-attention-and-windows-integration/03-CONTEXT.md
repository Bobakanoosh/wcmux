# Phase 3: Attention And Windows Integration - Context

**Gathered:** 2026-03-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Make background sessions visible through generic attention handling and native Windows notifications. Users can see when panes and tabs need attention via in-app indicators, and receive Windows desktop notifications when the app is unfocused. Attention triggers are generic (terminal bell), not hard-coded to any specific AI tool. Workspace persistence, automation, and browser surfaces remain out of scope.

</domain>

<decisions>
## Implementation Decisions

### Attention triggers
- Bell character (0x07) is the only attention trigger — no output-after-silence or pattern matching.
- Bell is detected in TerminalSurfaceBridge during output batching, before data reaches xterm.js.
- Bell character is stripped from output before sending to xterm.js — wcmux owns the full notification experience.
- 5-second cooldown per pane between repeated bell triggers to debounce rapid bells.
- Bells from the currently focused pane are suppressed entirely — attention only matters for background panes.

### In-app pane indicators
- Non-active panes are dimmed (reduced opacity on entire pane content including terminal) to make the active pane visually prominent.
- Panes with attention state get a blinking blue border — blinks 3-5 times, then holds steady blue.
- Active pane is indicated by being undimmed (no colored border needed since dimming handles differentiation).
- Attention border clears on pane focus — pane becomes the undimmed active pane.

### In-app tab indicators
- Tabs with any attention pane get blinking tab text — same blink-then-steady pattern as pane borders.
- Tab attention indicator clears only when ALL attention panes in that tab have been individually focused and cleared.
- Tab blinking stops after 3-5 blinks, then text stays in attention state (steady) until cleared.

### Desktop notifications
- Windows toast notifications fire only when the wcmux window does not have OS-level focus.
- Toast content shows tab name + pane title (e.g., "wcmux — Tab: project-x — ~/src/app").
- Clicking a toast deep-links: activates wcmux window, switches to the correct tab, focuses the specific pane.
- Taskbar icon flashes via FlashWindowEx alongside the toast notification.
- Pending toasts in Windows Action Center auto-dismissed when wcmux regains OS focus.

### Dismissal & sync
- Attention state on a pane clears the moment the pane receives focus.
- Only the focused pane clears — other attention panes in the same tab retain their state until individually focused.
- Tab-level attention persists until all its attention panes are cleared.
- Taskbar flashing stops naturally on window activation (standard FlashWindowEx behavior).

### Claude's Discretion
- Exact blink animation timing and easing (CSS/XAML animation details).
- Exact opacity level for dimmed non-active panes.
- Exact blue color value for attention border.
- Toast notification icon and styling.
- Whether to play a system sound alongside the toast.
- Internal architecture of the attention tracker (event types, state storage).

</decisions>

<specifics>
## Specific Ideas

- Active pane should decrease the opacity of non-active panes rather than using colored borders for active state.
- Attention panes have a blue, blinking border — distinct from the dimming-based active/inactive differentiation.
- Blink-then-steady pattern prevents persistent visual noise from long-running background tasks.
- Deep-linking from toast to exact pane makes the notification actionable, not just informational.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SessionEvent` record hierarchy (Wcmux.Core): Extensible with new event types like `SessionBellEvent`. Already flows through `SessionManager.SessionEventReceived`.
- `TerminalSurfaceBridge` (Wcmux.Core): Already parses OSC 7 sequences in output batching loop — same spot can detect 0x07 bell and strip it.
- `LayoutStore.ActivePaneId` / `TabStore.ActiveTabId`: Focus tracking already in place for suppression logic.
- `LayoutStore.FocusHistory`: Stack of recently focused panes, useful for determining which pane to clear.
- `WorkspaceView.UpdateActivePaneHighlight()`: Existing pane border color update logic — can be extended for attention colors and opacity.

### Established Patterns
- Event-based architecture with `event Action<T>` callbacks — attention events should follow this pattern.
- Immutable record types for state — attention state records should be immutable.
- Pure reducer transitions for state changes — attention state changes should be deterministic.
- Code-behind rendering from computed state — views react to state changes via events.

### Integration Points
- `TerminalSurfaceBridge.RunOutputBatchLoop()`: Detection point for bell character in output stream.
- `SessionManager.SessionEventReceived`: Central event bus — new attention events publish here.
- `WorkspaceView`: Pane border rendering and opacity changes.
- `TabBarView`: Tab text rendering for blinking indicators.
- `MainWindow`: Window-level focus tracking for toast suppression logic.
- `Wcmux.App.csproj`: WinUI 3 SDK already includes Windows.UI.Notifications for toast support.

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---
*Phase: 03-attention-and-windows-integration*
*Context gathered: 2026-03-08*
