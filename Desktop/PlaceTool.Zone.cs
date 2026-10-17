using System.Numerics;

namespace TerrainEditor.Desktop;

// The zone mode: random spots inside the zone, kept while the zone is the same.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Random spots inside the zone (Density, Spacing), kept while the zone stays the same (R: new ones).
    /// </summary>
    private List<Vector2> ZoneSpots()
    {
        List<Vector2> poly;
        lock (Points)
        {
            poly = [.. Points];
        }
        float key = poly.Sum(p => p.X * 31 + p.Y * 17) + Density * 1000 + Spacing * 7;
        if (_scatter is { } sc && sc.Key == key)
        {
            return sc.Pts;
        }
        float x0 = poly.Min(p => p.X), x1 = poly.Max(p => p.X), z0 = poly.Min(p => p.Y), z1 = poly.Max(p => p.Y);
        int n = Math.Min(MaxShape, (int)MathF.Round(Density / 100 * AreaTool.Area(poly)));
        var pts = new List<Vector2>();
        var h = new Hash(MathF.Max(0.5f, Spacing));
        for (int tries = 0; pts.Count < n && tries < n * 30; tries++)
        {
            var p = new Vector2(x0 + _rand.NextSingle() * (x1 - x0), z0 + _rand.NextSingle() * (z1 - z0));
            if (!AreaTool.Inside(poly, p.X, p.Y) || !h.Free(p, Spacing))
            {
                continue;
            }
            h.Add(p);
            pts.Add(p);
        }
        _scatter = (key, pts);
        return pts;
    }

    /// <summary>The zone's spots: each one an object where it can go (nothing until the zone has three points).</summary>
    private void ZonePlacements(WorldScene s, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, List<Placement> outp)
    {
        if (Points.Count < 3)
        {
            return;
        }
        var spots = ZoneSpots();
        for (int i = 0; i < spots.Count; i++)
        {
            if (PlacementAt(s, Xf(spots[i]), DrawAt(i), names, hash, mask, Spacing) is { } o)
            {
                outp.Add(o);
            }
        }
    }
}
