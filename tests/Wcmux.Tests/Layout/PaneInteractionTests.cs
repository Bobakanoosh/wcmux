using Wcmux.Core.Layout;

namespace Wcmux.Tests.Layout;

public class PaneInteractionTests
{
    #region SetSplitRatio Tests

    [Fact]
    public void SetSplitRatio_UpdatesRatioOnMatchingSplitNode()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.SetSplitRatio(split, split.NodeId, 0.7);

        var resultSplit = Assert.IsType<SplitNode>(result);
        Assert.Equal(0.7, resultSplit.Ratio);
    }

    [Fact]
    public void SetSplitRatio_ClampsToMinRatio()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.SetSplitRatio(split, split.NodeId, 0.01);

        var resultSplit = Assert.IsType<SplitNode>(result);
        Assert.Equal(LayoutReducer.MinRatio, resultSplit.Ratio);
    }

    [Fact]
    public void SetSplitRatio_ClampsToMaxRatio()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.SetSplitRatio(split, split.NodeId, 0.99);

        var resultSplit = Assert.IsType<SplitNode>(result);
        Assert.Equal(LayoutReducer.MaxRatio, resultSplit.Ratio);
    }

    [Fact]
    public void SetSplitRatio_NonExistentNodeId_ReturnsUnchangedTree()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.SetSplitRatio(split, "nonexistent", 0.7);

        Assert.True(ReferenceEquals(split, result));
    }

    [Fact]
    public void SetSplitRatio_WorksOnNestedSplitNodes()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var leafC = new LeafNode { PaneId = "c", SessionId = "s3" };
        var innerSplit = new SplitNode { Axis = SplitAxis.Horizontal, Ratio = 0.5, First = leafB, Second = leafC };
        var outerSplit = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = innerSplit };

        var result = LayoutReducer.SetSplitRatio(outerSplit, innerSplit.NodeId, 0.3);

        var resultOuter = Assert.IsType<SplitNode>(result);
        var resultInner = Assert.IsType<SplitNode>(resultOuter.Second);
        Assert.Equal(0.3, resultInner.Ratio);
        Assert.Equal(0.5, resultOuter.Ratio); // outer unchanged
    }

    #endregion

    #region SwapPanes Tests

    [Fact]
    public void SwapPanes_SwapsLeafContent()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1", Kind = PaneKind.Terminal };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2", Kind = PaneKind.Browser };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.SwapPanes(split, "a", "b");

        var resultSplit = Assert.IsType<SplitNode>(result);
        var first = Assert.IsType<LeafNode>(resultSplit.First);
        var second = Assert.IsType<LeafNode>(resultSplit.Second);

        // First position now has B's data
        Assert.Equal("b", first.PaneId);
        Assert.Equal("s2", first.SessionId);
        Assert.Equal(PaneKind.Browser, first.Kind);

        // Second position now has A's data
        Assert.Equal("a", second.PaneId);
        Assert.Equal("s1", second.SessionId);
        Assert.Equal(PaneKind.Terminal, second.Kind);
    }

    [Fact]
    public void SwapPanes_PreservesTreeStructure()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.6, First = leafA, Second = leafB };

        var result = LayoutReducer.SwapPanes(split, "a", "b");

        var resultSplit = Assert.IsType<SplitNode>(result);
        Assert.Equal(SplitAxis.Vertical, resultSplit.Axis);
        Assert.Equal(0.6, resultSplit.Ratio);
    }

    [Fact]
    public void SwapPanes_SamePane_ReturnsUnchangedTree()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.SwapPanes(split, "a", "a");

        Assert.True(ReferenceEquals(split, result));
    }

    [Fact]
    public void SwapPanes_NonExistentPane_ReturnsUnchangedTree()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.SwapPanes(split, "a", "nonexistent");

        Assert.True(ReferenceEquals(split, result));
    }

    #endregion

    #region MovePaneToTarget Tests

    [Fact]
    public void MovePaneToTarget_DetachesAndReinserts()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var leafC = new LeafNode { PaneId = "c", SessionId = "s3" };
        var innerSplit = new SplitNode { Axis = SplitAxis.Horizontal, Ratio = 0.5, First = leafB, Second = leafC };
        var root = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = innerSplit };

        // Move A to the right of C
        var result = LayoutReducer.MovePaneToTarget(root, "a", "c", Direction.Right);

        // A should no longer be a direct child at top level
        // Tree should contain all three panes
        var allPanes = LayoutReducer.GetAllPaneIds(result);
        Assert.Contains("a", allPanes);
        Assert.Contains("b", allPanes);
        Assert.Contains("c", allPanes);
        Assert.Equal(3, allPanes.Count);
    }

    [Fact]
    public void MovePaneToTarget_LeftRight_CreatesVerticalSplit()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var leafC = new LeafNode { PaneId = "c", SessionId = "s3" };
        var innerSplit = new SplitNode { Axis = SplitAxis.Horizontal, Ratio = 0.5, First = leafB, Second = leafC };
        var root = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = innerSplit };

        var result = LayoutReducer.MovePaneToTarget(root, "a", "b", Direction.Right);

        // Find the split containing pane "a" and "b" - it should be Vertical
        var newSplit = FindSplitContainingPane(result, "a");
        Assert.NotNull(newSplit);
        Assert.Equal(SplitAxis.Vertical, newSplit!.Axis);
    }

    [Fact]
    public void MovePaneToTarget_UpDown_CreatesHorizontalSplit()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var leafC = new LeafNode { PaneId = "c", SessionId = "s3" };
        var innerSplit = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafB, Second = leafC };
        var root = new SplitNode { Axis = SplitAxis.Horizontal, Ratio = 0.5, First = leafA, Second = innerSplit };

        var result = LayoutReducer.MovePaneToTarget(root, "a", "b", Direction.Down);

        var newSplit = FindSplitContainingPane(result, "a");
        Assert.NotNull(newSplit);
        Assert.Equal(SplitAxis.Horizontal, newSplit!.Axis);
    }

    [Fact]
    public void MovePaneToTarget_LeftUp_InsertsSourceAsFirst()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.MovePaneToTarget(split, "a", "b", Direction.Left);

        var newSplit = FindSplitContainingPane(result, "a");
        Assert.NotNull(newSplit);
        var firstLeaf = Assert.IsType<LeafNode>(newSplit!.First);
        Assert.Equal("a", firstLeaf.PaneId);
    }

    [Fact]
    public void MovePaneToTarget_RightDown_InsertsSourceAsSecond()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.MovePaneToTarget(split, "a", "b", Direction.Right);

        var newSplit = FindSplitContainingPane(result, "a");
        Assert.NotNull(newSplit);
        var secondLeaf = Assert.IsType<LeafNode>(newSplit!.Second);
        Assert.Equal("a", secondLeaf.PaneId);
    }

    [Fact]
    public void MovePaneToTarget_SourceEqualsTarget_ReturnsUnchangedTree()
    {
        var leafA = new LeafNode { PaneId = "a", SessionId = "s1" };
        var leafB = new LeafNode { PaneId = "b", SessionId = "s2" };
        var split = new SplitNode { Axis = SplitAxis.Vertical, Ratio = 0.5, First = leafA, Second = leafB };

        var result = LayoutReducer.MovePaneToTarget(split, "a", "a", Direction.Right);

        Assert.True(ReferenceEquals(split, result));
    }

    [Fact]
    public void MovePaneToTarget_OnlyPane_ReturnsUnchangedTree()
    {
        var leaf = new LeafNode { PaneId = "a", SessionId = "s1" };

        var result = LayoutReducer.MovePaneToTarget(leaf, "a", "b", Direction.Right);

        Assert.True(ReferenceEquals(leaf, result));
    }

    #endregion

    #region Test Helpers

    private static SplitNode? FindSplitContainingPane(LayoutNode node, string paneId)
    {
        if (node is SplitNode split)
        {
            if (split.First is LeafNode f && f.PaneId == paneId) return split;
            if (split.Second is LeafNode s && s.PaneId == paneId) return split;

            var result = FindSplitContainingPane(split.First, paneId);
            if (result is not null) return result;
            return FindSplitContainingPane(split.Second, paneId);
        }
        return null;
    }

    #endregion
}
