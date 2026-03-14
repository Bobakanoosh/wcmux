using System.Collections.Concurrent;
using System.Text;
using Wcmux.Core.Runtime;

namespace Wcmux.Core.Terminal;

/// <summary>
/// Bidirectional bridge for VT output, terminal input, cwd signals, and
/// resize updates between the ConPTY runtime and a hosted terminal surface.
///
/// Responsibilities:
/// - Batches VT output writes to reduce UI-thread dispatch frequency
/// - Routes user input (keystrokes, paste) from the surface to ConPTY
/// - Tracks and exposes cwdChanged signals from the session
/// - Implements debounced resize: translates pane pixel bounds to terminal
///   rows/columns, suppresses redundant ResizePseudoConsole calls, and
///   debounces rapid resize spam during window drag
/// </summary>
public sealed class TerminalSurfaceBridge : IAsyncDisposable
{
    /// <summary>Default output batch interval in milliseconds.</summary>
    public const int DefaultBatchIntervalMs = 16; // ~60fps

    /// <summary>Default resize debounce interval in milliseconds.</summary>
    public const int DefaultResizeDebounceMs = 100;

    private readonly ISession _session;
    private readonly Func<string, Task> _writeToSurface;
    private readonly ConcurrentQueue<string> _outputQueue = new();
    private readonly CancellationTokenSource _batchCts = new();
    private readonly Task _batchTask;
    private readonly object _resizeLock = new();
    private readonly object _ringLock = new();
    private readonly string[] _ringBuffer;
    private readonly int _ringCapacity;
    private int _ringHead;
    private int _ringCount;

    private int _lastCols;
    private int _lastRows;
    private Timer? _resizeDebounceTimer;
    private (int cols, int rows)? _pendingResize;
    private string? _lastKnownCwd;
    private bool _disposed;

    /// <summary>
    /// Fired when the session reports a working directory change (OSC 7).
    /// </summary>
    public event Action<string>? CwdChanged;

    /// <summary>
    /// Fired when a bell character (0x07) is detected in the output stream.
    /// Multiple bells in a single batch result in a single invocation.
    /// The bell character is stripped from the output before delivery to the surface.
    /// </summary>
    public event Action? BellDetected;

    /// <summary>
    /// The last known working directory reported by the session.
    /// </summary>
    public string? LastKnownCwd => _lastKnownCwd;

    /// <summary>
    /// The last applied terminal dimensions (columns).
    /// </summary>
    public int CurrentColumns => _lastCols;

    /// <summary>
    /// The last applied terminal dimensions (rows).
    /// </summary>
    public int CurrentRows => _lastRows;

    /// <summary>
    /// How many output writes have been batched since creation.
    /// Exposed for testing.
    /// </summary>
    public long TotalOutputBatches { get; private set; }

    /// <summary>
    /// How many resize calls were suppressed because cols/rows were unchanged.
    /// Exposed for testing.
    /// </summary>
    public long SuppressedResizes { get; private set; }

    /// <summary>
    /// Interval in ms between output batch flushes.
    /// </summary>
    public int BatchIntervalMs { get; }

    /// <summary>
    /// Interval in ms for resize debounce.
    /// </summary>
    public int ResizeDebounceMs { get; }

    /// <summary>
    /// When false (default), AppendToRingBuffer is skipped to avoid CPU overhead
    /// while SIDE-02 preview display is disabled.
    /// </summary>
    public bool PreviewEnabled { get; set; } = false;

    /// <summary>
    /// Creates a new bridge between the given session and surface writer.
    /// </summary>
    /// <param name="session">The ConPTY session to bridge.</param>
    /// <param name="writeToSurface">
    /// Callback that writes VT output to the terminal surface.
    /// This will be called on a background thread -- the implementation
    /// must marshal to the UI thread if needed.
    /// </param>
    /// <param name="initialCols">Initial terminal column count.</param>
    /// <param name="initialRows">Initial terminal row count.</param>
    /// <param name="batchIntervalMs">Output batch interval in ms.</param>
    /// <param name="resizeDebounceMs">Resize debounce interval in ms.</param>
    /// <param name="ringBufferCapacity">Max lines retained in the output ring buffer (default 20).</param>
    public TerminalSurfaceBridge(
        ISession session,
        Func<string, Task> writeToSurface,
        int initialCols = 120,
        int initialRows = 30,
        int batchIntervalMs = DefaultBatchIntervalMs,
        int resizeDebounceMs = DefaultResizeDebounceMs,
        int ringBufferCapacity = 20)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _writeToSurface = writeToSurface ?? throw new ArgumentNullException(nameof(writeToSurface));
        _lastCols = initialCols;
        _lastRows = initialRows;
        _lastKnownCwd = session.LastKnownCwd;
        BatchIntervalMs = batchIntervalMs;
        ResizeDebounceMs = resizeDebounceMs;
        _ringCapacity = Math.Max(1, ringBufferCapacity);
        _ringBuffer = new string[_ringCapacity];

        _batchTask = Task.Factory.StartNew(
            () => RunOutputBatchLoop(_batchCts.Token),
            TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Enqueues VT output text for batched delivery to the terminal surface.
    /// Thread-safe -- called from the session output pump.
    /// </summary>
    public void EnqueueOutput(string text)
    {
        if (_disposed || string.IsNullOrEmpty(text)) return;
        _outputQueue.Enqueue(text);
    }

    /// <summary>
    /// Handles a cwd change signal from the session. Updates the tracked
    /// cwd and fires the CwdChanged event.
    /// </summary>
    public void HandleCwdChanged(string newCwd)
    {
        if (string.IsNullOrEmpty(newCwd)) return;
        if (newCwd == _lastKnownCwd) return;

        _lastKnownCwd = newCwd;
        CwdChanged?.Invoke(newCwd);
    }

    /// <summary>
    /// Routes user input from the terminal surface to the ConPTY session.
    /// </summary>
    public async Task HandleInputAsync(byte[] data)
    {
        if (_disposed || data.Length == 0) return;
        await _session.WriteInputAsync(data);
    }

    /// <summary>
    /// Requests a resize of the terminal. The resize is debounced and
    /// redundant resize calls (same cols/rows) are suppressed.
    /// </summary>
    /// <param name="cols">New column count.</param>
    /// <param name="rows">New row count.</param>
    public void RequestResize(int cols, int rows)
    {
        if (_disposed) return;
        if (cols <= 0 || rows <= 0) return;

        lock (_resizeLock)
        {
            // Suppress redundant resizes
            if (cols == _lastCols && rows == _lastRows)
            {
                SuppressedResizes++;
                return;
            }

            _pendingResize = (cols, rows);

            // Debounce: reset the timer on each new resize request
            _resizeDebounceTimer?.Dispose();
            _resizeDebounceTimer = new Timer(
                _ => FlushResize(),
                null,
                ResizeDebounceMs,
                Timeout.Infinite);
        }
    }

    /// <summary>
    /// Translates pane pixel dimensions and cell size into terminal
    /// rows and columns, then requests the resize through the debounced
    /// pipeline.
    /// </summary>
    /// <param name="widthPx">Pane width in pixels.</param>
    /// <param name="heightPx">Pane height in pixels.</param>
    /// <param name="cellWidthPx">Measured cell width in pixels.</param>
    /// <param name="cellHeightPx">Measured cell height in pixels.</param>
    public void RequestResizeFromPixels(
        double widthPx, double heightPx,
        double cellWidthPx, double cellHeightPx)
    {
        if (cellWidthPx <= 0 || cellHeightPx <= 0) return;

        var cols = Math.Max(20, (int)Math.Floor(widthPx / cellWidthPx));
        var rows = Math.Max(6, (int)Math.Floor(heightPx / cellHeightPx));

        RequestResize(cols, rows);
    }

    private void FlushResize()
    {
        (int cols, int rows)? pending;
        lock (_resizeLock)
        {
            pending = _pendingResize;
            _pendingResize = null;
        }

        if (pending is null) return;

        var (newCols, newRows) = pending.Value;

        // Final redundancy check after debounce
        if (newCols == _lastCols && newRows == _lastRows)
        {
            SuppressedResizes++;
            return;
        }

        _lastCols = newCols;
        _lastRows = newRows;

        try
        {
            _session.ResizeAsync((short)newCols, (short)newRows)
                .GetAwaiter().GetResult();
        }
        catch
        {
            // Resize failed -- session may be shutting down
        }
    }

    private async Task RunOutputBatchLoop(CancellationToken ct)
    {
        var sb = new StringBuilder(4096);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(BatchIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Drain the queue into a single batch
            sb.Clear();
            while (_outputQueue.TryDequeue(out var chunk))
            {
                sb.Append(chunk);
            }

            if (sb.Length == 0) continue;

            TotalOutputBatches++;

            var batch = sb.ToString();

            // Detect and strip bell characters before delivering to surface
            if (batch.Contains('\u0007'))
            {
                batch = batch.Replace("\u0007", "");
                BellDetected?.Invoke();
            }

            if (batch.Length == 0) continue;

            // Capture plain-text lines into ring buffer for sidebar preview
            if (PreviewEnabled) AppendToRingBuffer(batch);

            try
            {
                await _writeToSurface(batch);
            }
            catch
            {
                // Surface write failed -- surface may be disposed
            }
        }

        // Final drain on shutdown
        sb.Clear();
        while (_outputQueue.TryDequeue(out var chunk))
        {
            sb.Append(chunk);
        }
        if (sb.Length > 0)
        {
            try
            {
                await _writeToSurface(sb.ToString());
            }
            catch { }
        }
    }

    /// <summary>
    /// Returns the most recent non-empty lines captured from terminal output,
    /// with ANSI escape codes stripped. Thread-safe.
    /// </summary>
    /// <param name="count">Maximum number of lines to return.</param>
    public string[] GetRecentLines(int count)
    {
        if (count <= 0) return [];

        lock (_ringLock)
        {
            var available = Math.Min(count, _ringCount);
            if (available == 0) return [];

            var result = new string[available];

            // Read from oldest to newest within the requested range
            var startIdx = (_ringHead - _ringCount + _ringCapacity) % _ringCapacity;
            var skipCount = _ringCount - available;
            var readIdx = (startIdx + skipCount) % _ringCapacity;

            for (int i = 0; i < available; i++)
            {
                result[i] = _ringBuffer[readIdx];
                readIdx = (readIdx + 1) % _ringCapacity;
            }

            return result;
        }
    }

    private void AppendToRingBuffer(string rawBatch)
    {
        var stripped = AnsiStripper.Strip(rawBatch);
        var lines = stripped.Split('\n');

        lock (_ringLock)
        {
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                _ringBuffer[_ringHead] = trimmed;
                _ringHead = (_ringHead + 1) % _ringCapacity;
                if (_ringCount < _ringCapacity) _ringCount++;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _resizeDebounceTimer?.Dispose();
        await _batchCts.CancelAsync();

        try
        {
            await _batchTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort
        }

        _batchCts.Dispose();
    }
}
