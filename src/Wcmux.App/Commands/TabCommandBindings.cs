using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Wcmux.App.ViewModels;
using Windows.System;

namespace Wcmux.App.Commands;

/// <summary>
/// Keyboard bindings for tab operations. These are persistent (not
/// re-attached on tab switch) because they operate on TabViewModel.
///
/// Key bindings:
///   New tab:       Ctrl+Shift+T
///   Next tab:      Ctrl+Tab
///   Previous tab:  Ctrl+Shift+Tab
///   Tab 1-8:       Ctrl+1 through Ctrl+8
///   Last tab:      Ctrl+9
/// </summary>
public static class TabCommandBindings
{
    /// <summary>
    /// Attaches tab command keyboard accelerators to the given UIElement.
    /// </summary>
    public static void Attach(UIElement target, TabViewModel viewModel, Func<Task> createTabHandler)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));

        // New tab: Ctrl+Shift+T
        AddAccelerator(target, VirtualKey.T,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            async () => await createTabHandler());

        // Next tab: Ctrl+Tab
        AddAccelerator(target, VirtualKey.Tab,
            VirtualKeyModifiers.Control,
            () => { SwitchToNextTab(viewModel); return Task.CompletedTask; });

        // Previous tab: Ctrl+Shift+Tab
        AddAccelerator(target, VirtualKey.Tab,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => { SwitchToPreviousTab(viewModel); return Task.CompletedTask; });

        // Ctrl+1 through Ctrl+9 for tab index switching
        for (int i = 1; i <= 9; i++)
        {
            var index = i; // capture for closure
            AddAccelerator(target, (VirtualKey)(VirtualKey.Number1 + (i - 1)),
                VirtualKeyModifiers.Control,
                () => { SwitchToTabByIndex(viewModel, index); return Task.CompletedTask; });
        }
    }

    private static void SwitchToNextTab(TabViewModel viewModel)
    {
        var order = viewModel.TabStore.TabOrder;
        if (order.Count <= 1) return;

        var activeId = viewModel.TabStore.ActiveTabId;
        if (activeId is null) return;
        var currentIndex = FindIndex(order, activeId);
        if (currentIndex < 0) return;

        var nextIndex = (currentIndex + 1) % order.Count;
        viewModel.SwitchTab(order[nextIndex]);
    }

    private static void SwitchToPreviousTab(TabViewModel viewModel)
    {
        var order = viewModel.TabStore.TabOrder;
        if (order.Count <= 1) return;

        var activeId = viewModel.TabStore.ActiveTabId;
        if (activeId is null) return;
        var currentIndex = FindIndex(order, activeId);
        if (currentIndex < 0) return;

        var prevIndex = (currentIndex - 1 + order.Count) % order.Count;
        viewModel.SwitchTab(order[prevIndex]);
    }

    private static void SwitchToTabByIndex(TabViewModel viewModel, int oneBasedIndex)
    {
        var order = viewModel.TabStore.TabOrder;
        if (order.Count == 0) return;

        // Ctrl+9 always goes to last tab
        if (oneBasedIndex == 9)
        {
            viewModel.SwitchTab(order[^1]);
            return;
        }

        var index = oneBasedIndex - 1;
        if (index < order.Count)
        {
            viewModel.SwitchTab(order[index]);
        }
    }

    private static int FindIndex(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value) return i;
        }
        return -1;
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
