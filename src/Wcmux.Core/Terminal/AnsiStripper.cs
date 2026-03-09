using System.Text.RegularExpressions;

namespace Wcmux.Core.Terminal;

/// <summary>
/// Strips ANSI/VT escape sequences and non-printable control characters
/// from terminal output, yielding plain readable text suitable for
/// preview display in the sidebar tab list.
/// </summary>
public static partial class AnsiStripper
{
    // Order matters: match longer/more-specific patterns first.
    // 1. CSI sequences:  ESC [ (params) (intermediates) final-byte
    // 2. OSC sequences:  ESC ] ... (BEL | ST)
    // 3. Charset designation: ESC ( char  or  ESC ) char
    // 4. Two-char ESC sequences: ESC followed by a single char (@ - ~ range)
    // 5. Bare ESC (if nothing else matched)
    // 6. Control chars except \n \r \t
    [GeneratedRegex(
        @"\x1b\[[0-9;]*[A-Za-z]"            // CSI sequences
        + @"|\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)" // OSC sequences (BEL or ST terminator)
        + @"|\x1b[()][A-Za-z0-9]"            // Charset designations
        + @"|\x1b[\x20-\x7e]"                 // Two-char ESC sequences
        + @"|\x1b"                            // Bare ESC
        + @"|[\x00-\x08\x0b\x0c\x0e-\x1a\x1c-\x1f\x7f]", // Control chars (keep \t \n \r)
        RegexOptions.Compiled)]
    private static partial Regex AnsiPattern();

    /// <summary>
    /// Removes all ANSI/VT escape sequences and control characters from
    /// the input string, preserving printable text plus \n, \r, and \t.
    /// </summary>
    public static string Strip(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return AnsiPattern().Replace(input, string.Empty);
    }
}
