using System.Text;
using Wcmux.Core.Terminal;

namespace Wcmux.Tests.Terminal;

/// <summary>
/// Automated coverage for TerminalSurfaceBridge: output forwarding,
/// input routing, and cwd signal parsing.
/// </summary>
public class TerminalBridgeTests : IAsyncDisposable
{
    private readonly FakeSession _session = new();
    private readonly List<string> _surfaceWrites = new();
    private TerminalSurfaceBridge? _bridge;

    private TerminalSurfaceBridge CreateBridge(
        int batchIntervalMs = 5,
        int resizeDebounceMs = 10)
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
            resizeDebounceMs: resizeDebounceMs);
        return _bridge;
    }

    public async ValueTask DisposeAsync()
    {
        if (_bridge is not null)
            await _bridge.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueOutput_BatchesMultipleChunks_IntoSingleSurfaceWrite()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        bridge.EnqueueOutput("hello ");
        bridge.EnqueueOutput("world");

        // Wait for batch to flush
        await Task.Delay(50);

        Assert.Contains(_surfaceWrites, w => w.Contains("hello ") && w.Contains("world"));
        Assert.True(bridge.TotalOutputBatches >= 1);
    }

    [Fact]
    public async Task EnqueueOutput_EmptyString_IsIgnored()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        bridge.EnqueueOutput("");
        bridge.EnqueueOutput(null!);

        await Task.Delay(50);

        Assert.Equal(0, bridge.TotalOutputBatches);
    }

    [Fact]
    public async Task EnqueueOutput_VtEscapeSequences_PreservedInOutput()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);

        var vtText = "\x1b[31mred text\x1b[0m";
        bridge.EnqueueOutput(vtText);

        await Task.Delay(50);

        Assert.Contains(_surfaceWrites, w => w.Contains(vtText));
    }

    [Fact]
    public async Task HandleInputAsync_RoutesDataToSession()
    {
        var bridge = CreateBridge();

        var inputData = Encoding.UTF8.GetBytes("ls\r\n");
        await bridge.HandleInputAsync(inputData);

        Assert.Single(_session.InputWrites);
        Assert.Equal(inputData, _session.InputWrites[0]);
    }

    [Fact]
    public async Task HandleInputAsync_EmptyData_IsIgnored()
    {
        var bridge = CreateBridge();

        await bridge.HandleInputAsync([]);

        Assert.Empty(_session.InputWrites);
    }

    [Fact]
    public async Task HandleInputAsync_MultipleWrites_AllRouted()
    {
        var bridge = CreateBridge();

        await bridge.HandleInputAsync(Encoding.UTF8.GetBytes("a"));
        await bridge.HandleInputAsync(Encoding.UTF8.GetBytes("b"));
        await bridge.HandleInputAsync(Encoding.UTF8.GetBytes("c"));

        Assert.Equal(3, _session.InputWrites.Count);
    }

    [Fact]
    public void HandleCwdChanged_UpdatesLastKnownCwd()
    {
        var bridge = CreateBridge();

        bridge.HandleCwdChanged("C:\\Users\\test");

        Assert.Equal("C:\\Users\\test", bridge.LastKnownCwd);
    }

    [Fact]
    public void HandleCwdChanged_FiresCwdChangedEvent()
    {
        var bridge = CreateBridge();
        string? reportedCwd = null;
        bridge.CwdChanged += cwd => reportedCwd = cwd;

        bridge.HandleCwdChanged("C:\\new\\path");

        Assert.Equal("C:\\new\\path", reportedCwd);
    }

    [Fact]
    public void HandleCwdChanged_SamePath_DoesNotFireEvent()
    {
        _session.LastKnownCwd = "C:\\same";
        var bridge = CreateBridge();
        int eventCount = 0;
        bridge.CwdChanged += _ => eventCount++;

        bridge.HandleCwdChanged("C:\\same");

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void HandleCwdChanged_EmptyString_IsIgnored()
    {
        var bridge = CreateBridge();
        int eventCount = 0;
        bridge.CwdChanged += _ => eventCount++;

        bridge.HandleCwdChanged("");
        bridge.HandleCwdChanged(null!);

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Constructor_InitializesWithSessionCwd()
    {
        _session.LastKnownCwd = "C:\\initial";
        var bridge = CreateBridge();

        Assert.Equal("C:\\initial", bridge.LastKnownCwd);
    }

    [Fact]
    public void Constructor_SetsInitialDimensions()
    {
        var bridge = CreateBridge();

        Assert.Equal(80, bridge.CurrentColumns);
        Assert.Equal(24, bridge.CurrentRows);
    }

    [Fact]
    public async Task Dispose_FlushesPendingOutput()
    {
        var bridge = CreateBridge(batchIntervalMs: 1000); // Long interval

        bridge.EnqueueOutput("final output");

        // Dispose should flush the remaining output
        await bridge.DisposeAsync();
        _bridge = null; // Prevent double-dispose in DisposeAsync

        Assert.Contains(_surfaceWrites, w => w.Contains("final output"));
    }

    [Fact]
    public async Task EnqueueOutput_AfterDispose_IsIgnored()
    {
        var bridge = CreateBridge(batchIntervalMs: 5);
        await bridge.DisposeAsync();
        _bridge = null;

        bridge.EnqueueOutput("too late");

        await Task.Delay(50);

        // Should not crash and no output should arrive
        Assert.DoesNotContain(_surfaceWrites, w => w.Contains("too late"));
    }
}
