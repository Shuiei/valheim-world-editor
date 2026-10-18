
namespace TerrainEditor.Desktop;

/// <summary>
/// The gable roof's pieces (ClaudeBuilder.Roof): the slope courses and ridge, the gable ends and their walls.
/// </summary>
public static partial class ClaudeBuilder
{
    /// <summary>The slope pieces on both sides of the ridge, course by course up from the eave, and the ridge pieces.</summary>
    /// <param name="list">The pieces, added to.</param>
    /// <param name="ridge">The roof's frame.</param>
    /// <param name="spots">Where the pieces stand along the ridge (their middles).</param>
    /// <param name="n">How many courses each side.</param>
    /// <param name="top">The height of the ridge.</param>
    /// <param name="rise">The rise of one course.</param>
    /// <param name="highY">The slope's high edge height (its snap point).</param>
    /// <param name="slopeName">The slope piece's name.</param>
    /// <param name="ridgeName">The ridge piece's name.</param>
    internal static void Slopes(List<ClaudeTools.PieceSpec> list, Ridge ridge, List<float> spots, int n, float top, float rise, float highY, string slopeName, string ridgeName)
    {
        float plusSide = ridge.AlongX ? 0 : 90, minusSide = ridge.AlongX ? 180 : 270, ridgeYaw = ridge.AlongX ? 0 : 90;
        foreach (float a in spots)
        {
            float u = a - ridge.Centre;
            for (int k = 1; k <= n; k++)
            {
                float y = top - (k - 1) * rise - highY;
                var (px, pz) = ridge.At(u, 2 * k);
                list.Add(P(slopeName, px, y, pz, plusSide));
                var (mx, mz) = ridge.At(u, -2 * k);
                list.Add(P(slopeName, mx, y, mz, minusSide));
            }
            var (rx, rz) = ridge.At(u, 0);
            list.Add(P(ridgeName, rx, top, rz, ridgeYaw));
        }
    }

    /// <summary>
    /// The two gable ends: columns of 2 m from each wall line toward the ridge, walls up to the roof's underside at each
    /// column's outer edge, then a sloped wall rising with the roof over the column.
    /// </summary>
    /// <param name="list">The pieces, added to.</param>
    /// <param name="ridge">The roof's frame.</param>
    /// <param name="eave">The height where the roof's underside meets the wall lines.</param>
    /// <param name="pitch">The slope's pitch (rise over run).</param>
    /// <param name="gableName">The sloped gable piece's name.</param>
    internal static void Gables(List<ClaudeTools.PieceSpec> list, Ridge ridge, float eave, float pitch, string gableName)
    {
        // The gable piece is checked to be a piece, as the walls and its slopes are.
        Info(gableName);
        var full = Info("woodwall");
        var half = Info("wood_wall_half");
        float wallYaw = ridge.AlongX ? 90 : 0;
        // The sloped wall rises toward its local +x: toward the ridge from either side.
        float plusGable = ridge.AlongX ? 90 : 180, minusGable = ridge.AlongX ? 270 : 0;
        float depth = ridge.Depth;
        foreach (float end in new[] { -ridge.Length / 2, ridge.Length / 2 })
        {
            // An odd number of metres from the wall line to the ridge line: the 2 m under the ridge is
            // wall up to the ridge's foot.
            int columns = (int)MathF.Floor(depth / 2 + 0.005f);
            if (MathF.Abs(depth - 2 * columns - 1) < 0.05f)
            {
                var (sx, sz) = ridge.At(end, 0);
                FillWall(list, sx, sz, eave, eave + pitch * (depth - 1), wallYaw, full.MinY, half.MinY);
            }
            for (int j = 0; depth - 2 * j - 2 >= -0.01f; j++)
            {
                float baseY = eave + pitch * 2 * j;
                foreach (int sign in new[] { 1, -1 })
                {
                    float v = sign * (depth - 2 * j - 1);
                    var (gx, gz) = ridge.At(end, v);
                    FillWall(list, gx, gz, eave, baseY, wallYaw, full.MinY, half.MinY);
                    list.Add(P(gableName, gx, baseY, gz, sign > 0 ? plusGable : minusGable));
                }
            }
        }
    }

    /// <summary>Walls from y0 up to y1 at a point: full walls, then a half wall when needed.</summary>
    /// <param name="list">The pieces, added to.</param>
    /// <param name="x">The point, x.</param>
    /// <param name="z">The point, z.</param>
    /// <param name="y0">The height the walls start at.</param>
    /// <param name="y1">The height they reach.</param>
    /// <param name="yaw">The walls' turn, in degrees.</param>
    /// <param name="fullMinY">The full wall's lowest point (its bottom offset).</param>
    /// <param name="halfMinY">The half wall's lowest point (its bottom offset).</param>
    internal static void FillWall(List<ClaudeTools.PieceSpec> list, float x, float z, float y0, float y1, float yaw, float fullMinY, float halfMinY)
    {
        while (y1 - y0 >= 2 - 0.01f)
        {
            list.Add(P("woodwall", x, y0 - fullMinY, z, yaw));
            y0 += 2;
        }
        if (y1 - y0 >= 1 - 0.01f)
        {
            list.Add(P("wood_wall_half", x, y0 - halfMinY, z, yaw));
        }
    }
}
