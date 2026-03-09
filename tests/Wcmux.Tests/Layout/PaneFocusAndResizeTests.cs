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
    public void FocusRight_InLShapedLayout_DoesNotJumpToFullWidthPane()
    {
        var store = CreateStore();

        // Build: [p1|p2] / [p3 (full-width)]
        // Split horizontally first (top/bottom), then split the top vertically
        store.SplitActivePane(SplitAxis.Horizontal, "p3", "s3");
        store.SetActivePane("p1");
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");

        // From p1 (top-left), going Right should reach p2 (top-right),
        // NOT p3 (full-width bottom) whose center happens to be to the right.
        store.SetActivePane("p1");
        var result = store.FocusDirection(Direction.Right);
        Assert.Equal("p2", result);

        // From p2 (top-right), going Left should reach p1 (top-left),
        // NOT p3 (full-width bottom).
        var result2 = store.FocusDirection(Direction.Left);
        Assert.Equal("p1", result2);
    }

    [Fact]
    public void FocusDirection_ThreeColumnWithFullWidthBottom_NavigatesCorrectly()
    {
        var store = CreateStore();

        // Build: |A|B|C| / |DDDDD|
        // 1. Split horizontally → [A / D]
        store.SplitActivePane(SplitAxis.Horizontal, "D", "sD");
        // 2. Focus A (top), split vertically → [[A|B] / D]
        store.SetActivePane("p1"); // p1 = A
        store.SplitActivePane(SplitAxis.Vertical, "B", "sB");
        // 3. Focus B, split vertically → [[A|[B|C]] / D]
        store.SplitActivePane(SplitAxis.Vertical, "C", "sC");

        // Verify rects are sane
        var rects = store.PaneRects;
        Assert.Equal(4, rects.Count);

        // From A → Right should go to B (immediate neighbor), not C or D
        store.SetActivePane("p1");
        Assert.Equal("B", store.FocusDirection(Direction.Right));

        // From B → Right should go to C
        Assert.Equal("C", store.FocusDirection(Direction.Right));

        // From C → Left should go back to B
        Assert.Equal("B", store.FocusDirection(Direction.Left));

        // From B → Left should go back to A
        Assert.Equal("p1", store.FocusDirection(Direction.Left));

        // From A → Up should return null (nothing above)
        store.SetActivePane("p1");
        Assert.Null(store.FocusDirection(Direction.Up));

        // From A → Down should go to D
        Assert.Equal("D", store.FocusDirection(Direction.Down));

        // From D → Up should find one of A, B, C (nearest by center)
        var upFromD = store.FocusDirection(Direction.Up);
        Assert.Contains(upFromD, new[] { "p1", "B", "C" });
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

    // ── Multi-pane traversal (user-reported scenario) ────────────────────

    [Fact]
    public void FocusLeft_ThreeVerticalPanes_TraversesOneAtATime()
    {
        // Layout: [P1 | P2 | P3]  (binary tree: Split(P1, Split(P2, P3)))
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SetActivePane("p1");
        store.SplitActivePane(SplitAxis.Vertical, "p3", "s3");

        // Tree: Split(Split(P1, P3), P2)  — P3 is right of P1, P2 is rightmost
        // After split on P1 vertical: P1 is left, P3 is right-of-P1
        // Wait, let's just check the actual rects and work from there.
        var rects = store.PaneRects;
        // Find the rightmost pane
        var rightmost = rects.OrderByDescending(r => r.Value.X).First().Key;
        store.SetActivePane(rightmost);

        // Traverse left: should visit each pane one at a time
        var visited = new List<string> { rightmost };
        for (int i = 0; i < 2; i++)
        {
            var result = store.FocusDirection(Direction.Left);
            Assert.NotNull(result);
            Assert.NotEqual(visited.Last(), result);
            visited.Add(result!);
        }

        // Should have visited all 3 distinct panes
        Assert.Equal(3, visited.Distinct().Count());

        // At the leftmost pane, going left again should return null
        Assert.Null(store.FocusDirection(Direction.Left));
    }

    [Fact]
    public void FocusRight_FourVerticalPanes_TraversesOneAtATime()
    {
        // Create 4 vertical panes via successive splits
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Vertical, "p2", "s2");
        store.SplitActivePane(SplitAxis.Vertical, "p3", "s3");
        store.SplitActivePane(SplitAxis.Vertical, "p4", "s4");

        // Find the leftmost pane
        var rects = store.PaneRects;
        var leftmost = rects.OrderBy(r => r.Value.X).First().Key;
        store.SetActivePane(leftmost);

        // Traverse right: should visit each pane one at a time
        var visited = new List<string> { leftmost };
        for (int i = 0; i < 3; i++)
        {
            var result = store.FocusDirection(Direction.Right);
            Assert.NotNull(result);
            Assert.NotEqual(visited.Last(), result);
            visited.Add(result!);
        }

        // Should have visited all 4 distinct panes
        Assert.Equal(4, visited.Distinct().Count());

        // At the rightmost pane, going right again should return null
        Assert.Null(store.FocusDirection(Direction.Right));
    }

    [Fact]
    public void FocusDirection_ComplexGrid_NeverSkipsPanes()
    {
        // Layout: 2x2 grid
        // [P1 | P2]
        // [P3 | P4]
        var store = CreateStore();
        store.SplitActivePane(SplitAxis.Horizontal, "p2", "s2"); // P1 top, P2 bottom
        store.SetActivePane("p1");
        store.SplitActivePane(SplitAxis.Vertical, "p3", "s3");   // P1 top-left, P3 top-right
        store.SetActivePane("p2");
        store.SplitActivePane(SplitAxis.Vertical, "p4", "s4");   // P2 bottom-left, P4 bottom-right

        var rects = store.PaneRects;

        // Verify we have 4 panes
        Assert.Equal(4, rects.Count);

        // From top-left, going right should reach top-right (not bottom)
        var topLeft = rects.Where(r => r.Value.X < 600 && r.Value.Y < 400)
            .Select(r => r.Key).Single();
        var topRight = rects.Where(r => r.Value.X >= 600 && r.Value.Y < 400)
            .Select(r => r.Key).Single();
        var bottomLeft = rects.Where(r => r.Value.X < 600 && r.Value.Y >= 400)
            .Select(r => r.Key).Single();

        store.SetActivePane(topLeft);
        Assert.Equal(topRight, store.FocusDirection(Direction.Right));

        // From top-left, going down should reach bottom-left (not right)
        store.SetActivePane(topLeft);
        Assert.Equal(bottomLeft, store.FocusDirection(Direction.Down));
    }
}
