using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Wcmux.Core.Runtime;

/// <summary>
/// Wraps the Windows ConPTY (pseudoconsole) API for creating and managing
/// pseudoconsole instances. Handles the Win32 interop boundary so session
/// code can work with managed types only.
/// </summary>
public sealed class ConPtyHost : IDisposable
{
    private SafeFileHandle? _inputReadSide;
    private SafeFileHandle? _outputWriteSide;
    private IntPtr _pseudoConsoleHandle;
    private bool _disposed;

    /// <summary>The pipe the host reads child output from.</summary>
    public SafeFileHandle OutputReadHandle { get; private set; } = null!;

    /// <summary>The pipe the host writes user input to.</summary>
    public SafeFileHandle InputWriteHandle { get; private set; } = null!;

    /// <summary>The raw pseudoconsole handle for process launch.</summary>
    internal IntPtr PseudoConsoleHandle => _pseudoConsoleHandle;

    /// <summary>
    /// Creates a new pseudoconsole with the specified dimensions and returns
    /// a ConPtyHost that owns all the handles.
    /// </summary>
    public static ConPtyHost Create(short columns, short rows)
    {
        var host = new ConPtyHost();
        host.Initialize(columns, rows);
        return host;
    }

    private void Initialize(short columns, short rows)
    {
        // Create the two anonymous pipe pairs:
        // Pipe 1: PTY reads from this pipe (we write user input to InputWriteHandle)
        // Pipe 2: PTY writes to this pipe (we read child output from OutputReadHandle)
        CreatePipe(out _inputReadSide, out var inputWriteSide);
        CreatePipe(out var outputReadSide, out _outputWriteSide);

        InputWriteHandle = inputWriteSide;
        OutputReadHandle = outputReadSide;

        var size = new NativeMethods.COORD { X = columns, Y = rows };
        int hr = NativeMethods.CreatePseudoConsole(
            size,
            _inputReadSide,
            _outputWriteSide,
            0,
            out _pseudoConsoleHandle);

        if (hr != 0)
        {
            throw new InvalidOperationException(
                $"CreatePseudoConsole failed with HRESULT 0x{hr:X8}");
        }
    }

    /// <summary>
    /// Resizes the pseudoconsole to new dimensions.
    /// </summary>
    public void Resize(short columns, short rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pseudoConsoleHandle == IntPtr.Zero) return;

        var size = new NativeMethods.COORD { X = columns, Y = rows };
        int hr = NativeMethods.ResizePseudoConsole(_pseudoConsoleHandle, size);
        if (hr != 0)
        {
            throw new InvalidOperationException(
                $"ResizePseudoConsole failed with HRESULT 0x{hr:X8}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pseudoConsoleHandle != IntPtr.Zero)
        {
            NativeMethods.ClosePseudoConsole(_pseudoConsoleHandle);
            _pseudoConsoleHandle = IntPtr.Zero;
        }

        _inputReadSide?.Dispose();
        _outputWriteSide?.Dispose();
        InputWriteHandle?.Dispose();
        OutputReadHandle?.Dispose();
    }

    private static void CreatePipe(out SafeFileHandle readSide, out SafeFileHandle writeSide)
    {
        if (!NativeMethods.CreatePipe(out readSide, out writeSide, IntPtr.Zero, 0))
        {
            throw new InvalidOperationException(
                $"CreatePipe failed with error {Marshal.GetLastWin32Error()}");
        }
    }

    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct COORD
        {
            public short X;
            public short Y;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int CreatePseudoConsole(
            COORD size,
            SafeFileHandle hInput,
            SafeFileHandle hOutput,
            uint dwFlags,
            out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(
            out SafeFileHandle hReadPipe,
            out SafeFileHandle hWritePipe,
            IntPtr lpPipeAttributes,
            uint nSize);
    }
}
