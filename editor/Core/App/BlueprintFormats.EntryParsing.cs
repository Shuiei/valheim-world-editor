using System.Globalization;
using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// Reading one line of a blueprint: a piece, a terrain entry or a number in it.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// One PlanBuild "#Pieces" line: name;category;x;y;z;qx;qy;qz;qw;info;sx;sy;sz (scale from sx when present).
    /// </summary>
    private static Piece ParseBlueprintPiece(string line)
    {
        // Old PlanBuild files were written with a comma as decimal separator.
        if (line.Contains(',') && !line.Contains('"'))
        {
            line = line.Replace(',', '.');
        }
        string[] p = line.Split(';');
        var q = new Quaternion(F(p[5]), F(p[6]), F(p[7]), F(p[8]));
        float scale = p.Length > 12 ? F(p[10]) : 1f;
        return new Piece(PrefabName(p[0]), new Vector3(Coord(p[2]), Coord(p[3]), Coord(p[4])), ToEuler(q), scale);
    }

    /// <summary>
    /// One .vbuild line: name qx qy qz qw x y z (no scale).
    /// </summary>
    private static Piece ParseVBuild(string line)
    {
        if (line.Contains(','))
        {
            line = line.Replace(',', '.');
        }
        string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var q = new Quaternion(F(p[1]), F(p[2]), F(p[3]), F(p[4]));
        return new Piece(PrefabName(p[0]), new Vector3(Coord(p[5]), Coord(p[6]), Coord(p[7])), ToEuler(q), 1f);
    }

    /// <summary>
    /// One PlanBuild "#Terrain" line: shape;x;y;z;radius;rotation;smooth;paint. A negative radius is a FormatException
    /// (the line cannot be read).
    /// </summary>
    private static TerrainMod ParseTerrain(string line)
    {
        string[] p = line.Split(';');
        float radius = F(p[4]);
        return radius < 0
            ? throw new FormatException($"{p[4].Trim()} is not a radius")
            : new TerrainMod(p[0].ToLowerInvariant(), new Vector3(Coord(p[1]), Coord(p[2]), Coord(p[3])), radius, (int)MathF.Round(F(p[5])), p.Length > 7 ? p[7] : "");
    }

    /// <summary>
    /// How far a position may be from a blueprint's origin: ten times a Valheim world's radius, further than any
    /// piece can be placed, and far short of where floats lose their precision and sums of them overflow.
    /// </summary>
    private const float MaxCoordinate = 100_000f;

    /// <summary>
    /// A position's coordinate (<c>F</c>), within <c>MaxCoordinate</c> of the origin; a FormatException beyond.
    /// </summary>
    private static float Coord(string s)
    {
        float v = F(s);
        return MathF.Abs(v) <= MaxCoordinate ? v : throw new FormatException($"{s.Trim()} is further than any world reaches");
    }

    /// <summary>
    /// "piece_chest(Clone)" -&gt; "piece_chest", as PlanBuild does.
    /// </summary>
    private static string PrefabName(string s) => s.Split('(')[0].Trim();

    /// <summary>
    /// A number in invariant culture; empty gives 0. One that is not finite ("NaN", "Infinity") is a FormatException,
    /// like text that is not a number: the line cannot be read.
    /// </summary>
    private static float F(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return 0f;
        }
        float v = float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        return float.IsFinite(v) ? v : throw new FormatException($"{s.Trim()} is not a finite number");
    }
}
