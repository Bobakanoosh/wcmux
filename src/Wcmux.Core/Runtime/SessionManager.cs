using System.Collections.Concurrent;

namespace Wcmux.Core.Runtime;

/// <summary>
/// Creates, tracks, and tears down terminal sessions. All session lifecycle
/// events are surfaced through <see cref="SessionEventReceived"/>.
/// </summary>
public sealed class SessionManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();

    /// <summary>Fired for every session lifecycle event (ready, output, cwd, resize, exit).</summary>
    public event EventHandler<SessionEvent>? SessionEventReceived;

    /// <summary>All currently tracked sessions.</summary>
    public IReadOnlyCollection<ISession> Sessions => _sessions.Values.ToList().AsReadOnly();

    /// <summary>
    /// Creates a new ConPTY-backed session from the given launch spec.
    /// The session is fully started (pumps running) before this method returns.
    /// </summary>
    public async Task<ISession> CreateSessionAsync(
        SessionLaunchSpec launchSpec,
        CancellationToken cancellationToken = default)
    {
        var session = await ConPtySession.StartAsync(launchSpec, OnSessionEvent, cancellationToken);
        _sessions.TryAdd(session.SessionId, session);
        return session;
    }

    /// <summary>
    /// Closes a specific session by ID. Removes it from tracking after teardown.
    /// </summary>
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.CloseAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Looks up a tracked session by ID.
    /// </summary>
    public ISession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// Disposes all tracked sessions. Used during application shutdown.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
                await session.CloseAsync();
            }
            catch
            {
                // Best-effort during shutdown
            }
        }
        _sessions.Clear();
    }

    private void OnSessionEvent(SessionEvent evt)
    {
        if (evt is SessionExitedEvent)
        {
            _sessions.TryRemove(evt.SessionId, out _);
        }
        SessionEventReceived?.Invoke(this, evt);
    }
}
