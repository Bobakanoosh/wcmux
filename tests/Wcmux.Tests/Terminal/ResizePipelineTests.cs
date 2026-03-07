using Wcmux.Core.Terminal;

namespace Wcmux.Tests.Terminal;

/// <summary>
/// Automated coverage for the resize negotiation pipeline in
/// TerminalSurfaceBridge: debounce behavior, redundant resize suppression,
/// pixel-to-cell-size translation, and minimum dimension enforcement.
/// </summary>
public class ResizePipelineTests : IAsyncDisposable
{
    private readonly FakeSession _session = new();
    private readonly List<string> _surfaceWrites = new();
    private TerminalSurfaceBridge? _bridge;

    private TerminalSurfaceBridge CreateBridge(
        int initialCols = 80,
        int initialRows = 24,
        int resizeDebounceMs = 20)
    {
        _bridge = new TerminalSurfaceBridge(
            _session,
            writeToSurface: text =>
            {
                _surfaceWrites.Add(text);
                return Task.CompletedTask;
            },
            initialCols: initialCols,
            initialRows: initialRows,
            batchIntervalMs: 5,
            resizeDebounceMs: resizeDebounceMs);
        return _bridge;
    }

    public async ValueTask DisposeAsync()
    {
        if (_bridge is not null)
            await _bridge.DisposeAsync();
    }

    [Fact]
    public void RequestResize_SameDimensions_SuppressesResize()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24);

        bridge.RequestResize(80, 24);

        Assert.Equal(1, bridge.SuppressedResizes);
        Assert.Empty(_session.Resizes);
    }

    [Fact]
    public void RequestResize_SameDimensions_MultipleTimes_CountsSuppressed()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24);

        bridge.RequestResize(80, 24);
        bridge.RequestResize(80, 24);
        bridge.RequestResize(80, 24);

        Assert.Equal(3, bridge.SuppressedResizes);
        Assert.Empty(_session.Resizes);
    }

    [Fact]
    public async Task RequestResize_DifferentDimensions_ResizesAfterDebounce()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 20);

        bridge.RequestResize(100, 30);

        // Wait for debounce to flush
        await Task.Delay(100);

        Assert.Single(_session.Resizes);
        Assert.Equal((short)100, _session.Resizes[0].cols);
        Assert.Equal((short)30, _session.Resizes[0].rows);
        Assert.Equal(100, bridge.CurrentColumns);
        Assert.Equal(30, bridge.CurrentRows);
    }

    [Fact]
    public async Task RequestResize_RapidChanges_OnlyLastResizeFires()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 50);

        // Simulate rapid resize spam -- each call resets the debounce timer
        bridge.RequestResize(90, 25);
        await Task.Delay(10);
        bridge.RequestResize(100, 26);
        await Task.Delay(10);
        bridge.RequestResize(110, 27);

        // Wait for the debounce to flush
        await Task.Delay(150);

        // Only the last resize should have fired
        Assert.Single(_session.Resizes);
        Assert.Equal((short)110, _session.Resizes[0].cols);
        Assert.Equal((short)27, _session.Resizes[0].rows);
    }

    [Fact]
    public void RequestResize_InvalidDimensions_Ignored()
    {
        var bridge = CreateBridge();

        bridge.RequestResize(0, 24);
        bridge.RequestResize(80, 0);
        bridge.RequestResize(-10, 24);
        bridge.RequestResize(80, -5);

        Assert.Empty(_session.Resizes);
        Assert.Equal(0, bridge.SuppressedResizes);
    }

    [Fact]
    public void RequestResizeFromPixels_CalculatesCorrectDimensions()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 5);

        // 8.4px cell width, 17px cell height
        // 840px / 8.4 = 100 cols, 510px / 17 = 30 rows
        bridge.RequestResizeFromPixels(840, 510, 8.4, 17.0);

        // Verify the calculated dimensions were passed to the resize pipeline
        Assert.Equal(0, bridge.SuppressedResizes); // Different from initial
    }

    [Fact]
    public void RequestResizeFromPixels_EnforcesMinimumColumns()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 5);

        // Very small width -- should clamp to 20 cols minimum
        bridge.RequestResizeFromPixels(50, 510, 8.4, 17.0);

        // 50 / 8.4 = 5.95, floor = 5, but min is 20
        // Should not suppress (20 != 80)
        Assert.Equal(0, bridge.SuppressedResizes);
    }

    [Fact]
    public void RequestResizeFromPixels_EnforcesMinimumRows()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 5);

        // Very small height -- should clamp to 6 rows minimum
        bridge.RequestResizeFromPixels(840, 30, 8.4, 17.0);

        // 30 / 17 = 1.76, floor = 1, but min is 6
        Assert.Equal(0, bridge.SuppressedResizes);
    }

    [Fact]
    public void RequestResizeFromPixels_ZeroCellSize_Ignored()
    {
        var bridge = CreateBridge();

        bridge.RequestResizeFromPixels(840, 510, 0, 17.0);
        bridge.RequestResizeFromPixels(840, 510, 8.4, 0);
        bridge.RequestResizeFromPixels(840, 510, -1, 17.0);

        Assert.Empty(_session.Resizes);
    }

    [Fact]
    public async Task RequestResize_AfterDispose_IsIgnored()
    {
        var bridge = CreateBridge(resizeDebounceMs: 5);
        await bridge.DisposeAsync();
        _bridge = null;

        bridge.RequestResize(200, 50);

        await Task.Delay(50);
        Assert.Empty(_session.Resizes);
    }

    [Fact]
    public async Task RequestResize_SequentialChanges_AllProcessed()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 10);

        // First resize
        bridge.RequestResize(100, 30);
        await Task.Delay(50);

        // Second resize (different from first)
        bridge.RequestResize(120, 40);
        await Task.Delay(50);

        Assert.Equal(2, _session.Resizes.Count);
        Assert.Equal((short)100, _session.Resizes[0].cols);
        Assert.Equal((short)30, _session.Resizes[0].rows);
        Assert.Equal((short)120, _session.Resizes[1].cols);
        Assert.Equal((short)40, _session.Resizes[1].rows);
    }

    [Fact]
    public async Task RequestResize_BackToOriginal_StillFires()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 10);

        // Change to different size
        bridge.RequestResize(100, 30);
        await Task.Delay(50);

        // Change back to original
        bridge.RequestResize(80, 24);
        await Task.Delay(50);

        Assert.Equal(2, _session.Resizes.Count);
        Assert.Equal((short)80, _session.Resizes[1].cols);
        Assert.Equal((short)24, _session.Resizes[1].rows);
    }

    [Fact]
    public async Task RequestResizeFromPixels_CorrectDimensionValues()
    {
        var bridge = CreateBridge(initialCols: 80, initialRows: 24, resizeDebounceMs: 10);

        // 1000px / 10px per cell = 100 cols
        // 600px / 20px per cell = 30 rows
        bridge.RequestResizeFromPixels(1000, 600, 10.0, 20.0);

        await Task.Delay(50);

        Assert.Single(_session.Resizes);
        Assert.Equal((short)100, _session.Resizes[0].cols);
        Assert.Equal((short)30, _session.Resizes[0].rows);
    }
}
