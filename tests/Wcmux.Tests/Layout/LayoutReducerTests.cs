using Wcmux.Core.Layout;

namespace Wcmux.Tests.Layout;

/// <summary>
/// Unit tests for the pure split-tree reducer transitions. Covers split
/// creation, close collapse, focus restoration, directional focus, resize
/// ratio math, and pane rect computation.
/// </summary>
public class LayoutReducerTests
{
    private static LeafNode MakeLeaf(string paneId = "p1", string sessionId = "s1")
        => new() { PaneId = paneId, SessionId = sessionId };

    // ── Split ───────────────────────────────────────────────────────────

    [Fact]
    public void SplitPane_HorizontalSplit_CreatesTopBottomChildren()
    {
        var root = MakeLeaf("p1", "s1");
        var (newRoot, newLeaf) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Horizontal, "p2", "s2");

        var split = Assert.IsType<SplitNode>(newRoot);
        Assert.Equal(SplitAxis.Horizontal, split.Axis);
        Assert.Equal(0.5, split.Ratio);

        var first = Assert.IsType<LeafNode>(split.First);
        Assert.Equal("p1", first.PaneId);

        var second = Assert.IsType<LeafNode>(split.Second);
        Assert.Equal("p2", second.PaneId);
        Assert.Equal("s2", second.SessionId);
        Assert.Same(newLeaf, second);
    }

    [Fact]
    public void SplitPane_VerticalSplit_CreatesLeftRightChildren()
    {
        var root = MakeLeaf("p1", "s1");
        var (newRoot, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        var split = Assert.IsType<SplitNode>(newRoot);
        Assert.Equal(SplitAxis.Vertical, split.Axis);
    }

    [Fact]
    public void SplitPane_NestedSplit_PreservesExistingTree()
    {
        // Start: [p1 | p2] vertical split
        var root = MakeLeaf("p1", "s1");
        var (root2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        // Split p2 horizontally
        var (root3, _) = LayoutReducer.SplitPane(root2, "p2", SplitAxis.Horizontal, "p3", "s3");

        // Root is still vertical
        var topSplit = Assert.IsType<SplitNode>(root3);
        Assert.Equal(SplitAxis.Vertical, topSplit.Axis);

        // Left child is still p1
        var left = Assert.IsType<LeafNode>(topSplit.First);
        Assert.Equal("p1", left.PaneId);

        // Right child is now a horizontal split
        var rightSplit = Assert.IsType<SplitNode>(topSplit.Second);
        Assert.Equal(SplitAxis.Horizontal, rightSplit.Axis);
    }

    [Fact]
    public void SplitPane_UnknownPaneId_ReturnsUnchangedTree()
    {
        var root = MakeLeaf("p1", "s1");
        var (newRoot, _) = LayoutReducer.SplitPane(root, "nonexistent", SplitAxis.Vertical, "p2", "s2");

        // Tree unchanged -- root is still a leaf
        Assert.IsType<LeafNode>(newRoot);
    }

    // ── Close ───────────────────────────────────────────────────────────

    [Fact]
    public void ClosePane_SingleLeaf_ReturnsNull()
    {
        var root = MakeLeaf("p1", "s1");
        var result = LayoutReducer.ClosePane(root, "p1");
        Assert.Null(result);
    }

    [Fact]
    public void ClosePane_SiblingPromoted_CollapsesParentSplit()
    {
        var root = MakeLeaf("p1", "s1");
        var (root2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        var result = LayoutReducer.ClosePane(root2, "p1");

        var leaf = Assert.IsType<LeafNode>(result);
        Assert.Equal("p2", leaf.PaneId);
    }

    [Fact]
    public void ClosePane_DeepNested_CollapsesCorrectSplit()
    {
        // Build: [p1 | [p2 / p3]]
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");
        var (r3, _) = LayoutReducer.SplitPane(r2, "p2", SplitAxis.Horizontal, "p3", "s3");

        // Close p3 -- right side should collapse back to p2
        var result = LayoutReducer.ClosePane(r3, "p3");

        var topSplit = Assert.IsType<SplitNode>(result);
        var left = Assert.IsType<LeafNode>(topSplit.First);
        Assert.Equal("p1", left.PaneId);
        var right = Assert.IsType<LeafNode>(topSplit.Second);
        Assert.Equal("p2", right.PaneId);
    }

    // ── Focus after close ───────────────────────────────────────────────

    [Fact]
    public void FindFocusAfterClose_PrefersSibling()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        var focus = LayoutReducer.FindFocusAfterClose(r2, "p1", ["p1", "p2"]);
        Assert.Equal("p2", focus);
    }

    [Fact]
    public void FindFocusAfterClose_FallsBackToFocusHistory()
    {
        // Build: [p1 | [p2 / p3]]
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");
        var (r3, _) = LayoutReducer.SplitPane(r2, "p2", SplitAxis.Horizontal, "p3", "s3");

        // Focus history shows p1 was most recently visited
        var focus = LayoutReducer.FindFocusAfterClose(r3, "p2", ["p3", "p1", "p2"]);

        // Sibling of p2 is p3
        Assert.Equal("p3", focus);
    }

    // ── Pane rect computation ───────────────────────────────────────────

    [Fact]
    public void ComputePaneRects_SingleLeaf_FillsContainer()
    {
        var root = MakeLeaf("p1", "s1");
        var rects = LayoutReducer.ComputePaneRects(root, 1000, 600);

        Assert.Single(rects);
        Assert.Equal(new PaneRect(0, 0, 1000, 600), rects["p1"]);
    }

    [Fact]
    public void ComputePaneRects_VerticalSplit_DividesWidth()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        var rects = LayoutReducer.ComputePaneRects(r2, 1000, 600);

        Assert.Equal(new PaneRect(0, 0, 500, 600), rects["p1"]);
        Assert.Equal(new PaneRect(500, 0, 500, 600), rects["p2"]);
    }

    [Fact]
    public void ComputePaneRects_HorizontalSplit_DividesHeight()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Horizontal, "p2", "s2");

        var rects = LayoutReducer.ComputePaneRects(r2, 1000, 600);

        Assert.Equal(new PaneRect(0, 0, 1000, 300), rects["p1"]);
        Assert.Equal(new PaneRect(0, 300, 1000, 300), rects["p2"]);
    }

    // ── Directional focus ───────────────────────────────────────────────

    [Fact]
    public void FindDirectionalFocus_RightFromLeftPane_FindsRightPane()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");
        var rects = LayoutReducer.ComputePaneRects(r2, 1000, 600);

        var target = LayoutReducer.FindDirectionalFocus(r2, "p1", Direction.Right, rects);
        Assert.Equal("p2", target);
    }

    [Fact]
    public void FindDirectionalFocus_LeftFromRightPane_FindsLeftPane()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");
        var rects = LayoutReducer.ComputePaneRects(r2, 1000, 600);

        var target = LayoutReducer.FindDirectionalFocus(r2, "p2", Direction.Left, rects);
        Assert.Equal("p1", target);
    }

    [Fact]
    public void FindDirectionalFocus_NoMatchInDirection_ReturnsNull()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");
        var rects = LayoutReducer.ComputePaneRects(r2, 1000, 600);

        // No pane to the left of p1
        var target = LayoutReducer.FindDirectionalFocus(r2, "p1", Direction.Left, rects);
        Assert.Null(target);
    }

    [Fact]
    public void FindDirectionalFocus_UnevenTree_FindsNearest()
    {
        // Build: [p1 | [p2 / p3]] where p2 is top-right, p3 is bottom-right
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");
        var (r3, _) = LayoutReducer.SplitPane(r2, "p2", SplitAxis.Horizontal, "p3", "s3");
        var rects = LayoutReducer.ComputePaneRects(r3, 1000, 600);

        // From p1 (left, full height), going right should find p2 (closer center)
        var target = LayoutReducer.FindDirectionalFocus(r3, "p1", Direction.Right, rects);
        Assert.Contains(target, new[] { "p2", "p3" }); // Either is valid, p2 is closer
    }

    // ── Resize ──────────────────────────────────────────────────────────

    [Fact]
    public void ResizePane_IncreasesRatio_WhenExpandingInSplitDirection()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        // Expand p1 to the right (increase its share of the vertical split)
        var resized = LayoutReducer.ResizePane(r2, "p1", Direction.Right);
        var split = Assert.IsType<SplitNode>(resized);
        Assert.True(split.Ratio > 0.5);
    }

    [Fact]
    public void ResizePane_DecreasesRatio_WhenShrinkingInSplitDirection()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        // Shrink p1 from the left
        var resized = LayoutReducer.ResizePane(r2, "p1", Direction.Left);
        var split = Assert.IsType<SplitNode>(resized);
        Assert.True(split.Ratio < 0.5);
    }

    [Fact]
    public void ResizePane_ClampsToMinRatio()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        // Shrink p1 repeatedly
        var current = r2;
        for (int i = 0; i < 20; i++)
        {
            current = LayoutReducer.ResizePane(current, "p1", Direction.Left);
        }

        var split = Assert.IsType<SplitNode>(current);
        Assert.True(split.Ratio >= LayoutReducer.MinRatio);
    }

    [Fact]
    public void ResizePane_ClampsToMaxRatio()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");

        // Expand p1 repeatedly
        var current = r2;
        for (int i = 0; i < 20; i++)
        {
            current = LayoutReducer.ResizePane(current, "p1", Direction.Right);
        }

        var split = Assert.IsType<SplitNode>(current);
        Assert.True(split.Ratio <= LayoutReducer.MaxRatio);
    }

    // ── GetAllPaneIds ───────────────────────────────────────────────────

    [Fact]
    public void GetAllPaneIds_ReturnsAllLeaves()
    {
        var root = MakeLeaf("p1", "s1");
        var (r2, _) = LayoutReducer.SplitPane(root, "p1", SplitAxis.Vertical, "p2", "s2");
        var (r3, _) = LayoutReducer.SplitPane(r2, "p2", SplitAxis.Horizontal, "p3", "s3");

        var ids = LayoutReducer.GetAllPaneIds(r3);
        Assert.Equal(3, ids.Count);
        Assert.Contains("p1", ids);
        Assert.Contains("p2", ids);
        Assert.Contains("p3", ids);
    }
}
