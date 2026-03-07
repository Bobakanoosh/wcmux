namespace Wcmux.Core.Runtime;

public sealed record SessionLaunchSpec(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string InitialWorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    string ProfileKind,
    short InitialColumns = 120,
    short InitialRows = 30)
{
    public static SessionLaunchSpec CreateDefaultPowerShell(string workingDirectory)
    {
        return new SessionLaunchSpec(
            "powershell.exe",
            ["-NoLogo"],
            workingDirectory,
            new Dictionary<string, string?>(),
            "powershell");
    }
}
