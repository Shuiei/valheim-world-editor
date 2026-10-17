using System.Numerics;

namespace TerrainEditor.Desktop;

// The placements along a line (every so many metres) and in a grid of cells.
public sealed partial class PlaceTool
{
    /// <summary>
    /// The objects along the line, every <c>Every</c> metres (and wiggled, turned along it where the kind allows), up to
    /// MaxShape of them.
    /// </summary>
    private void LinePlacements(WorldScene s, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, List<Placement> outp)
    {
        var c = LineRuns().SelectMany(r => r).ToList();
        if (c.Count < 2)
        {
            return;
        }
        float walked = 0, next = 0;
        int i = 0;
        for (int sgi = 1; sgi < c.Count; sgi++)
        {
            Vector2 a = c[sgi - 1], b = c[sgi];
            float len = Vector2.Distance(a, b);
            // 1 mm of slack: a line a whole number of spacings long gets its end piece whatever the
            // rounding of the points picked on the ground.
            while (next <= walked + len + 1e-3f)
            {
                float t = len > 0 ? (next - walked) / len : 0, dx = (b.X - a.X) / (len == 0 ? 1 : len), dz = (b.Y - a.Y) / (len == 0 ? 1 : len);
                var d = DrawAt(i);
                float side = (d.W - 0.5f) * 2 * Wiggle;
                var g = new Vector2(a.X + (b.X - a.X) * t - dz * side, a.Y + (b.Y - a.Y) * t + dx * side);
                // Unity yaw: clockwise from north (+z): the heading of (dx, dz) is atan2(dx, dz). A kind with
                // a known length turns its length along the line; others face along it.
                var e = EndsOf(PickName(names, d.T));
                float? yaw = Along || LineShape != LineShapes.Points ? (MathF.Atan2(dx, dz) - (e?.Heading ?? 0)) * 180 / MathF.PI : null;
                if (PlacementAt(s, g, d, names, hash, mask, 0.3f, yaw) is { } o)
                {
                    outp.Add(o);
                }
                i++;
                next += MathF.Max(0.1f, Every);
                if (i >= MaxShape)
                {
                    return;
                }
            }
            walked += len;
        }
    }

    /// <summary>The objects in a grid of cells over the box between the two grid corners: as many whole cells as fit best, centred.</summary>
    private void GridPlacements(WorldScene s, IReadOnlyList<string> names, Hash hash, Func<int, float>? mask, Vector2 ga, Vector2 gb, List<Placement> outp)
    {
        float x0 = MathF.Min(ga.X, gb.X), x1 = MathF.Max(ga.X, gb.X), z0 = MathF.Min(ga.Y, gb.Y), z1 = MathF.Max(ga.Y, gb.Y);
        // As many whole cells as fit best (at least one each way), centred in the box.
        int nx = Math.Max(1, (int)MathF.Round((x1 - x0) / Cell)), nz = Math.Max(1, (int)MathF.Round((z1 - z0) / Cell));
        float ox = (x0 + x1) / 2 - nx * Cell / 2, oz = (z0 + z1) / 2 - nz * Cell / 2;
        int i = 0;
        for (int iz = 0; iz < nz; iz++)
        {
            for (int ix = 0; ix < nx; ix++)
            {
                if (i >= MaxShape)
                {
                    return;
                }
                if (PlacementAt(s, Xf(new Vector2(ox + (ix + 0.5f) * Cell, oz + (iz + 0.5f) * Cell)), DrawAt(i++), names, hash, mask, 0.3f) is { } o)
                {
                    outp.Add(o);
                }
            }
        }
    }
}
