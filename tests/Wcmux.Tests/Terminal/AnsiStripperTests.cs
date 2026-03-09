using Wcmux.Core.Terminal;

namespace Wcmux.Tests.Terminal;

/// <summary>
/// Unit tests for AnsiStripper: verifies correct removal of VT escape
/// sequences, OSC sequences, charset designations, and control characters
/// while preserving printable text and standard whitespace.
/// </summary>
public class AnsiStripperTests
{
    [Fact]
    public void Strip_PlainText_ReturnsUnchanged()
    {
        Assert.Equal("hello", AnsiStripper.Strip("hello"));
    }

    [Fact]
    public void Strip_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", AnsiStripper.Strip(""));
    }

    [Fact]
    public void Strip_CsiColorCodes_Removed()
    {
        Assert.Equal("red", AnsiStripper.Strip("\x1b[31mred\x1b[0m"));
    }

    [Fact]
    public void Strip_OscWithBellTerminator_Removed()
    {
        Assert.Equal("rest", AnsiStripper.Strip("\x1b]0;window title\x07rest"));
    }

    [Fact]
    public void Strip_OscWithStTerminator_Removed()
    {
        Assert.Equal("rest", AnsiStripper.Strip("\x1b]0;title\x1b\\rest"));
    }

    [Fact]
    public void Strip_CharsetDesignationAndCsi_Removed()
    {
        Assert.Equal("", AnsiStripper.Strip("\x1b(B\x1b[m"));
    }

    [Fact]
    public void Strip_ControlCharsExceptNewlineTabCr_Removed()
    {
        Assert.Equal("helloworld", AnsiStripper.Strip("hello\x01\x02world"));
    }

    [Fact]
    public void Strip_PreservesNewlineTabCr()
    {
        Assert.Equal("line1\nline2\ttab\rreturn", AnsiStripper.Strip("line1\nline2\ttab\rreturn"));
    }

    [Fact]
    public void Strip_RealPowerShellPrompt_ExtractsText()
    {
        Assert.Equal("PS C:\\Users> ", AnsiStripper.Strip("\x1b[1;32mPS C:\\Users>\x1b[0m "));
    }

    [Fact]
    public void Strip_MixedContent_PreservesAllPrintableAndWhitespace()
    {
        var input = "\x1b[1m\x1b[34mhello\x1b[0m world\n\x1b[32mgreen\x1b[0m";
        Assert.Equal("hello world\ngreen", AnsiStripper.Strip(input));
    }

    [Fact]
    public void Strip_MultipleCsiSequences_AllRemoved()
    {
        Assert.Equal("abc", AnsiStripper.Strip("\x1b[1m\x1b[31m\x1b[4ma\x1b[22m\x1b[39m\x1b[24mb\x1b[0mc"));
    }

    [Fact]
    public void Strip_TwoCharEscapeSequences_Removed()
    {
        // ESC = (set alternate charset), ESC > (numeric keypad mode)
        Assert.Equal("text", AnsiStripper.Strip("\x1b=text\x1b>"));
    }

    [Fact]
    public void Strip_BareEscape_Removed()
    {
        // Bare ESC at end of string (not followed by a recognized sequence char)
        Assert.Equal("text", AnsiStripper.Strip("text\x1b"));
    }
}
