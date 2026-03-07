using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Wcmux.Core.Runtime;

/// <summary>
/// A live ConPTY-backed terminal session. Manages the pseudoconsole, child
/// process, and IO pumps. Emits typed session events through the supplied
/// callback. Implements deterministic cleanup on close or process exit.
/// </summary>
internal sealed partial class ConPtySession : ISession
{
    private const int GracefulShutdownTimeoutMs = 3000;

    private readonly ConPtyHost _ptyHost;
    private readonly Process _process;
    private readonly Action<SessionEvent> _emitEvent;
    private readonly CancellationTokenSource _pumpCts = new();
    private readonly Task _outputPumpTask;
    private readonly Task _exitMonitorTask;
    private readonly FileStream _inputStream;
    private readonly SemaphoreSlim _inputLock = new(1, 1);
    private volatile bool _closed;
    private string? _lastKnownCwd;

    public string SessionId { get; }
    public SessionLaunchSpec LaunchSpec { get; }
    public bool IsRunning => !_process.HasExited;
    public string? LastKnownCwd => _lastKnownCwd;

    private ConPtySession(
        string sessionId,
        SessionLaunchSpec launchSpec,
        ConPtyHost ptyHost,
        Process process,
        Action<SessionEvent> emitEvent)
    {
        SessionId = sessionId;
        LaunchSpec = launchSpec;
        _ptyHost = ptyHost;
        _process = process;
        _emitEvent = emitEvent;
        _lastKnownCwd = launchSpec.InitialWorkingDirectory;

        // Create a persistent input stream that does NOT own the handle
        _inputStream = new FileStream(
            new SafeFileHandle(ptyHost.InputWriteHandle.DangerousGetHandle(), ownsHandle: false),
            FileAccess.Write, bufferSize: 256);

        _outputPumpTask = Task.Run(() => OutputPumpAsync(_pumpCts.Token));
        _exitMonitorTask = Task.Run(() => MonitorExitAsync(_pumpCts.Token));
    }

    /// <summary>
    /// Creates a pseudoconsole, launches the child process, starts IO pumps,
    /// and emits a Ready event once the session is operational.
    /// </summary>
    internal static async Task<ConPtySession> StartAsync(
        SessionLaunchSpec launchSpec,
        Action<SessionEvent> emitEvent,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var ptyHost = ConPtyHost.Create(launchSpec.InitialColumns, launchSpec.InitialRows);

        Process process;
        try
        {
            process = LaunchProcess(launchSpec, ptyHost);
        }
        catch
        {
            ptyHost.Dispose();
            throw;
        }

        var session = new ConPtySession(sessionId, launchSpec, ptyHost, process, emitEvent);

        // Allow pumps to start before declaring ready
        await Task.Yield();
        emitEvent(new SessionReadyEvent(sessionId));

        return session;
    }

    public async Task WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        await _inputLock.WaitAsync(cancellationToken);
        try
        {
            await _inputStream.WriteAsync(data, cancellationToken);
            await _inputStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _inputLock.Release();
        }
    }

    public Task ResizeAsync(short columns, short rows, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        _ptyHost.Resize(columns, rows);
        _emitEvent(new SessionResizedEvent(SessionId, columns, rows));
        return Task.CompletedTask;
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_closed) return;
        _closed = true;

        // Signal pumps to stop
        await _pumpCts.CancelAsync();

        // Try graceful shutdown first
        if (!_process.HasExited)
        {
            try
            {
                // Send Ctrl+C equivalent by closing the input pipe
                _ptyHost.InputWriteHandle.Dispose();

                using var graceCts = new CancellationTokenSource(GracefulShutdownTimeoutMs);
                try
                {
                    await _process.WaitForExitAsync(graceCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Grace period expired -- force kill
                }
            }
            catch
            {
                // Ignore errors during graceful attempt
            }
        }

        // Force kill if still alive
        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process may have exited between check and kill
            }
        }

        // Wait for pumps to finish
        try
        {
            await Task.WhenAll(_outputPumpTask, _exitMonitorTask)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Pump cleanup best-effort
        }

        _inputStream.Dispose();
        _inputLock.Dispose();
        _ptyHost.Dispose();
        _process.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    private async Task OutputPumpAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            using var stream = new FileStream(
                _ptyHost.OutputReadHandle,
                FileAccess.Read,
                bufferSize: 0,
                isAsync: false);

            while (!ct.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer.AsMemory(), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    // Pipe broken -- process likely exited
                    break;
                }

                if (bytesRead == 0) break;

                var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                _emitEvent(new SessionOutputEvent(SessionId, text));

                // Parse OSC 7 for cwd tracking
                TryParseCwdSignal(text);
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception)
        {
            // Output pump failed -- session is likely dead
        }
    }

    private void TryParseCwdSignal(string text)
    {
        // OSC 7 format: ESC ] 7 ; file://hostname/path ST
        // ST can be ESC \ or BEL (\x07)
        var match = Osc7Pattern().Match(text);
        if (match.Success)
        {
            var rawPath = match.Groups[1].Value;
            // Decode percent-encoded path from file:// URI
            try
            {
                var uri = new Uri(rawPath);
                var cwd = uri.LocalPath;
                if (!string.IsNullOrEmpty(cwd) && cwd != _lastKnownCwd)
                {
                    _lastKnownCwd = cwd;
                    _emitEvent(new SessionCwdChangedEvent(SessionId, cwd));
                }
            }
            catch
            {
                // Malformed URI -- ignore
            }
        }
    }

    [GeneratedRegex(@"\x1b\]7;(file://[^\x07\x1b]*?)(?:\x07|\x1b\\)", RegexOptions.Compiled)]
    private static partial Regex Osc7Pattern();

    private async Task MonitorExitAsync(CancellationToken ct)
    {
        try
        {
            await _process.WaitForExitAsync(ct);
            _emitEvent(new SessionExitedEvent(SessionId, _process.ExitCode));
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested before process exited
        }
        catch
        {
            _emitEvent(new SessionExitedEvent(SessionId, null));
        }
    }

    /// <summary>
    /// Launches the child process attached to the pseudoconsole using
    /// STARTUPINFOEX with the PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE attribute.
    /// </summary>
    private static Process LaunchProcess(SessionLaunchSpec spec, ConPtyHost ptyHost)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.ExecutablePath,
            WorkingDirectory = spec.InitialWorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        foreach (var arg in spec.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in spec.EnvironmentVariables)
        {
            if (value is not null)
                startInfo.Environment[key] = value;
        }

        // Use the low-level Win32 API to create the process with the
        // pseudoconsole attached via the thread attribute list.
        return LaunchWithPseudoConsole(startInfo, ptyHost.PseudoConsoleHandle);
    }

    private static Process LaunchWithPseudoConsole(ProcessStartInfo psi, IntPtr hPC)
    {
        var startupInfo = default(NativeMethods.STARTUPINFOEX);
        startupInfo.StartupInfo.cb = Marshal.SizeOf<NativeMethods.STARTUPINFOEX>();

        var lpSize = IntPtr.Zero;

        // First call to get required buffer size
        NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
        var attributeList = Marshal.AllocHGlobal(lpSize);
        startupInfo.lpAttributeList = attributeList;

        try
        {
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref lpSize))
                throw new InvalidOperationException(
                    $"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

            if (!NativeMethods.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    hPC,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new InvalidOperationException(
                    $"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
            }

            // Build command line
            var cmdLine = BuildCommandLine(psi);

            // Set up environment block
            var envBlock = IntPtr.Zero;
            // We let the child inherit the parent's environment with overrides
            // applied through ProcessStartInfo.Environment above.

            var processInfo = default(NativeMethods.PROCESS_INFORMATION);

            if (!NativeMethods.CreateProcess(
                    null,
                    cmdLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    NativeMethods.EXTENDED_STARTUPINFO_PRESENT,
                    envBlock,
                    psi.WorkingDirectory,
                    ref startupInfo,
                    out processInfo))
            {
                throw new InvalidOperationException(
                    $"CreateProcess failed: {Marshal.GetLastWin32Error()}");
            }

            // We don't need the thread handle
            NativeMethods.CloseHandle(processInfo.hThread);

            return Process.GetProcessById(processInfo.dwProcessId);
        }
        finally
        {
            NativeMethods.DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
        }
    }

    private static string BuildCommandLine(ProcessStartInfo psi)
    {
        var sb = new StringBuilder();

        // Quote the executable if needed
        if (psi.FileName.Contains(' '))
            sb.Append('"').Append(psi.FileName).Append('"');
        else
            sb.Append(psi.FileName);

        foreach (var arg in psi.ArgumentList)
        {
            sb.Append(' ');
            if (arg.Contains(' ') || arg.Contains('"'))
                sb.Append('"').Append(arg.Replace("\"", "\\\"")).Append('"');
            else
                sb.Append(arg);
        }

        return sb.ToString();
    }

    private static class NativeMethods
    {
        internal const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

        // 22 decimal = PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE
        internal static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE =
            (IntPtr)((0x00020000) | (22 & 0x0000FFFF));

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList, uint dwFlags, IntPtr attribute,
            IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess(
            string? lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);
    }
}
