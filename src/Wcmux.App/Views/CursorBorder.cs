using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace Wcmux.App.Views;

/// <summary>
/// Grid subclass that exposes the ProtectedCursor property for setting
/// resize cursors (EW/NS) on split boundary handles.
/// Border is sealed in WinUI 3, so we use Grid as the base class.
/// </summary>
internal sealed class CursorBorder : Grid
{
    public InputCursor? Cursor
    {
        get => ProtectedCursor;
        set => ProtectedCursor = value;
    }
}
