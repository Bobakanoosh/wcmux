using Wcmux.Core.Terminal;

namespace Wcmux.Tests.Terminal;

/// <summary>
/// Tests for bell character detection and stripping in TerminalSurfaceBridge.
/// </summary>
public class BellDetectionTests : IAsyncDisposable
{
    private readonly FakeSession _session = new();
    private readonly List<string> _surfaceWrites = new();
    private TerminalSurfaceBridge? _bridge;

    private TerminalSurfaceBridge CreateBridge(int batchIntervalMs = 5)
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
            batchIntervalMs: batchIntervalMs);
        return _bridge;
    }

    public async ValueTask DisposeAsync()
    {
        if (_bridge is not null)
            await _bridge.DisposeAsync();
    }

    [Trait("Category", "Attention")]
    [Fact]
    public async Task Output_WithBell_StrippedAndBellDetectedFires()
    {
        var bridge = CreateBridge();
        int bellCount = 0;
        bridge.BellDetected += () => bellCount++;

        bridge.EnqueueOutput("hello\u0007world");

        await Task.Delay(50);

        Assert.Equal(1, bellCount);
        Assert.Contains(_surfaceWrites, w => w == "helloworld");
    }

    [Trait("Category", "Attention")]
    [Fact]
    public async Task Output_WithoutBell_PassesThroughUnchanged()
    {
        var bridge = CreateBridge();
        int bellCount = 0;
        bridge.BellDetected += () => bellCount++;

        bridge.EnqueueOutput("normal output");

        await Task.Delay(50);

        Assert.Equal(0, bellCount);
        Assert.Contains(_surfaceWrites, w => w == "normal output");
    }

    [Trait("Category", "Attention")]
    [Fact]
    public async Task Output_MultipleBellsInOneBatch_SingleBellDetected()
    {
        var bridge = CreateBridge();
        int bellCount = 0;
        bridge.BellDetected += () => bellCount++;

        bridge.EnqueueOutput("a\u0007b\u0007c");

        await Task.Delay(50);

        Assert.Equal(1, bellCount);
        Assert.Contains(_surfaceWrites, w => w == "abc");
    }
}
