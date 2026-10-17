using System.Numerics;
using TerrainEditor.Save;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>
    /// Fills a piece of wall (t0..t1 along the line, y0..y1) with the material's pieces, largest first.
    /// Where no piece is low enough for what is left at the top, one rises past it, into the roof.
    /// </summary>
    public static void Fill(Material mat, DungeonOut o, bool alongZ, float line, float t0, float t1, float y0, float y1)
    {
        const float Eps = 0.01f;
        if (t1 - t0 < 0.9f || y1 - y0 < 0.4f)
        {
            return;
        }
        var fits = mat.Walls.Where(p => p.W <= t1 - t0 + Eps).ToList();
        if (fits.Count == 0)
        {
            return;
        }
        var low = fits.Where(p => p.H <= y1 - y0 + Eps).ToList();
        var part = low.Count > 0 ? low.OrderByDescending(p => p.H).ThenByDescending(p => p.W).First() : fits.OrderBy(p => p.H).ThenByDescending(p => p.W).First();
        float tc = t0 + part.W / 2, y = y0 + part.Bottom;
        // A window now and then in a material that has one, on the upper row (never the lowest), by the spot's hash.
        string prefab = mat.Window is { } window && part.W >= 4 && y0 >= 1.9f && (StableHash.Of(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{line:0.#}|{tc:0.#}|{y0:0.#}")) & 0xFF) < 46 ? window : part.Prefab;
        o.Add(prefab, alongZ ? new Vector3(line, y, tc) : new Vector3(tc, y, line), alongZ ? 90 : 0);
        Fill(mat, o, alongZ, line, t0 + part.W, t1, y0, y0 + Math.Min(part.H, y1 - y0));
        Fill(mat, o, alongZ, line, t0, t1, y0 + part.H, y1);
    }
}
