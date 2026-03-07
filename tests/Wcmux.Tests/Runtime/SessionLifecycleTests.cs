using System.Collections.Concurrent;
using System.Text;
using Wcmux.Core.Runtime;

namespace Wcmux.Tests.Runtime;

/// <summary>
/// Tests that exercise repeated session lifecycle operations: open-close loops,
/// cwd signal capture, concurrent sessions, and resource cleanup verification.
/// These complement SessionHostIntegrationTests by focusing on lifecycle
/// correctness under repeated use.
/// </summary>
public sealed class SessionLifecycleTests : IAsyncDisposable
{
    private readonly SessionManager _manager = new();

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
    }

    [Fact]
    public async Task SessionLifecycle_RepeatedOpenClose_DoesNotLeak()
    {
        // Open and close 3 sessions sequentially to verify handle cleanup.
        // Use CloseSessionAsync (through the manager) to ensure proper removal.
        for (var i = 0; i < 3; i++)
        {
            var spec = SessionLaunchSpec.CreateDefaultPowerShell(
                Environment.CurrentDirectory);

            var session = await _manager.CreateSessionAsync(spec);

            await _manager.CloseSessionAsync(session.SessionId);
            Assert.False(session.IsRunning, $"Session {i} should not be running after close");
            Assert.Empty(_manager.Sessions);
        }
    }

    [Fact]
    public async Task SessionLifecycle_ConcurrentSessions_TrackedIndependently()
    {
        var spec1 = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);
        var spec2 = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session1 = await _manager.CreateSessionAsync(spec1);
        var session2 = await _manager.CreateSessionAsync(spec2);

        Assert.Equal(2, _manager.Sessions.Count);
        Assert.NotEqual(session1.SessionId, session2.SessionId);
        Assert.True(session1.IsRunning);
        Assert.True(session2.IsRunning);

        // Close one, verify the other is still alive
        await _manager.CloseSessionAsync(session1.SessionId);
        Assert.Single(_manager.Sessions);
        Assert.True(session2.IsRunning);

        await session2.CloseAsync();
    }

    [Fact]
    public async Task SessionLifecycle_ExitEventFires_OnNaturalExit()
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

        // The session may exit naturally on its own (under test runner context).
        // If still alive, send an exit command. Either way, verify exit event.
        if (session.IsRunning)
        {
            try
            {
                var exitCommand = Encoding.UTF8.GetBytes("exit\r\n");
                await session.WriteInputAsync(exitCommand);
            }
            catch (ObjectDisposedException) { /* session already closed */ }
        }

        // Wait for exit event
        await WaitForConditionAsync(
            () => exitEvents.Any(e => e.SessionId == session.SessionId),
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected exit event");

        var exitEvent = exitEvents.First(e => e.SessionId == session.SessionId);
        Assert.Equal(0, exitEvent.ExitCode);

        // Session should be removed from manager after exit
        await WaitForConditionAsync(
            () => _manager.Sessions.Count == 0,
            timeout: TimeSpan.FromSeconds(5),
            message: "Expected session to be removed from manager after exit");
    }

    [Fact]
    public async Task SessionLifecycle_CloseAfterExit_IsIdempotent()
    {
        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        // Wait for shell ready
        await Task.Delay(1500);

        // Tell the shell to exit naturally
        var exitCommand = Encoding.UTF8.GetBytes("exit\r\n");
        await session.WriteInputAsync(exitCommand);

        // Wait for process to exit
        await WaitForConditionAsync(
            () => !session.IsRunning,
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected session to stop running after exit");

        // Calling CloseAsync after natural exit should not throw
        await session.CloseAsync();
        await session.CloseAsync(); // Double close should also be safe
    }

    [Fact]
    public async Task SessionLifecycle_OutputPump_DeliversData()
    {
        var output = new ConcurrentBag<SessionOutputEvent>();
        _manager.SessionEventReceived += (_, e) =>
        {
            if (e is SessionOutputEvent o)
                output.Add(o);
        };

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        // Wait for any output (shell prompt or startup banner)
        await WaitForConditionAsync(
            () => output.Count > 0,
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected output from shell startup");

        // All output events should reference this session
        Assert.All(output, o => Assert.Equal(session.SessionId, o.SessionId));
        Assert.All(output, o => Assert.False(string.IsNullOrEmpty(o.Text)));

        await session.CloseAsync();
    }

    [Fact]
    public async Task SessionLifecycle_Resize_DuringActiveSession()
    {
        var resizeEvents = new List<SessionResizedEvent>();
        _manager.SessionEventReceived += (_, e) =>
        {
            if (e is SessionResizedEvent r)
                lock (resizeEvents) resizeEvents.Add(r);
        };

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        // Resize multiple times to simulate aggressive window resizing
        await session.ResizeAsync(80, 24);
        await session.ResizeAsync(100, 40);
        await session.ResizeAsync(60, 20);

        lock (resizeEvents)
        {
            Assert.Equal(3, resizeEvents.Count);

            // Verify resize events are in order with correct values
            Assert.Equal(80, resizeEvents[0].Columns);
            Assert.Equal(24, resizeEvents[0].Rows);
            Assert.Equal(100, resizeEvents[1].Columns);
            Assert.Equal(40, resizeEvents[1].Rows);
            Assert.Equal(60, resizeEvents[2].Columns);
            Assert.Equal(20, resizeEvents[2].Rows);
        }

        await session.CloseAsync();
    }

    [Fact]
    public async Task SessionLifecycle_ManagerDispose_ClosesAllSessions()
    {
        // Use a separate manager so we can dispose it
        var manager = new SessionManager();

        var spec1 = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);
        var spec2 = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session1 = await manager.CreateSessionAsync(spec1);
        var session2 = await manager.CreateSessionAsync(spec2);

        Assert.Equal(2, manager.Sessions.Count);

        await manager.DisposeAsync();

        // Both sessions should be closed after dispose
        Assert.False(session1.IsRunning);
        Assert.False(session2.IsRunning);
    }

    [Fact]
    public void SessionLifecycle_LaunchSpec_DefaultPowerShell_HasCorrectDefaults()
    {
        var cwd = Environment.CurrentDirectory;
        var spec = SessionLaunchSpec.CreateDefaultPowerShell(cwd);

        Assert.Equal(cwd, spec.InitialWorkingDirectory);
        Assert.Equal("pwsh", spec.ProfileKind);
        Assert.Equal(120, spec.InitialColumns);
        Assert.Equal(30, spec.InitialRows);
        Assert.Contains(spec.ExecutablePath, new[] { "pwsh.exe", "powershell.exe" });
        Assert.Contains("-NoLogo", spec.Arguments);
        Assert.Contains("-NoProfile", spec.Arguments);
    }

    [Fact]
    public async Task SessionLifecycle_ReadyEvent_FiredBeforeFirstOutput()
    {
        var eventOrder = new ConcurrentBag<string>();
        _manager.SessionEventReceived += (_, e) =>
        {
            switch (e)
            {
                case SessionReadyEvent:
                    eventOrder.Add("ready");
                    break;
                case SessionOutputEvent:
                    eventOrder.Add("output");
                    break;
            }
        };

        var spec = SessionLaunchSpec.CreateDefaultPowerShell(
            Environment.CurrentDirectory);

        var session = await _manager.CreateSessionAsync(spec);

        // Wait for output to arrive
        await WaitForConditionAsync(
            () => eventOrder.Any(e => e == "output"),
            timeout: TimeSpan.FromSeconds(10),
            message: "Expected output event");

        // Ready should appear before (or at least at the same time as) output
        var ordered = eventOrder.ToArray().Reverse().ToList();
        var readyIndex = ordered.IndexOf("ready");
        var firstOutputIndex = ordered.IndexOf("output");
        Assert.True(readyIndex <= firstOutputIndex,
            "Ready event should fire before first output event");

        await session.CloseAsync();
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
