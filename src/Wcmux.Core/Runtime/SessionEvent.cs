namespace Wcmux.Core.Runtime;

public abstract record SessionEvent(string SessionId);

public sealed record SessionReadyEvent(string SessionId) : SessionEvent(SessionId);

public sealed record SessionOutputEvent(string SessionId, string Text) : SessionEvent(SessionId);

public sealed record SessionCwdChangedEvent(string SessionId, string WorkingDirectory) : SessionEvent(SessionId);

public sealed record SessionResizedEvent(string SessionId, int Columns, int Rows) : SessionEvent(SessionId);

public sealed record SessionExitedEvent(string SessionId, int? ExitCode) : SessionEvent(SessionId);
