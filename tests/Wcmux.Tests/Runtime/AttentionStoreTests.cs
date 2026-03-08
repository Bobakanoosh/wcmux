using Wcmux.Core.Runtime;

namespace Wcmux.Tests.Runtime;

/// <summary>
/// Unit tests for AttentionStore: per-pane attention state management
/// with cooldown, focus suppression, clearance, and cleanup.
/// </summary>
public class AttentionStoreTests
{
    private readonly AttentionStore _store = new();

    [Trait("Category", "Attention")]
    [Fact]
    public void RaiseBell_UnfocusedPane_SetsHasAttention()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now);

        Assert.True(_store.HasAttention("pane-1"));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void RaiseBell_FocusedPane_Suppressed()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-1", now);

        Assert.False(_store.HasAttention("pane-1"));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void RaiseBell_WithinCooldown_Suppressed()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now);
        _store.ClearAttention("pane-1");

        // Bell within 5-second cooldown
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now + TimeSpan.FromSeconds(3));

        Assert.False(_store.HasAttention("pane-1"));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void RaiseBell_AfterCooldownExpires_RaisesAttentionAgain()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now);
        _store.ClearAttention("pane-1");

        // Bell after cooldown expires
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now + TimeSpan.FromSeconds(6));

        Assert.True(_store.HasAttention("pane-1"));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void ClearAttention_RemovesAttentionState()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now);

        _store.ClearAttention("pane-1");

        Assert.False(_store.HasAttention("pane-1"));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void HasAttention_UnknownPaneId_ReturnsFalse()
    {
        Assert.False(_store.HasAttention("nonexistent"));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void TabHasAttention_AnyPaneHasAttention_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-3", now);

        Assert.True(_store.TabHasAttention(["pane-1", "pane-2"]));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void TabHasAttention_AllPanesCleared_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-3", now);
        _store.ClearAttention("pane-1");

        Assert.False(_store.TabHasAttention(["pane-1", "pane-2"]));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void RemovePane_CleansUpAllState()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now);

        _store.RemovePane("pane-1");

        Assert.False(_store.HasAttention("pane-1"));

        // After removal, a new bell should work immediately (no leftover cooldown)
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now + TimeSpan.FromSeconds(1));
        Assert.True(_store.HasAttention("pane-1"));
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void AttentionChanged_FiresOnRaise()
    {
        string? changedPaneId = null;
        _store.AttentionChanged += paneId => changedPaneId = paneId;

        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now);

        Assert.Equal("pane-1", changedPaneId);
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void AttentionChanged_FiresOnClear()
    {
        var now = DateTimeOffset.UtcNow;
        _store.RaiseBell("pane-1", activePaneId: "pane-2", now);

        string? changedPaneId = null;
        _store.AttentionChanged += paneId => changedPaneId = paneId;
        _store.ClearAttention("pane-1");

        Assert.Equal("pane-1", changedPaneId);
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void AttentionChanged_DoesNotFireOnSuppressedBell()
    {
        var now = DateTimeOffset.UtcNow;
        int eventCount = 0;
        _store.AttentionChanged += _ => eventCount++;

        // Focused pane - suppressed
        _store.RaiseBell("pane-1", activePaneId: "pane-1", now);

        Assert.Equal(0, eventCount);
    }

    [Trait("Category", "Attention")]
    [Fact]
    public void ClearAttention_NoPriorAttention_DoesNotFireEvent()
    {
        int eventCount = 0;
        _store.AttentionChanged += _ => eventCount++;

        _store.ClearAttention("nonexistent");

        Assert.Equal(0, eventCount);
    }
}
