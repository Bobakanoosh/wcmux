namespace Wcmux.Core.Runtime;

/// <summary>
/// Per-pane attention state management. Tracks which panes have pending
/// attention (triggered by terminal bell), enforces cooldown to debounce
/// rapid bells, suppresses bells from the focused pane, and clears
/// attention when a pane receives focus.
/// </summary>
public sealed class AttentionStore
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, bool> _paneAttention = new();
    private readonly Dictionary<string, DateTimeOffset> _lastBellTime = new();

    /// <summary>
    /// Fired with the pane ID whenever attention state changes (raised or cleared).
    /// </summary>
    public event Action<string>? AttentionChanged;

    /// <summary>
    /// Returns whether the specified pane currently has attention.
    /// Returns false for unknown pane IDs.
    /// </summary>
    public bool HasAttention(string paneId)
    {
        return _paneAttention.TryGetValue(paneId, out var has) && has;
    }

    /// <summary>
    /// Returns true if any pane in the given set has attention.
    /// </summary>
    public bool TabHasAttention(IEnumerable<string> paneIds)
    {
        foreach (var paneId in paneIds)
        {
            if (HasAttention(paneId))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Raises a bell for the specified pane. The bell is suppressed if:
    /// - The pane is the currently focused pane (paneId == activePaneId)
    /// - The bell arrives within the cooldown window of the last bell for this pane
    /// Otherwise, sets attention state and fires AttentionChanged.
    /// </summary>
    /// <param name="paneId">The pane that received the bell.</param>
    /// <param name="activePaneId">The currently focused pane ID.</param>
    /// <param name="now">Current timestamp for deterministic cooldown checks.</param>
    public void RaiseBell(string paneId, string activePaneId, DateTimeOffset now)
    {
        // Suppress bells from the focused pane
        if (paneId == activePaneId)
            return;

        // Suppress if within cooldown window
        if (_lastBellTime.TryGetValue(paneId, out var lastBell)
            && (now - lastBell) < Cooldown)
            return;

        _paneAttention[paneId] = true;
        _lastBellTime[paneId] = now;
        AttentionChanged?.Invoke(paneId);
    }

    /// <summary>
    /// Clears attention state for the specified pane. Fires AttentionChanged
    /// only if the pane actually had attention.
    /// </summary>
    public void ClearAttention(string paneId)
    {
        if (_paneAttention.TryGetValue(paneId, out var has) && has)
        {
            _paneAttention[paneId] = false;
            AttentionChanged?.Invoke(paneId);
        }
    }

    /// <summary>
    /// Removes all state (attention and cooldown) for a closed pane.
    /// </summary>
    public void RemovePane(string paneId)
    {
        _paneAttention.Remove(paneId);
        _lastBellTime.Remove(paneId);
    }
}
