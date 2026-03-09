using Wcmux.Core.Layout;

namespace Wcmux.Tests.Layout;

/// <summary>
/// Tests for PaneKind enum and its integration with LeafNode.
/// </summary>
public class PaneKindTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void LeafNode_DefaultKind_IsTerminal()
    {
        var leaf = new LeafNode { PaneId = "p1", SessionId = "s1" };
        Assert.Equal(PaneKind.Terminal, leaf.Kind);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LeafNode_BrowserKind_PreservedThroughSplit()
    {
        var browserLeaf = new LeafNode
        {
            PaneId = "p1",
            SessionId = "s1",
            Kind = PaneKind.Browser,
        };

        // Split the browser pane -- the original leaf should keep Kind=Browser
        var (newRoot, newLeaf) = LayoutReducer.SplitPane(
            browserLeaf, "p1", SplitAxis.Vertical, "p2", "s2");

        var split = Assert.IsType<SplitNode>(newRoot);
        var first = Assert.IsType<LeafNode>(split.First);
        Assert.Equal(PaneKind.Browser, first.Kind);

        // New leaf should default to Terminal
        Assert.Equal(PaneKind.Terminal, newLeaf.Kind);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ClosePane_BrowserKind_ReturnsCorrectSessionId()
    {
        var browserLeaf = new LeafNode
        {
            PaneId = "p1",
            SessionId = "s1",
            Kind = PaneKind.Browser,
        };
        var terminalLeaf = new LeafNode
        {
            PaneId = "p2",
            SessionId = "s2",
        };

        var root = new SplitNode
        {
            Axis = SplitAxis.Vertical,
            Ratio = 0.5,
            First = browserLeaf,
            Second = terminalLeaf,
        };

        // Close the browser pane -- should collapse to terminal leaf
        var result = LayoutReducer.ClosePane(root, "p1");
        var remaining = Assert.IsType<LeafNode>(result);
        Assert.Equal("p2", remaining.PaneId);
        Assert.Equal("s2", remaining.SessionId);
    }
}
