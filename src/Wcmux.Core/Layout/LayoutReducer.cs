namespace Wcmux.Core.Layout;

/// <summary>
/// Pure state transitions for the binary split-tree layout model.
/// All methods are static and return new tree instances -- no mutation,
/// no UI widget references. The reducer enforces minimum pane sizes in
/// terminal cells and deterministic close/focus behavior.
/// </summary>
public static class LayoutReducer
{
    /// <summary>Minimum pane width in terminal columns.</summary>
    public const int MinColumns = 20;

    /// <summary>Minimum pane height in terminal rows.</summary>
    public const int MinRows = 6;

    /// <summary>Minimum ratio to prevent panes from collapsing.</summary>
    public const double MinRatio = 0.1;

    /// <summary>Maximum ratio to prevent panes from collapsing.</summary>
    public const double MaxRatio = 0.9;

    /// <summary>Default ratio step for resize operations (fraction of total).</summary>
    public const double ResizeStep = 0.05;

    /// <summary>
    /// Splits the pane identified by <paramref name="paneId"/> along the given axis.
    /// Returns the new tree root and the newly created leaf node.
    /// </summary>
    public static (LayoutNode NewRoot, LeafNode NewLeaf) SplitPane(
        LayoutNode root,
        string paneId,
        SplitAxis axis,
        string newPaneId,
        string newSessionId,
        PaneKind kind = PaneKind.Terminal)
    {
        var newLeaf = new LeafNode
        {
            PaneId = newPaneId,
            SessionId = newSessionId,
            Kind = kind,
        };

        var newRoot = SplitPaneInTree(root, paneId, axis, newLeaf);
        return (newRoot, newLeaf);
    }

    /// <summary>
    /// Closes the pane identified by <paramref name="paneId"/> and collapses
    /// the parent split node. Returns null if the closed pane was the last one.
    /// </summary>
    public static LayoutNode? ClosePane(LayoutNode root, string paneId)
    {
        return ClosePaneInTree(root, paneId);
    }

    /// <summary>
    /// Finds the best pane to focus when moving in <paramref name="direction"/>
    /// from the pane at <paramref name="fromPaneId"/>, using the computed pane
    /// rectangles for geometric proximity.
    /// </summary>
    public static string? FindDirectionalFocus(
        LayoutNode root,
        string fromPaneId,
        Direction direction,
        IReadOnlyDictionary<string, PaneRect> paneRects)
    {
        if (!paneRects.TryGetValue(fromPaneId, out var fromRect))
            return null;

        var fromLeft = fromRect.X;
        var fromRight = fromRect.X + fromRect.Width;
        var fromTop = fromRect.Y;
        var fromBottom = fromRect.Y + fromRect.Height;
        var fromCenterX = fromRect.X + fromRect.Width / 2;
        var fromCenterY = fromRect.Y + fromRect.Height / 2;

        string? bestPaneId = null;
        double bestDistance = double.MaxValue;

        foreach (var (paneId, rect) in paneRects)
        {
            if (paneId == fromPaneId) continue;

            var left = rect.X;
            var right = rect.X + rect.Width;
            var top = rect.Y;
            var bottom = rect.Y + rect.Height;
            var centerX = rect.X + rect.Width / 2;
            var centerY = rect.Y + rect.Height / 2;

            // Edge-based direction check: the candidate's near edge must be
            // at or beyond the source pane's far edge in that direction.
            // A small tolerance handles floating-point rounding at shared
            // split boundaries.
            const double tolerance = 1.0;
            bool isInDirection = direction switch
            {
                Direction.Left => right <= fromLeft + tolerance,
                Direction.Right => left >= fromRight - tolerance,
                Direction.Up => bottom <= fromTop + tolerance,
                Direction.Down => top >= fromBottom - tolerance,
                _ => false,
            };

            if (!isInDirection) continue;

            // Edge-to-edge primary distance + perpendicular center offset penalty
            double dist = direction switch
            {
                Direction.Left =>
                    (fromLeft - right) + Math.Abs(centerY - fromCenterY) * 0.5,
                Direction.Right =>
                    (left - fromRight) + Math.Abs(centerY - fromCenterY) * 0.5,
                Direction.Up =>
                    (fromTop - bottom) + Math.Abs(centerX - fromCenterX) * 0.5,
                Direction.Down =>
                    (top - fromBottom) + Math.Abs(centerX - fromCenterX) * 0.5,
                _ => double.MaxValue,
            };

            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestPaneId = paneId;
            }
        }

        return bestPaneId;
    }

    /// <summary>
    /// Resizes the active pane in <paramref name="direction"/> by adjusting
    /// the ratio of the closest ancestor split that matches the resize axis.
    /// Returns the new tree root.
    /// </summary>
    public static LayoutNode ResizePane(
        LayoutNode root,
        string paneId,
        Direction direction,
        double step = ResizeStep)
    {
        return ResizePaneInTree(root, paneId, direction, step);
    }

    /// <summary>
    /// Determines the best pane to receive focus after <paramref name="closedPaneId"/>
    /// is closed. Prefers the sibling of the closed pane, then falls back to
    /// the most recent entry in <paramref name="focusHistory"/>.
    /// </summary>
    public static string? FindFocusAfterClose(
        LayoutNode root,
        string closedPaneId,
        IReadOnlyList<string> focusHistory)
    {
        // Find sibling of closed pane
        var sibling = FindSibling(root, closedPaneId);
        if (sibling is not null)
        {
            // Get the leftmost/topmost leaf of the sibling subtree
            return GetFirstLeaf(sibling)?.PaneId;
        }

        // Fall back to focus history
        var allPanes = new HashSet<string>();
        CollectPaneIds(root, allPanes);
        allPanes.Remove(closedPaneId);

        for (int i = focusHistory.Count - 1; i >= 0; i--)
        {
            if (allPanes.Contains(focusHistory[i]))
                return focusHistory[i];
        }

        // Last resort: any surviving pane
        return allPanes.FirstOrDefault();
    }

    /// <summary>
    /// Computes pixel rectangles for all leaf panes in the tree given
    /// a container size.
    /// </summary>
    public static Dictionary<string, PaneRect> ComputePaneRects(
        LayoutNode root,
        double containerWidth,
        double containerHeight)
    {
        var rects = new Dictionary<string, PaneRect>();
        ComputeRectsRecursive(root, 0, 0, containerWidth, containerHeight, rects);
        return rects;
    }

    /// <summary>
    /// Collects all pane IDs from the tree.
    /// </summary>
    public static List<string> GetAllPaneIds(LayoutNode root)
    {
        var ids = new HashSet<string>();
        CollectPaneIds(root, ids);
        return ids.ToList();
    }

    #region Private helpers

    private static LayoutNode SplitPaneInTree(
        LayoutNode node,
        string paneId,
        SplitAxis axis,
        LeafNode newLeaf)
    {
        if (node is LeafNode leaf && leaf.PaneId == paneId)
        {
            return new SplitNode
            {
                Axis = axis,
                Ratio = 0.5,
                First = leaf,
                Second = newLeaf,
            };
        }

        if (node is SplitNode split)
        {
            var newFirst = SplitPaneInTree(split.First, paneId, axis, newLeaf);
            if (!ReferenceEquals(newFirst, split.First))
            {
                return split with { First = newFirst };
            }

            var newSecond = SplitPaneInTree(split.Second, paneId, axis, newLeaf);
            if (!ReferenceEquals(newSecond, split.Second))
            {
                return split with { Second = newSecond };
            }
        }

        return node;
    }

    private static LayoutNode? ClosePaneInTree(LayoutNode node, string paneId)
    {
        if (node is LeafNode leaf)
        {
            return leaf.PaneId == paneId ? null : node;
        }

        if (node is SplitNode split)
        {
            // Check if either direct child is the target leaf
            if (split.First is LeafNode firstLeaf && firstLeaf.PaneId == paneId)
                return split.Second;

            if (split.Second is LeafNode secondLeaf && secondLeaf.PaneId == paneId)
                return split.First;

            // Recurse into children
            var newFirst = ClosePaneInTree(split.First, paneId);
            if (newFirst is null)
                return split.Second;
            if (!ReferenceEquals(newFirst, split.First))
                return split with { First = newFirst };

            var newSecond = ClosePaneInTree(split.Second, paneId);
            if (newSecond is null)
                return split.First;
            if (!ReferenceEquals(newSecond, split.Second))
                return split with { Second = newSecond };
        }

        return node;
    }

    private static LayoutNode ResizePaneInTree(
        LayoutNode node,
        string paneId,
        Direction direction,
        double step)
    {
        if (node is not SplitNode split)
            return node;

        var targetAxis = direction is Direction.Left or Direction.Right
            ? SplitAxis.Vertical
            : SplitAxis.Horizontal;

        // If this split matches the resize axis and the pane is in a child...
        if (split.Axis == targetAxis)
        {
            bool paneInFirst = ContainsPane(split.First, paneId);
            bool paneInSecond = ContainsPane(split.Second, paneId);

            if (paneInFirst || paneInSecond)
            {
                // Determine ratio adjustment
                double delta = 0;
                if (paneInFirst)
                {
                    delta = direction is Direction.Right or Direction.Down ? step : -step;
                }
                else // paneInSecond
                {
                    delta = direction is Direction.Left or Direction.Up ? step : -step;
                }

                if (delta != 0)
                {
                    var newRatio = Math.Clamp(split.Ratio + delta, MinRatio, MaxRatio);
                    var result = split with { Ratio = newRatio };

                    // Also recurse into the child containing the pane
                    if (paneInFirst)
                        result = result with { First = ResizePaneInTree(result.First, paneId, direction, step) };
                    else
                        result = result with { Second = ResizePaneInTree(result.Second, paneId, direction, step) };

                    return result;
                }
            }
        }

        // Axis doesn't match -- recurse into the child containing the pane
        if (ContainsPane(split.First, paneId))
        {
            var newFirst = ResizePaneInTree(split.First, paneId, direction, step);
            if (!ReferenceEquals(newFirst, split.First))
                return split with { First = newFirst };
        }
        else if (ContainsPane(split.Second, paneId))
        {
            var newSecond = ResizePaneInTree(split.Second, paneId, direction, step);
            if (!ReferenceEquals(newSecond, split.Second))
                return split with { Second = newSecond };
        }

        return node;
    }

    private static bool ContainsPane(LayoutNode node, string paneId)
    {
        if (node is LeafNode leaf)
            return leaf.PaneId == paneId;

        if (node is SplitNode split)
            return ContainsPane(split.First, paneId) || ContainsPane(split.Second, paneId);

        return false;
    }

    private static LayoutNode? FindSibling(LayoutNode node, string paneId)
    {
        if (node is not SplitNode split) return null;

        if (split.First is LeafNode firstLeaf && firstLeaf.PaneId == paneId)
            return split.Second;

        if (split.Second is LeafNode secondLeaf && secondLeaf.PaneId == paneId)
            return split.First;

        var result = FindSibling(split.First, paneId);
        if (result is not null) return result;

        return FindSibling(split.Second, paneId);
    }

    private static LeafNode? GetFirstLeaf(LayoutNode node)
    {
        if (node is LeafNode leaf) return leaf;
        if (node is SplitNode split) return GetFirstLeaf(split.First);
        return null;
    }

    private static void CollectPaneIds(LayoutNode node, HashSet<string> ids)
    {
        if (node is LeafNode leaf)
        {
            ids.Add(leaf.PaneId);
        }
        else if (node is SplitNode split)
        {
            CollectPaneIds(split.First, ids);
            CollectPaneIds(split.Second, ids);
        }
    }

    private static void ComputeRectsRecursive(
        LayoutNode node,
        double x, double y,
        double width, double height,
        Dictionary<string, PaneRect> rects)
    {
        if (node is LeafNode leaf)
        {
            rects[leaf.PaneId] = new PaneRect(x, y, width, height);
            return;
        }

        if (node is SplitNode split)
        {
            if (split.Axis == SplitAxis.Vertical)
            {
                var firstWidth = width * split.Ratio;
                var secondWidth = width - firstWidth;
                ComputeRectsRecursive(split.First, x, y, firstWidth, height, rects);
                ComputeRectsRecursive(split.Second, x + firstWidth, y, secondWidth, height, rects);
            }
            else
            {
                var firstHeight = height * split.Ratio;
                var secondHeight = height - firstHeight;
                ComputeRectsRecursive(split.First, x, y, width, firstHeight, rects);
                ComputeRectsRecursive(split.Second, x, y + firstHeight, width, secondHeight, rects);
            }
        }
    }

    #endregion
}
