using Wcmux.Core.Layout;

namespace Wcmux.Tests.Layout;

/// <summary>
/// Integration coverage for directional focus movement across uneven pane
/// rectangles, keyboard-driven resize behavior with ratio clamping, and
/// repeated split-resize-close loops that exercise the full layout lifecycle.
/// </summary>
public class PaneFocusAndResizeTests
{
    private static LayoutStore CreateStore(string paneId = "p1", string sessionId = "s1")
    {
        var store = new LayoutStore(paneId, sessionId);
        store.UpdateContainerSize(1200, 800);
        return store;
    }

    // ── Directional focus across simple split ───────────────────────────

    [Fact]
    public void FocusRight_FromLeftPane_MovesFocusToRightPane()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");

        var result = store.FocusDirection(Direction.Right);

        Assert.Equal("p2", result);
        Assert.Equal("p2", store.ActivePaneId);
    }

    [Fact]
    public void FocusLeft_FromRightPane_MovesFocusToLeftPane()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        // Focus is on p2 after split
        Assert.Equal("p2", store.ActivePaneId);

        var result = store.FocusDirection(Direction.Left);

        Assert.Equal("p1", result);
        Assert.Equal("p1", store.ActivePaneId);
    }

    [Fact]
    public void FocusDown_FromTopPane_MovesFocusToBottomPane()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Horizontal, "p2", "s2");
        store.SetActivePane("p1");

        var result = store.FocusDirection(Direction.Down);

        Assert.Equal("p2", result);
    }

    [Fact]
    public void FocusUp_FromBottomPane_MovesFocusToTopPane()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Horizontal, "p2", "s2");

        var result = store.FocusDirection(Direction.Up);

        Assert.Equal("p1", result);
    }

    // ── Directional focus across uneven tree ────────────────────────────

    [Fact]
    public void FocusRight_InUnevenTree_FindsNearestPane()
    {
        var store = CreateStore();
        // Build [p1 | [p2 / p3]]
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");

        // Focus p1 (left, full height)
        store.SetActivePane("p1");

        var result = store.FocusDirection(Direction.Right);

        // Should find one of the right-side panes
        Assert.Contains(result, new[] { "p2", "p3" });
    }

    [Fact]
    public void FocusLeft_FromRightPanes_InUnevenTree_FindsLeftPane()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");

        // Focus p3 (bottom-right)
        store.SetActivePane("p3");

        var result = store.FocusDirection(Direction.Left);
        Assert.Equal("p1", result);
    }

    [Fact]
    public void Focus_AtEdge_ReturnsNull()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");

        // No pane to the left of p1
        var result = store.FocusDirection(Direction.Left);
        Assert.Null(result);
        Assert.Equal("p1", store.ActivePaneId); // Focus unchanged
    }

    // ── Keyboard resize ─────────────────────────────────────────────────

    [Fact]
    public void ResizeActivePane_Right_IncreasesFirstChildRatio()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");

        var rectsBefore = store.PaneRects;
        var widthBefore = rectsBefore["p1"].Width;

        store.ResizeActivePane(Direction.Right);

        var rectsAfter = store.PaneRects;
        Assert.True(rectsAfter["p1"].Width > widthBefore);
    }

    [Fact]
    public void ResizeActivePane_Left_DecreasesFirstChildRatio()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");

        var rectsBefore = store.PaneRects;
        var widthBefore = rectsBefore["p1"].Width;

        store.ResizeActivePane(Direction.Left);

        var rectsAfter = store.PaneRects;
        Assert.True(rectsAfter["p1"].Width < widthBefore);
    }

    [Fact]
    public void ResizeActivePane_RepeatedRight_ClampsAtMaxRatio()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");

        for (int i = 0; i < 30; i++)
        {
            store.ResizeActivePane(Direction.Right);
        }

        var rects = store.PaneRects;
        // p1 should not take more than MaxRatio (0.9) of the container
        Assert.True(rects["p1"].Width <= 1200 * LayoutReducer.MaxRatio + 1);
        // p2 should still have some space
        Assert.True(rects["p2"].Width > 0);
    }

    [Fact]
    public void ResizeActivePane_RepeatedLeft_ClampsAtMinRatio()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");

        for (int i = 0; i < 30; i++)
        {
            store.ResizeActivePane(Direction.Left);
        }

        var rects = store.PaneRects;
        // p1 should not shrink below MinRatio (0.1) of the container
        Assert.True(rects["p1"].Width >= 1200 * LayoutReducer.MinRatio - 1);
    }

    [Fact]
    public void ResizeActivePane_Down_InHorizontalSplit_IncreasesTopPane()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Horizontal, "p2", "s2");
        store.SetActivePane("p1");

        var heightBefore = store.PaneRects["p1"].Height;

        store.ResizeActivePane(Direction.Down);

        Assert.True(store.PaneRects["p1"].Height > heightBefore);
    }

    // ── Close behavior and focus restoration ────────────────────────────

    [Fact]
    public void ClosePane_RestoresFocusToSibling()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        // Focus is on p2, close it
        store.ClosePane("p2");

        Assert.Equal("p1", store.ActivePaneId);
        Assert.True(store.IsSinglePane);
    }

    [Fact]
    public void ClosePane_InDeepTree_RestoresFocusToRelatedPane()
    {
        var store = CreateStore();
        // Build [p1 | [p2 / p3]]
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");

        // Close p2 -- sibling p3 should get focus
        store.SetActivePane("p2");
        store.ClosePane("p2");

        Assert.Equal("p3", store.ActivePaneId);
        Assert.Equal(2, store.AllPaneIds.Count);
    }

    [Fact]
    public void CloseActivePane_ClosesCurrentAndRestoresFocus()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        store.CloseActivePane();

        Assert.Equal("p1", store.ActivePaneId);
        Assert.True(store.IsSinglePane);
    }

    // ── Repeated split-resize-close loops ───────────────────────────────

    [Fact]
    public void RepeatedSplitResizeClose_MaintainsConsistentState()
    {
        var store = CreateStore();

        for (int loop = 0; loop < 5; loop++)
        {
            // Split vertically
            var newPaneId = $"loop{loop}";
            store.SplitActivePane(SplitAxis.Vertical, newPaneId, $"s{loop}");
            Assert.Equal(newPaneId, store.ActivePaneId);

            // Resize a few times
            store.ResizeActivePane(Direction.Right);
            store.ResizeActivePane(Direction.Right);
            store.ResizeActivePane(Direction.Left);

            // Close the new pane
            store.ClosePane(newPaneId);
            Assert.Equal("p1", store.ActivePaneId);
            Assert.True(store.IsSinglePane);
        }

        // After all loops, should still have one healthy pane
        Assert.Single(store.AllPaneIds);
        Assert.Equal("p1", store.AllPaneIds[0]);
    }

    [Fact]
    public void SplitResizeClose_WithMixedAxes_MaintainsTreeIntegrity()
    {
        var store = CreateStore();

        // Build a 4-pane layout: [[p1|p2] / [p3|p4]]
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");
        store.SetActivePane("p2");
        store.SplitActivePane(SplitAxis.Horizontal, "p4", "s4");

        Assert.Equal(4, store.AllPaneIds.Count);

        // Resize some panes
        store.SetActivePane("p1");
        store.ResizeActivePane(Direction.Right);
        store.ResizeActivePane(Direction.Down);

        // Close p3 -- p1 should survive as sibling
        store.SetActivePane("p3");
        store.ClosePane("p3");

        Assert.Equal(3, store.AllPaneIds.Count);
        Assert.Contains("p1", store.AllPaneIds);
        Assert.Contains("p2", store.AllPaneIds);
        Assert.Contains("p4", store.AllPaneIds);

        // Close p4
        store.SetActivePane("p4");
        store.ClosePane("p4");

        Assert.Equal(2, store.AllPaneIds.Count);

        // Close p2
        store.SetActivePane("p2");
        store.ClosePane("p2");

        Assert.True(store.IsSinglePane);
        Assert.Equal("p1", store.ActivePaneId);
    }

    // ── Focus direction with 4-pane grid ────────────────────────────────

    [Fact]
    public void FocusDirection_InGridLayout_NavigatesCorrectly()
    {
        var store = CreateStore();

        // Build: [[p1 / p3] | [p2 / p4]] (2x2 grid)
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");
        store.SetActivePane("p2");
        store.SplitActivePane(SplitAxis.Horizontal, "p4", "s4");

        // Start at p1 (top-left)
        store.SetActivePane("p1");

        // Right -> p2 (top-right)
        var right = store.FocusDirection(Direction.Right);
        Assert.Equal("p2", right);

        // Down -> p4 (bottom-right)
        var down = store.FocusDirection(Direction.Down);
        Assert.Equal("p4", down);

        // Left -> p3 (bottom-left)
        var left = store.FocusDirection(Direction.Left);
        Assert.Equal("p3", left);

        // Up -> p1 (top-left)
        var up = store.FocusDirection(Direction.Up);
        Assert.Equal("p1", up);
    }

    // ── Event firing ────────────────────────────────────────────────────

    [Fact]
    public void LayoutChanged_FiresOnSplit()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1200, 800);

        bool fired = false;
        store.LayoutChanged += () => fired = true;

        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        Assert.True(fired);
    }

    [Fact]
    public void ActivePaneChanged_FiresOnFocusChange()
    {
        var store = CreateStore();
        store.UpdateContainerSize(1200, 800);
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        string? changedTo = null;
        store.ActivePaneChanged += paneId => changedTo = paneId;

        store.SetActivePane("p1");

        Assert.Equal("p1", changedTo);
    }

    [Fact]
    public void LayoutChanged_FiresOnResize()
    {
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");

        bool fired = false;
        store.LayoutChanged += () => fired = true;

        store.ResizeActivePane(Direction.Right);

        Assert.True(fired);
    }
}
