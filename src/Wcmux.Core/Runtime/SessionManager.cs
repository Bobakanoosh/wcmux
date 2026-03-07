namespace Wcmux.Core.Runtime;

public sealed class SessionManager
{
    public event EventHandler<SessionEvent>? SessionEventReceived;

    public Task<string> CreateSessionAsync(SessionLaunchSpec launchSpec, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        SessionEventReceived?.Invoke(this, new SessionReadyEvent(sessionId));
        return Task.FromResult(sessionId);
    }
}
