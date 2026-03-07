namespace Wcmux.Core.Runtime;

/// <summary>
/// Describes how to launch a terminal session. Shell-agnostic by design so
/// the runtime can host pwsh, cmd, wsl, or any other Windows shell.
/// </summary>
public sealed record SessionLaunchSpec(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string InitialWorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    string ProfileKind,
    short InitialColumns = 120,
    short InitialRows = 30)
{
    /// <summary>
    /// Creates a launch spec for the default PowerShell session.
    /// Prefers pwsh.exe (PowerShell 7+) and falls back to powershell.exe
    /// if pwsh is not on the PATH.
    /// </summary>
    public static SessionLaunchSpec CreateDefaultPowerShell(string workingDirectory)
    {
        var executable = ResolvePowerShellExecutable();
        return new SessionLaunchSpec(
            executable,
            ["-NoLogo", "-NoProfile"],
            workingDirectory,
            new Dictionary<string, string?>(),
            "pwsh",
            InitialColumns: 120,
            InitialRows: 30);
    }

    private static string ResolvePowerShellExecutable()
    {
        // Prefer PowerShell 7+ (pwsh.exe) over legacy Windows PowerShell
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, "pwsh.exe");
            if (File.Exists(candidate))
                return "pwsh.exe";
        }
        return "powershell.exe";
    }
}
