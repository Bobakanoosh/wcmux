namespace Wcmux.Core.Layout;

/// <summary>
/// The axis along which a split divides two child panes.
/// Horizontal splits stack children top-to-bottom; vertical splits stack left-to-right.
/// </summary>
public enum SplitAxis
{
    /// <summary>Children stacked top-to-bottom.</summary>
    Horizontal,

    /// <summary>Children stacked left-to-right.</summary>
    Vertical,
}

/// <summary>
/// Directional focus/resize movement.
/// </summary>
public enum Direction
{
    Left,
    Right,
    Up,
    Down,
}

/// <summary>
/// Immutable binary split-tree node. A node is either a leaf (terminal pane)
/// or a split containing two children. Layout state is reducer-owned --
/// UI widgets render the tree but never define it.
/// </summary>
public abstract record LayoutNode
{
    /// <summary>Unique identity for this node in the tree.</summary>
    public string NodeId { get; init; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// A leaf node representing a single terminal pane attached to a session.
/// </summary>
public sealed record LeafNode : LayoutNode
{
    /// <summary>Pane identifier (matches a session attachment).</summary>
    public required string PaneId { get; init; }

    /// <summary>Session ID currently attached to this pane.</summary>
    public required string SessionId { get; init; }
}

/// <summary>
/// A split node containing exactly two children divided along an axis.
/// The ratio determines how space is distributed (0.0 to 1.0).
/// </summary>
public sealed record SplitNode : LayoutNode
{
    /// <summary>Split direction.</summary>
    public required SplitAxis Axis { get; init; }

    /// <summary>
    /// Proportion of space given to the first child (0.0 to 1.0).
    /// The second child receives 1 - Ratio.
    /// </summary>
    public required double Ratio { get; init; }

    /// <summary>First (top or left) child.</summary>
    public required LayoutNode First { get; init; }

    /// <summary>Second (bottom or right) child.</summary>
    public required LayoutNode Second { get; init; }
}

/// <summary>
/// Pixel rectangle for a pane, computed from the split tree and container size.
/// </summary>
public readonly record struct PaneRect(double X, double Y, double Width, double Height);
