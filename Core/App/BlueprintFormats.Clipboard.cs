using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Assembling the editor's clipboard (ground and objects) from a parsed blueprint.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// The editor's clipboard (as stored: heights in cm, -32768 = no ground; weights and paint in 1/255)
    /// from a parsed file. The lowest piece rests on the point where it is pasted; ground from terrain
    /// entries is levelled to their height, relative to the same point. With terrain entries, that point is
    /// the middle of the ground they reach (the grid is centred on it and as large as they reach); without,
    /// the middle of the pieces.
    /// </summary>
    /// <param name="bp">The parsed file.</param>
    /// <param name="known">Whether a prefab name can be placed; pieces it rejects are left out.</param>
    /// <param name="unknown">The rejected prefab names, distinct and sorted.</param>
    /// <exception cref="InvalidDataException">The terrain entries span more than <c>MaxGround</c> metres, or one has
    /// a negative radius (<c>Parse</c> leaves such a line out).</exception>
    public static JsonObject ToClip(Parsed bp, Func<string, bool> known, out List<string> unknown)
    {
        var pieces = bp.Pieces.Where(p => known(p.Name)).ToList();
        unknown = [.. bp.Pieces.Select(p => p.Name).Where(n => !known(n)).Distinct().OrderBy(n => n)];
        float minY = pieces.Count > 0 ? pieces.Min(p => p.Position.Y) : bp.Terrain.Count > 0 ? bp.Terrain.Min(t => t.Position.Y) : 0f;
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        void Grow(float x, float z) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); z0 = Math.Min(z0, z); z1 = Math.Max(z1, z); }
        foreach (Piece p in pieces)
        {
            Grow(p.Position.X, p.Position.Z);
        }

        // The terrain entries' own extents: the ground grid is centred on them, and the pieces placed from there.
        float tx0 = float.MaxValue, tx1 = float.MinValue, tz0 = float.MaxValue, tz1 = float.MinValue;
        foreach (TerrainMod t in bp.Terrain)
        {
            if (t.Radius < 0)
            {
                throw new InvalidDataException("a terrain mark has a negative radius");
            }
            float r = Reach(t);
            tx0 = Math.Min(tx0, t.Position.X - r); tx1 = Math.Max(tx1, t.Position.X + r);
            tz0 = Math.Min(tz0, t.Position.Z - r); tz1 = Math.Max(tz1, t.Position.Z + r);
            Grow(t.Position.X - r, t.Position.Z - r); Grow(t.Position.X + r, t.Position.Z + r);
        }
        if (x0 > x1)
        {
            x0 = x1 = z0 = z1 = 0f;
        }
        // Midpoints as halves: a huge radius makes an extent too large to sum, and is refused below.
        float cx = bp.Terrain.Count > 0 ? tx0 / 2f + tx1 / 2f : x0 / 2f + x1 / 2f, cz = bp.Terrain.Count > 0 ? tz0 / 2f + tz1 / 2f : z0 / 2f + z1 / 2f;
        JsonArray objects = PieceObjects(pieces, cx, cz, minY);
        int w = 1, h = 1;
        int[] rel = [-32768], wt = [0], pnt = [-255, -255, -255, -255];
        if (bp.Terrain.Count > 0)
        {
            (w, h, rel, wt, pnt) = TerrainGrid(bp, cx, cz, minY, tx1 - tx0, tz1 - tz0);
        }
        const float Pad = 1f;
        JsonArray poly = new(
            Point(x0 - cx - Pad, z0 - cz - Pad), Point(x1 - cx + Pad, z0 - cz - Pad),
            Point(x1 - cx + Pad, z1 - cz + Pad), Point(x0 - cx - Pad, z1 - cz + Pad));
        return new JsonObject
        {
            ["w"] = w,
            ["h"] = h,
            ["rel"] = new JsonArray(rel.Select(v => (JsonNode)v).ToArray()),
            ["wt"] = new JsonArray(wt.Select(v => (JsonNode)v).ToArray()),
            ["pnt"] = new JsonArray(pnt.Select(v => (JsonNode)v).ToArray()),
            ["objects"] = objects,
            ["poly"] = poly,
            ["name"] = bp.Name,
        };
        static JsonObject Point(float gx, float gz) => new() { ["gx"] = R(gx), ["gz"] = R(gz) };
    }

    /// <summary>
    /// The ground grid of a clip from the terrain entries: as large as they reach (spanW by spanH metres) and centred on
    /// (cx, cz). Only the ground the entries reach is gridded, however far the pieces go. A whole span starts the grid on
    /// the marks' first edge, so marks a whole number of metres apart fall on its points. Later entries are applied over
    /// earlier ones, as in game.
    /// </summary>
    /// <returns>The grid's width and height in points, the heights in cm (-32768: none), the weights and the paint (1/255 each).</returns>
    /// <exception cref="InvalidDataException">The entries span more than <c>MaxGround</c> metres.</exception>
    private static (int W, int H, int[] Rel, int[] Wt, int[] Pnt) TerrainGrid(Parsed bp, float cx, float cz, float minY, float spanW, float spanH)
    {
        if (spanW > MaxGround || spanH > MaxGround)
        {
            throw new InvalidDataException($"its ground marks span {spanW:F0} m by {spanH:F0} m, more than the editor's {MaxGround} m");
        }
        int w = (int)MathF.Ceiling(spanW) + 1;
        int h = (int)MathF.Ceiling(spanH) + 1;
        int[] rel = [.. Enumerable.Repeat(-32768, w * h)];
        int[] wt = new int[w * h];
        int[] pnt = [.. Enumerable.Repeat(-255, w * h * 4)];
        for (int iz = 0; iz < h; iz++)
        {
            for (int ix = 0; ix < w; ix++)
            {
                float x = cx + ix - (w - 1) / 2f, z = cz + iz - (h - 1) / 2f;
                // Later entries are applied over earlier ones, as in game.
                TerrainMod? hit = bp.Terrain.LastOrDefault(t => Covers(t, x, z));
                if (hit == null)
                {
                    continue;
                }
                int i = iz * w + ix;
                rel[i] = (int)MathF.Round((hit.Position.Y - minY) * 100f);
                wt[i] = 255;
                float[]? paint = PaintOf(hit.Paint);
                if (paint != null)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        pnt[i * 4 + c] = (int)MathF.Round(paint[c] * 255f);
                    }
                }
            }
        }
        return (w, h, rel, wt, pnt);
    }
}
