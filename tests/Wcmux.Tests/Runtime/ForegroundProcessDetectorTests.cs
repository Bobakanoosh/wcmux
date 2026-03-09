using Wcmux.Core.Runtime;

namespace Wcmux.Tests.Runtime;

/// <summary>
/// Tests for ToolHelp32-based foreground process detection.
/// </summary>
public class ForegroundProcessDetectorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void GetForegroundProcessName_ZeroPid_ReturnsNull()
    {
        var result = ForegroundProcessDetector.GetForegroundProcessName(0);
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetForegroundProcessName_InvalidPid_ReturnsNull()
    {
        // Use a very high PID that is unlikely to exist
        var result = ForegroundProcessDetector.GetForegroundProcessName(int.MaxValue);
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetForegroundProcessName_CurrentProcess_ReturnsNameWithoutExe()
    {
        // Current process will be dotnet or testhost -- should return a string
        var pid = Environment.ProcessId;
        var result = ForegroundProcessDetector.GetForegroundProcessName(pid);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.DoesNotContain(".exe", result, StringComparison.OrdinalIgnoreCase);
    }
}
