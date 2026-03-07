using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Wcmux.App.ViewModels;
using Wcmux.Core.Layout;
using Windows.System;

namespace Wcmux.App.Commands;

/// <summary>
/// Keyboard bindings for split, focus movement, resize, and pane close.
/// All commands route through the WorkspaceViewModel so the layout store
/// remains the single source of truth.
///
/// Key bindings (Ctrl+Shift prefix):
///   Split horizontal: Ctrl+Shift+H
///   Split vertical:   Ctrl+Shift+V
///   Close pane:       Ctrl+Shift+W
///   Focus left:       Ctrl+Shift+Left
///   Focus right:      Ctrl+Shift+Right
///   Focus up:         Ctrl+Shift+Up
///   Focus down:       Ctrl+Shift+Down
///   Resize left:      Ctrl+Alt+Left
///   Resize right:     Ctrl+Alt+Right
///   Resize up:        Ctrl+Alt+Up
///   Resize down:      Ctrl+Alt+Down
/// </summary>
public static class PaneCommandBindings
{
    /// <summary>
    /// Attaches pane command keyboard accelerators to the given UIElement
    /// (typically the window root or workspace container).
    /// </summary>
    public static void Attach(UIElement target, WorkspaceViewModel viewModel)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));

        // Split commands
        AddAccelerator(target, VirtualKey.H,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            async () => await viewModel.SplitActivePaneHorizontalAsync());

        AddAccelerator(target, VirtualKey.V,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            async () => await viewModel.SplitActivePaneVerticalAsync());

        // Close pane
        AddAccelerator(target, VirtualKey.W,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            async () => await viewModel.CloseActivePaneAsync());

        // Focus movement
        AddAccelerator(target, VirtualKey.Left,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => { viewModel.FocusLeft(); return Task.CompletedTask; });

        AddAccelerator(target, VirtualKey.Right,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => { viewModel.FocusRight(); return Task.CompletedTask; });

        AddAccelerator(target, VirtualKey.Up,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => { viewModel.FocusUp(); return Task.CompletedTask; });

        AddAccelerator(target, VirtualKey.Down,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => { viewModel.FocusDown(); return Task.CompletedTask; });

        // Resize commands (Ctrl+Alt)
        AddAccelerator(target, VirtualKey.Left,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => { viewModel.ResizeActivePane(Direction.Left); return Task.CompletedTask; });

        AddAccelerator(target, VirtualKey.Right,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => { viewModel.ResizeActivePane(Direction.Right); return Task.CompletedTask; });

        AddAccelerator(target, VirtualKey.Up,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => { viewModel.ResizeActivePane(Direction.Up); return Task.CompletedTask; });

        AddAccelerator(target, VirtualKey.Down,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu,
            () => { viewModel.ResizeActivePane(Direction.Down); return Task.CompletedTask; });
    }

    private static void AddAccelerator(
        UIElement target,
        VirtualKey key,
        VirtualKeyModifiers modifiers,
        Func<Task> handler)
    {
        var accel = new KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers,
        };
        accel.Invoked += async (sender, args) =>
        {
            args.Handled = true;
            await handler();
        };
        target.KeyboardAccelerators.Add(accel);
    }
}
