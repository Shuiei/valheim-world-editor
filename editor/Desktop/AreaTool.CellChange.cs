namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>
    /// What an action does to one grid point of the selection: the action in one go, by the point's weight. The action
    /// itself is the brushes' and the Path tool's (<see cref="GroundOps"/>).
    /// </summary>
    /// <param name="G">The ground to change.</param>
    /// <param name="B">The brush: Naturalize's bumps, bump size and pattern.</param>
    /// <param name="Act">The action.</param>
    /// <param name="Height">Flatten's height, in metres.</param>
    /// <param name="Amount">Raise and Lower's amount, in metres.</param>
    /// <param name="Paint">The colour (four weights, as the paint brushes put down).</param>
    /// <param name="Before">The heights before the action (Smooth and Naturalize read the ground round a point).</param>
    internal sealed record CellChange(Ground G, Brush B, GroundAction Act, float Height, float Amount, float[] Paint, HeightsBefore Before)
    {
        /// <summary>Changes one grid point by its weight, as the action does.</summary>
        /// <param name="p">The grid point (index z * width + x).</param>
        /// <param name="w">The point's weight, 0 to 1 (the soft edge).</param>
        /// <returns>Whether the point reached the game's ±8 m limit.</returns>
        public bool Change(int p, float w)
        {
            switch (Act)
            {
                case GroundAction.Flatten:
                    return GroundOps.Toward(G, p, Height, w);
                case GroundAction.Raise:
                    return GroundOps.Move(G, p, Amount * w);
                case GroundAction.Lower:
                    return GroundOps.Move(G, p, -(Amount * w));
                case GroundAction.Smooth:
                    return GroundOps.Smooth(G, Before, p, w);
                case GroundAction.Natural:
                    return GroundOps.Naturalize(G, B, Before, p, w);
                case GroundAction.Restore:
                    GroundOps.Restore(G, p, w, w);
                    return false;
                case GroundAction.Paint:
                    GroundOps.Paint(G, p, Paint, w);
                    return false;
                default:
                    return false;
            }
        }
    }

    /// <summary>How far round its points an action reads the ground as it was before, in grid points; below zero: it reads none.</summary>
    /// <param name="act">The action.</param>
    /// <param name="b">The brush: Naturalize's bump size.</param>
    /// <returns>The reach.</returns>
    internal static int ReachOf(GroundAction act, Brush b) =>
        act == GroundAction.Smooth ? GroundOps.SmoothReach : act == GroundAction.Natural ? GroundOps.NaturalReach(b) : -1;
}
