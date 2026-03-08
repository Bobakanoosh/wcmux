using Wcmux.Core.Layout;

namespace Wcmux.Tests.Layout;

/// <summary>
/// Unit tests for PathHelper: path display truncation for tab labels
/// and pane border titles.
/// </summary>
public class PathHelperTests
{
    private static readonly string Home =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ── TruncateCwdFromLeft ──────────────────────────────────────────────

    [Fact]
    public void TruncateCwdFromLeft_Null_ReturnsEmpty()
    {
        Assert.Equal("", PathHelper.TruncateCwdFromLeft(null!));
    }

    [Fact]
    public void TruncateCwdFromLeft_Empty_ReturnsEmpty()
    {
        Assert.Equal("", PathHelper.TruncateCwdFromLeft(""));
    }

    [Fact]
    public void TruncateCwdFromLeft_HomeDir_ReturnsTilde()
    {
        Assert.Equal("~", PathHelper.TruncateCwdFromLeft(Home));
    }

    [Fact]
    public void TruncateCwdFromLeft_UnderHome_ReplacesPrefixWithTilde()
    {
        var path = Path.Combine(Home, "projects");
        var result = PathHelper.TruncateCwdFromLeft(path);

        Assert.StartsWith("~", result);
        Assert.Contains("projects", result);
    }

    [Fact]
    public void TruncateCwdFromLeft_ShortPath_ReturnedAsIs()
    {
        var path = Path.Combine(Home, "code");
        var result = PathHelper.TruncateCwdFromLeft(path, maxLength: 50);

        // Short enough -- no truncation, just home replacement
        Assert.DoesNotContain("...", result);
    }

    [Fact]
    public void TruncateCwdFromLeft_LongPath_TruncatedFromLeft()
    {
        var path = Path.Combine(Home, "very", "deeply", "nested", "project", "directory", "structure");
        var result = PathHelper.TruncateCwdFromLeft(path, maxLength: 20);

        Assert.StartsWith(".../", result);
        Assert.True(result.Length <= 24); // maxLength + some tolerance for .../
    }

    [Fact]
    public void TruncateCwdFromLeft_SingleSegment_NeverTruncated()
    {
        var result = PathHelper.TruncateCwdFromLeft("C:", maxLength: 5);

        Assert.DoesNotContain("...", result);
    }

    [Fact]
    public void TruncateCwdFromLeft_NotUnderHome_NoTildeReplacement()
    {
        var result = PathHelper.TruncateCwdFromLeft(@"D:\other\path");

        Assert.DoesNotContain("~", result);
    }

    [Fact]
    public void TruncateCwdFromLeft_UncPath_DoesNotCrash()
    {
        var result = PathHelper.TruncateCwdFromLeft(@"\\server\share\folder");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    // ── FormatTabLabel ───────────────────────────────────────────────────

    [Fact]
    public void FormatTabLabel_HomeDir_ReturnsTilde()
    {
        Assert.Equal("~", PathHelper.FormatTabLabel(Home));
    }

    [Fact]
    public void FormatTabLabel_UnderHome_ReturnsLastTwoSegments()
    {
        var path = Path.Combine(Home, "projects", "myapp");
        var result = PathHelper.FormatTabLabel(path);

        Assert.Contains("projects", result);
        Assert.Contains("myapp", result);
    }

    [Fact]
    public void FormatTabLabel_SingleFolderUnderHome_ReturnsTildeSlashFolder()
    {
        var path = Path.Combine(Home, "code");
        var result = PathHelper.FormatTabLabel(path);

        Assert.Equal("~/code", result);
    }

    [Fact]
    public void FormatTabLabel_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", PathHelper.FormatTabLabel(null!));
        Assert.Equal("", PathHelper.FormatTabLabel(""));
    }
}
