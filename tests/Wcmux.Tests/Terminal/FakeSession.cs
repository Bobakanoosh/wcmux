using Wcmux.Core.Runtime;

namespace Wcmux.Tests.Terminal;

/// <summary>
/// Fake ISession implementation for unit testing the terminal bridge
/// and resize pipeline without requiring a real ConPTY session.
/// </summary>
internal sealed class FakeSession : ISession
{
    private readonly List<byte[]> _inputWrites = new();
    private readonly List<(short cols, short rows)> _resizes = new();
    private bool _closed;

    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    public SessionLaunchSpec LaunchSpec { get; } = new(
        "fake.exe", [], "C:\\fake", new Dictionary<string, string?>(), "pwsh");

    public bool IsRunning { get; set; } = true;
    public string? LastKnownCwd { get; set; } = "C:\\fake";

    /// <summary>All input data written to this session.</summary>
    public IReadOnlyList<byte[]> InputWrites => _inputWrites;

    /// <summary>All resize calls made to this session.</summary>
    public IReadOnlyList<(short cols, short rows)> Resizes => _resizes;

    /// <summary>Whether CloseAsync was called.</summary>
    public bool WasClosed => _closed;

    public Task WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        _inputWrites.Add(data.ToArray());
        return Task.CompletedTask;
    }

    public Task ResizeAsync(short columns, short rows, CancellationToken cancellationToken = default)
    {
        _resizes.Add((columns, rows));
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _closed = true;
        IsRunning = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _closed = true;
        IsRunning = false;
        return ValueTask.CompletedTask;
    }
}
