namespace Wcmux.Core.Runtime;

/// <summary>
/// Represents a live terminal session. Exposes the seam where later renderer
/// and pane orchestration code will attach.
/// </summary>
public interface ISession : IAsyncDisposable
{
    /// <summary>Unique identifier for this session.</summary>
    string SessionId { get; }

    /// <summary>The launch spec that created this session.</summary>
    SessionLaunchSpec LaunchSpec { get; }

    /// <summary>Whether the session process is still running.</summary>
    bool IsRunning { get; }

    /// <summary>The OS process ID of the shell process.</summary>
    int ProcessId { get; }

    /// <summary>The last known working directory reported by the shell.</summary>
    string? LastKnownCwd { get; }

    /// <summary>Writes raw input bytes to the session's ConPTY input pipe.</summary>
    Task WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>Resizes the pseudoconsole to the specified dimensions.</summary>
    Task ResizeAsync(short columns, short rows, CancellationToken cancellationToken = default);

    /// <summary>Requests graceful shutdown, then forces termination after a timeout.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
