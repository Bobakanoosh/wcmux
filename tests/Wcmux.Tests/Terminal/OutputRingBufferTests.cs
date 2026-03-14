using Wcmux.Core.Terminal;

namespace Wcmux.Tests.Terminal;

/// <summary>
/// Unit tests for the ring buffer and GetRecentLines functionality
/// integrated into TerminalSurfaceBridge.
/// </summary>
public class OutputRingBufferTests : IAsyncDisposable
{
    private readonly FakeSession _session = new();
    private readonly List<string> _surfaceWrites = new();
    private TerminalSurfaceBridge? _bridge;

    private TerminalSurfaceBridge CreateBridge(
        int batchIntervalMs = 5,
        int ringBufferCapacity = 20)
    {
        _bridge = new TerminalSurfaceBridge(
            _session,
            writeToSurface: text =>
            {
                _surfaceWrites.Add(text);
                return Task.CompletedTask;
            },
            initialCols: 80,
            initialRows: 24,
            batchIntervalMs: batchIntervalMs,
            ringBufferCapacity: ringBufferCapacity);
        _bridge.PreviewEnabled = true;
        return _bridge;
    }

    public async ValueTask DisposeAsync()
    {
        if (_bridge is not null)
            await _bridge.DisposeAsync();
    }

    [Fact]
    public void GetRecentLines_NoOutput_ReturnsEmptyArray()
    {
        var bridge = CreateBridge();
        var lines = bridge.GetRecentLines(2);
        Assert.Empty(lines);
    }

    [Fact]
    public async Task GetRecentLines_AfterOutput_ReturnsStrippedText()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        bridge.EnqueueOutput("\x1b[32mhello\x1b[0m\nworld\n");
        await Task.Delay(50);

        var lines = bridge.GetRecentLines(2);
        Assert.Equal(2, lines.Length);
        Assert.Equal("hello", lines[0]);
        Assert.Equal("world", lines[1]);
    }

    [Fact]
    public async Task GetRecentLines_RequestMoreThanAvailable_ReturnsOnlyAvailable()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        bridge.EnqueueOutput("line1\nline2\nline3\n");
        await Task.Delay(50);

        var lines = bridge.GetRecentLines(5);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task GetRecentLines_EmptyLinesSkipped()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        bridge.EnqueueOutput("first\n\n\n   \nsecond\n");
        await Task.Delay(50);

        var lines = bridge.GetRecentLines(5);
        Assert.Equal(2, lines.Length);
        Assert.Equal("first", lines[0]);
        Assert.Equal("second", lines[1]);
    }

    [Fact]
    public async Task RingBuffer_WrapsAround_OldestOverwritten()
    {
        var bridge = CreateBridge(batchIntervalMs: 5, ringBufferCapacity: 3);

        bridge.EnqueueOutput("a\nb\nc\nd\ne\n");
        await Task.Delay(50);

        var lines = bridge.GetRecentLines(3);
        Assert.Equal(3, lines.Length);
        // Should have the last 3 lines: c, d, e
        Assert.Equal("c", lines[0]);
        Assert.Equal("d", lines[1]);
        Assert.Equal("e", lines[2]);
    }

    [Fact]
    public async Task RingBuffer_MultipleBatches_AccumulatesCorrectly()
    {
        var bridge = CreateBridge(batchIntervalMs: 5, ringBufferCapacity: 20);

        bridge.EnqueueOutput("batch1-line1\nbatch1-line2\n");
        await Task.Delay(50);

        bridge.EnqueueOutput("batch2-line1\n");
        await Task.Delay(50);

        var lines = bridge.GetRecentLines(3);
        Assert.Equal(3, lines.Length);
        Assert.Equal("batch1-line1", lines[0]);
        Assert.Equal("batch1-line2", lines[1]);
        Assert.Equal("batch2-line1", lines[2]);
    }

    [Fact]
    public async Task GetRecentLines_AnsiStrippedFromOutput()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        bridge.EnqueueOutput("\x1b[1;32mPS C:\\Users>\x1b[0m command\n\x1b[31merror\x1b[0m\n");
        await Task.Delay(50);

        var lines = bridge.GetRecentLines(2);
        Assert.Equal(2, lines.Length);
        Assert.Equal("PS C:\\Users> command", lines[0]);
        Assert.Equal("error", lines[1]);
    }

    [Fact]
    public async Task GetRecentLines_ThreadSafe_NoCrashOnConcurrentAccess()
    {
        var bridge = CreateBridge(batchIntervalMs: 2, ringBufferCapacity: 10);

        // Enqueue output rapidly while reading concurrently
        var writerTask = Task.Run(async () =>
        {
            for (int i = 0; i < 50; i++)
            {
                bridge.EnqueueOutput($"line{i}\n");
                await Task.Delay(1);
            }
        });

        var readerTask = Task.Run(async () =>
        {
            for (int i = 0; i < 50; i++)
            {
                // Should not throw
                var lines = bridge.GetRecentLines(5);
                Assert.True(lines.Length <= 5);
                await Task.Delay(1);
            }
        });

        await Task.WhenAll(writerTask, readerTask);
    }

    [Fact]
    public async Task RingBuffer_DoesNotAffectSurfaceOutput()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        var vtText = "\x1b[31mcolored\x1b[0m";
        bridge.EnqueueOutput(vtText);
        await Task.Delay(50);

        // Surface should still receive the raw VT text
        Assert.Contains(_surfaceWrites, w => w.Contains(vtText));
    }
}
