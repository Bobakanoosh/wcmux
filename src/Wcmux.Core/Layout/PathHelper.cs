namespace Wcmux.Core.Layout;

/// <summary>
/// Static helper for path display truncation used in tab labels
/// and pane border titles.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Truncates a working directory path from the left for display.
    /// Replaces the user home directory with ~. Long paths are prefixed
    /// with ".../" showing the last segments that fit within maxLength.
    /// </summary>
    public static string TruncateCwdFromLeft(string? path, int maxLength = 30)
    {
        if (string.IsNullOrEmpty(path)) return "";

        // Replace home directory with ~
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) &&
            path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            path = "~" + path[home.Length..];
        }

        // Normalize separators for display
        path = path.Replace('\\', '/');

        if (path.Length <= maxLength) return path;

        // Truncate from left: .../last/segments
        var segments = path.Split('/');
        if (segments.Length <= 1) return path;

        var result = segments[^1];
        for (int i = segments.Length - 2; i >= 0; i--)
        {
            var candidate = segments[i] + "/" + result;
            if (candidate.Length + 4 > maxLength) // 4 for ".../"
            {
                return ".../" + result;
            }
            result = candidate;
        }
        return result;
    }

    /// <summary>
    /// Formats a path for use as a tab label. Returns "~" for the home
    /// directory, or the last 2 path segments for deeper paths.
    /// </summary>
    public static string FormatTabLabel(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) &&
            string.Equals(path, home, StringComparison.OrdinalIgnoreCase))
        {
            return "~";
        }

        // Replace home prefix with ~
        if (!string.IsNullOrEmpty(home) &&
            path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
        {
            path = "~" + path[home.Length..];
        }

        // Normalize separators
        path = path.Replace('\\', '/');

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 2) return string.Join("/", segments);

        // Return last 2 segments
        return segments[^2] + "/" + segments[^1];
    }
}
