using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The gable roof (ClaudeBuilder.Roof): its names, its slopes and ridge, and its gables.
/// </summary>
public static partial class ClaudeBuilder
{
    /// <summary>
    /// A gable roof over the rectangle (its wall lines), the ridge along the longer side: slopes of
    /// 26 or 45 degrees whose underside meets the wall lines at eave, a ridge, and (gables) the two ends
    /// closed with sloped walls over full and half walls.
    /// </summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    /// <param name="eave">The height where the roof's underside meets the wall lines, in metres.</param>
    /// <param name="angle">26 or 45; anything else throws <see cref="ArgumentException"/>.</param>
    /// <param name="material">Thatch or shingle (any case); anything else throws.</param>
    /// <param name="gables">Also close the two ends.</param>
    /// <exception cref="ArgumentException">Also when it would be more than <see cref="MaxPieces"/> pieces.</exception>
    public static List<ClaudeTools.PieceSpec> Roof(float x0, float z0, float x1, float z1, float eave, int angle, string material, bool gables)
    {
        (float lx, float hx, float lz, float hz) = (MathF.Min(x0, x1), MathF.Max(x0, x1), MathF.Min(z0, z1), MathF.Max(z0, z1));
        var (slopeName, ridgeName, gableName) = RoofNames(material, angle);
        var (rise, run, pitch, highY) = SlopeOf(Info(slopeName), slopeName);
        bool alongX = hx - lx >= hz - lz;
        var ridge = new Ridge(alongX, (lx + hx) / 2, (lz + hz) / 2, alongX ? hx - lx : hz - lz, (alongX ? hz - lz : hx - lx) / 2);
        // Rows each side: from the ridge's edge (1 m out) to at or past the wall line.
        float rows = MathF.Max(1, MathF.Ceiling((ridge.Depth - 1) / run - 0.01f));
        // The slopes and ridge first (the gables are fewer: the ridge runs along the longer side), all of it at the end.
        Limit((double)Count(alongX ? lx : lz, alongX ? hx : hz) * (2 * rows + 1));
        int n = (int)rows;
        float overhang = 1 + n * run - ridge.Depth;
        float eaveEdge = eave - pitch * overhang, top = eaveEdge + n * rise;
        var list = new List<ClaudeTools.PieceSpec>();
        Slopes(list, ridge, Across(alongX ? lx : lz, alongX ? hx : hz), n, top, rise, highY, slopeName, ridgeName);
        if (gables)
        {
            Gables(list, ridge, eave, pitch, gableName);
        }
        Limit(list.Count);
        return list;
    }

    /// <summary>The names of a roof's slope, ridge and gable pieces, by material and angle.</summary>
    /// <param name="material">Thatch or shingle (any case); anything else throws.</param>
    /// <param name="angle">26 or 45; anything else throws.</param>
    /// <returns>The slope, the ridge and the gable piece names.</returns>
    internal static (string Slope, string Ridge, string Gable) RoofNames(string material, int angle)
    {
        string kind = material.ToLowerInvariant() switch
        {
            "thatch" => "wood_roof",
            "shingle" => "darkwood_roof",
            _ => throw new ArgumentException("A roof is thatch or shingle."),
        };
        string suffix = angle switch { 26 => "", 45 => "_45", _ => throw new ArgumentException("A roof is 26 or 45 degrees.") };
        return (kind + suffix, kind + "_top" + suffix, "wood_wall_roof" + suffix);
    }

    /// <summary>
    /// A slope piece's rise and run per course (its snap points: low toward its +z, high toward its -z), the pitch
    /// they make, and the height of its high edge; throws <see cref="ArgumentException"/> when the game data gives it no
    /// such edges.
    /// </summary>
    /// <param name="slope">The slope piece.</param>
    /// <param name="slopeName">Its name, for the message.</param>
    internal static (float Rise, float Run, float Pitch, float HighY) SlopeOf(PieceCatalog.Info slope, string slopeName)
    {
        // The slope's edges: low toward its +z, high toward its -z (snap points).
        if (slope.Snaps.Length < 2)
        {
            throw new ArgumentException($"No roof laid: {slopeName} has no snap points to lay it by in the game data read.");
        }
        float[] low = slope.Snaps.OrderByDescending(s => s[2]).First();
        float[] high = slope.Snaps.OrderBy(s => s[2]).First();
        float rise = high[1] - low[1], run = low[2] - high[2], pitch = rise / run;
        if (!(run > 0.01f))
        {
            throw new ArgumentException($"No roof laid: {slopeName}'s snap points do not slope down one way in the game data read.");
        }
        return (rise, run, pitch, high[1]);
    }

    /// <summary>
    /// The roof's frame: the ridge along one axis (x when <paramref name="AlongX"/>), its middle, its length along the
    /// ridge and its depth from the ridge to a wall line.
    /// </summary>
    /// <param name="AlongX">Whether the ridge runs along x.</param>
    /// <param name="Cx">The middle, x.</param>
    /// <param name="Cz">The middle, z.</param>
    /// <param name="Length">The length along the ridge.</param>
    /// <param name="Depth">Half the width across the ridge (the ridge to a wall line).</param>
    internal sealed record Ridge(bool AlongX, float Cx, float Cz, float Length, float Depth)
    {
        /// <summary>The middle along the ridge (x or z, as the ridge runs).</summary>
        public float Centre => AlongX ? Cx : Cz;

        /// <summary>World point from (along the ridge, across it).</summary>
        /// <param name="u">Along the ridge, from the middle.</param>
        /// <param name="v">Across the ridge, from the middle.</param>
        public (float X, float Z) At(float u, float v) => AlongX ? (Cx + u, Cz + v) : (Cx + v, Cz + u);
    }
}
