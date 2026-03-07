using Wcmux.Core.Layout;
using Wcmux.Core.Runtime;
using Wcmux.Tests.Terminal;

namespace Wcmux.Tests.Layout;

/// <summary>
/// Tests for split command semantics exercised through the LayoutStore,
/// covering horizontal and vertical split creation, cwd inheritance
/// expectations, and the split-tree shape after successive operations.
/// </summary>
public class SplitCommandsTests
{
    private static LayoutStore CreateStore(string paneId = "p1", string sessionId = "s1")
        => new(paneId, sessionId);

    // ── Horizontal split ────────────────────────────────────────────────

    [Fact]
    public void HorizontalSplit_CreatesNewPane_MoveFocusToIt()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1000, 600);

        var (newPaneId, _) = store.SplitActivePane(SplitAxis.Horizontal, "p2", "s2");

        Assert.Equal("p2", newPaneId);
        Assert.Equal("p2", store.ActivePaneId);
        Assert.Equal(2, store.AllPaneIds.Count);
    }

    [Fact]
    public void HorizontalSplit_DividesHeightEvenly()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1000, 600);

        store.SplitActivePane(SplitAxis.Horizontal, "p2", "s2");

        var rects = store.PaneRects;
        Assert.Equal(300, rects["p1"].Height, 0.01);
        Assert.Equal(300, rects["p2"].Height, 0.01);
        Assert.Equal(1000, rects["p1"].Width, 0.01);
    }

    // ── Vertical split ──────────────────────────────────────────────────

    [Fact]
    public void VerticalSplit_CreatesLeftRightPanes()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1000, 600);

        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        var rects = store.PaneRects;
        Assert.Equal(500, rects["p1"].Width, 0.01);
        Assert.Equal(500, rects["p2"].Width, 0.01);
    }

    // ── Successive splits ───────────────────────────────────────────────

    [Fact]
    public void SuccessiveSplits_CreateCorrectTreeShape()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1000, 600);

        // Split p1 vertically -> [p1 | p2], focus moves to p2
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        Assert.Equal("p2", store.ActivePaneId);

        // Split p2 horizontally -> [p1 | [p2 / p3]], focus moves to p3
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");
        Assert.Equal("p3", store.ActivePaneId);

        Assert.Equal(3, store.AllPaneIds.Count);

        // Verify rects make geometric sense
        var rects = store.PaneRects;
        // p1 should be on the left half
        Assert.Equal(0, rects["p1"].X, 0.01);
        Assert.Equal(500, rects["p1"].Width, 0.01);
        // p2 should be top-right
        Assert.Equal(500, rects["p2"].X, 0.01);
        Assert.True(rects["p2"].Height < 600);
        // p3 should be bottom-right
        Assert.Equal(500, rects["p3"].X, 0.01);
        Assert.True(rects["p3"].Y > 0);
    }

    // ── Close after split ───────────────────────────────────────────────

    [Fact]
    public void ClosePane_AfterSplit_CollapsesTree()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1000, 600);

        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        Assert.Equal("p2", store.ActivePaneId);

        // Close p2 -- should go back to single pane p1
        store.ClosePane("p2");

        Assert.True(store.IsSinglePane);
        Assert.Equal("p1", store.ActivePaneId);
    }

    [Fact]
    public void ClosePane_InUnevenTree_RestoresFocusDeterministically()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1000, 600);

        // Build [p1 | [p2 / p3]]
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");

        // Focus p2 first, then back to p3
        store.SetActivePane("p2");
        store.SetActivePane("p3");

        // Close p3 -- sibling p2 should get focus
        store.ClosePane("p3");
        Assert.Equal("p2", store.ActivePaneId);
    }

    // ── Session ID tracking ─────────────────────────────────────────────

    [Fact]
    public void SplitPane_TracksSessionIds()
    {
        var store = CreateStore("p1", "s1");
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        Assert.Equal("s1", store.GetSessionId("p1"));
        Assert.Equal("s2", store.GetSessionId("p2"));
    }

    // ── Focus history ───────────────────────────────────────────────────

    [Fact]
    public void FocusHistory_TracksPaneVisits()
    {
        var store = CreateStore("p1", "s1");
        store.UpdateContainerSize(1000, 600);

        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");
        store.SetActivePane("p2");

        var history = store.FocusHistory;
        Assert.Equal("p1", history[^2]);
        Assert.Equal("p2", history[^1]);
    }

    // ── Container size update ───────────────────────────────────────────

    [Fact]
    public void UpdateContainerSize_RecomputesPaneRects()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1000, 600);

        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        var rects1 = store.PaneRects;
        Assert.Equal(500, rects1["p1"].Width, 0.01);

        // Resize container
        store.UpdateContainerSize(2000, 600);
        var rects2 = store.PaneRects;
        Assert.Equal(1000, rects2["p1"].Width, 0.01);
    }
}
