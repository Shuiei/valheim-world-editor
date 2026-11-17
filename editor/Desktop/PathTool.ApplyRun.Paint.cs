namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    internal sealed partial class ApplyRun
    {
        /// <summary>Paints one point with the paint action's colour, by its weight.</summary>
        /// <param name="s">The point and what is known about it.</param>
        internal void Paint(in PointSample s) => GroundOps.Paint(_g, s.P, _colour!, s.W);

        /// <summary>
        /// Erode: weathers the points the line reached, each by its weight, in one go (as the Area tool erodes a
        /// selection), and reports the points the erosion changed.
        /// </summary>
        /// <returns>The points changed, the rectangle around them (one point wider each way), and whether the game's limit stopped some.</returns>
        internal (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Erode()
        {
            var set = new HashSet<int>();
            var ((x0, z0, x1, z1), eroded) = Erosion.Region(_g, _weighed, (_x0, _z0, _x1, _z1), _b.RestAngle, _b.Random, set);
            return (set.ToList(), (x0 - 1, z0 - 1, x1 + 1, z1 + 1), eroded);
        }
    }
}
