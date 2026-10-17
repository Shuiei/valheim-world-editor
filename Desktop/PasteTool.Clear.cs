using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>Clearing the site of a paste (PasteTool.Clear.cs): the ground under the pasted buildings is dug down to them.</summary>
public sealed partial class PasteTool
{
    /// <summary>
    /// Clears the site of a paste: each grid point under (or within a metre of) a pasted buildable piece is dug down to
    /// the lowest such piece's bottom (see <see cref="Digs"/>), never raised. Not where the copy's own ground went in
    /// full (shaped): the building stood on it, its pieces set into it as they were.
    /// </summary>
    /// <param name="g">The ground to dig.</param>
    /// <param name="placed">The pasted objects, with whether each is a player-built piece.</param>
    /// <param name="shaped">The points the copy's ground went in full on, left alone.</param>
    /// <param name="mask">The Mask's weight per grid point (null: none).</param>
    /// <returns>The points dug, and the rectangle they span.</returns>
    private (List<int> Dug, (int X0, int Z0, int X1, int Z1) Rect) Clear(Ground g, List<(NewObject O, bool Piece)> placed, HashSet<int> shaped, Func<int, float>? mask)
    {
        var to = Digs(g, placed);
        var dug = new List<int>();
        int rx0 = g.W, rx1 = 0, rz0 = g.H, rz1 = 0;
        bool limit = g.NoLimit;
        g.NoLimit = true;
        try
        {
            foreach (var ((gx, gz), y) in to)
            {
                Site.Add((gx, gz));
                int p = gz * g.W + gx;
                if (g.Locked(gx, gz) || shaped.Contains(p) || (mask?.Invoke(p) ?? 1) <= 0 || g.HeightOf(p) <= y)
                {
                    continue;
                }
                g.SetHeight(p, y);
                dug.Add(p);
                (rx0, rx1, rz0, rz1) = (Math.Min(rx0, gx), Math.Max(rx1, gx), Math.Min(rz0, gz), Math.Max(rz1, gz));
            }
        }
        finally
        {
            g.NoLimit = limit;
        }
        return (dug, (rx0, rz0, rx1, rz1));
    }

    /// <summary>
    /// The height each grid point under the pasted buildable pieces is dug to: the lowest bottom of the pieces over it,
    /// past the game's ±8 m if need be. A linked group (its parts, and what they hang on) does not stand on the ground,
    /// so none is dug under it; pieces hidden far below (HiddenDepth) are left out.
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="placed">The pasted objects, with whether each is a player-built piece.</param>
    /// <returns>Each grid point (x, z) → the height to dig it to.</returns>
    internal static Dictionary<(int, int), float> Digs(Ground g, List<(NewObject O, bool Piece)> placed)
    {
        float ox = g.OriginX, oz = g.OriginZ;
        var to = new Dictionary<(int, int), float>();
        // A linked group (its parts, and what they hang on) does not stand on the ground: none is dug under it.
        var links = placed.Select(p => ObjectLinks.With(null, p.O.Data)).ToList();
        var held = links.Where(l => l is { Holder: not 0 }).Select(l => l!.Holder).ToHashSet();
        for (int k = 0; k < placed.Count; k++)
        {
            var o = placed[k].O;
            if (links[k] is { } l && (l.Holder != 0 || held.Contains(l.Id)))
            {
                continue;
            }
            if (CopyData.Extent(o.Prefab, o.Position, o.Rotation, o.Scale) is not var (ax, az, bx, bz, bottom)
                || bottom < g.HeightOf(Index(g, new Vector2(o.Position.X - ox, o.Position.Z - oz))) - HiddenDepth)
            {
                continue;
            }
            int x0 = (int)MathF.Floor(ax - ox) - 1, x1 = (int)MathF.Ceiling(bx - ox) + 1;
            int z0 = (int)MathF.Floor(az - oz) - 1, z1 = (int)MathF.Ceiling(bz - oz) + 1;
            for (int gz = Math.Max(1, z0); gz <= Math.Min(g.H - 2, z1); gz++)
            {
                for (int gx = Math.Max(1, x0); gx <= Math.Min(g.W - 2, x1); gx++)
                {
                    to[(gx, gz)] = to.TryGetValue((gx, gz), out float was) ? MathF.Min(was, bottom) : bottom;
                }
            }
        }
        return to;
    }
}
