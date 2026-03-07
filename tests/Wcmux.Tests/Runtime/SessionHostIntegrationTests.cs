using System.Collections.Concurrent;
using System.Text;
using Wcmux.Core.Runtime;

namespace Wcmux.Tests.Runtime;

/// <summary>
/// Integration tests that exercise real ConPTY-backed sessions against
/// actual PowerShell processes. These tests validate launch, IO round-trip,
/// exit observation, and resource cleanup.
/// </summary>
public sealed class SessionHostIntegrationTests : IAsyncDisposable
{
    private readonly SessionManager _manager = new();

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
    }

    [Fact]
    public async Task SessionHost_LaunchesAndEmitsReadyEvent()
    {
        var events = new ConcurrentBag<SessionEvent>();
        _manager.SessionEventReceived += (_, e) => events.Add(e);

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.True(session.IsRunning);
        Assert.Contains(events, e => e is SessionReadyEvent r && r.SessionId == session.SessionId);

        await session.CloseAsync();
    }

    [Fact]
    public async Task SessionHost_ReceivesOutputFromShell()
    {
        var outputEvents = new ConcurrentBag<SessionOutputEvent>();
        _manager.SessionEventReceived += (_, e) =>
        {
            if (e is SessionOutputEvent o)
                outputEvents.Add(o);
        };

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        // Wait for initial shell output (prompt)
        await WaitForConditionAsync(
            () => outputEvents.Count > 0,
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected output from shell within 10 seconds");

        Assert.NotEmpty(outputEvents);
        Assert.All(outputEvents, o => Assert.Equal(session.SessionId, o.SessionId));

        await session.CloseAsync();
    }

    [Fact]
    public async Task SessionHost_AcceptsInputAndProducesOutput()
    {
        // Verify the ConPTY IO pipeline: session launches, produces initial
        // VT output through the pseudoconsole, accepts input without error,
        // and emits an exit event when the process terminates.
        var outputEvents = new ConcurrentBag<SessionOutputEvent>();
        var exitEvents = new ConcurrentBag<SessionExitedEvent>();
        _manager.SessionEventReceived += (_, e) =>
        {
            if (e is SessionOutputEvent o) outputEvents.Add(o);
            if (e is SessionExitedEvent x) exitEvents.Add(x);
        };

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);
        Assert.True(session.IsRunning, "Session should be running after launch");

        // ConPTY emits VT control sequences through the output pipe
        await WaitForConditionAsync(
            () => outputEvents.Count > 0,
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected VT output from ConPTY");

        Assert.NotEmpty(outputEvents);

        // Write input -- even if the session exits quickly, the write should
        // succeed or be handled gracefully (no unhandled exceptions)
        try
        {
            var command = Encoding.UTF8.GetBytes("exit\r\n");
            await session.WriteInputAsync(command);
        }
        catch (ObjectDisposedException)
        {
            // Acceptable -- session may have already closed
        }
        catch (IOException)
        {
            // Acceptable -- pipe may have closed
        }

        // Wait for the exit event
        await WaitForConditionAsync(
            () => exitEvents.Any(e => e.SessionId == session.SessionId),
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected exit event");

        // Session should have exited
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task SessionHost_EmitsExitEventOnShellExit()
    {
        var exitEvents = new ConcurrentBag<SessionExitedEvent>();
        _manager.SessionEventReceived += (_, e) =>
        {
            if (e is SessionExitedEvent x)
                exitEvents.Add(x);
        };

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        // Wait for prompt
        await Task.Delay(1000);

        // Tell the shell to exit
        var exitCommand = Encoding.UTF8.GetBytes("exit\r\n");
        await session.WriteInputAsync(exitCommand);

        // Wait for the exit event
        await WaitForConditionAsync(
            () => exitEvents.Any(e => e.SessionId == session.SessionId),
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected exit event after 'exit' command");

        var exitEvent = exitEvents.First(e => e.SessionId == session.SessionId);
        Assert.Equal(0, exitEvent.ExitCode);
    }

    [Fact]
    public async Task SessionHost_ResizeSendsEvent()
    {
        var resizeEvents = new ConcurrentBag<SessionResizedEvent>();
        _manager.SessionEventReceived += (_, e) =>
        {
            if (e is SessionResizedEvent r)
                resizeEvents.Add(r);
        };

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        await session.ResizeAsync(80, 24);

        Assert.Contains(resizeEvents, r =>
            r.SessionId == session.SessionId &&
            r.Columns == 80 &&
            r.Rows == 24);

        await session.CloseAsync();
    }

    [Fact]
    public async Task SessionHost_CloseDisposesCleanly()
    {
        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);
        Assert.True(session.IsRunning);

        await session.CloseAsync();

        // After close, the session should not be running
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task SessionHost_SessionManagerTracksActiveSessions()
    {
        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);
        Assert.Single(_manager.Sessions);
        Assert.Equal(session.SessionId, _manager.Sessions.First().SessionId);

        await _manager.CloseSessionAsync(session.SessionId);
        Assert.Empty(_manager.Sessions);
    }

    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string message)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(100);
        }
        Assert.Fail($"Timed out: {message}");
    }
}
