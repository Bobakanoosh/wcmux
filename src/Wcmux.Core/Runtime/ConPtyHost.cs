namespace Wcmux.Core.Runtime;

public sealed class ConPtyHost
{
    public static Task<ConPtyHost> StartAsync(SessionLaunchSpec launchSpec, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ConPtyHost());
    }
}
