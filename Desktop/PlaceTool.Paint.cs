using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// Brush mode: painting the objects in and erasing them.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Brush painting: the placements a frame of a drag adds (around the cursor), kept apart from what stands and from
    /// what this stroke already placed. More per second with a stronger brush.
    /// </summary>
    /// <param name="at">The cursor's grid point.</param>
    /// <param name="dt">Seconds since the last step (counted as 0.1 at most).</param>
    /// <param name="already">What this stroke has placed so far.</param>
    public List<Placement> PaintStep(Vector2 at, float dt, List<Placement> already)
    {
        var s = Scene();
        var names = Chosen.ToList();
        var outp = new List<Placement>();
        if (s == null || names.Count == 0)
        {
            return outp;
        }
        var hash = Standing(s, MathF.Max(1, Spacing));
        foreach (var o in already)
        {
            hash.Add(o.G);
        }
        float r = Brush.Radius, want = Density / 100 * MathF.PI * r * r * MathF.Min(dt, 0.1f) * (0.5f + 2.5f * Brush.Strength);
        int n = (int)MathF.Floor(want) + (_rand.NextSingle() < want % 1 ? 1 : 0);
        var mask = Mask();
        for (int tries = 0; n > 0 && tries < n * 6; tries++)
        {
            float a = _rand.NextSingle() * MathF.Tau, d = MathF.Sqrt(_rand.NextSingle()) * r;
            if (PlacementAt(s, at + new Vector2(MathF.Cos(a) * d, MathF.Sin(a) * d), NewDraw(), names, hash, mask) is not { } o)
            {
                continue;
            }
            hash.Add(o.G);
            outp.Add(o);
            n--;
        }
        return outp;
    }

    /// <summary>
    /// Shift + drag: the things of the chosen kinds under the brush (indices into the scene's things), where the mask
    /// allows.
    /// </summary>
    public List<int> EraseAt(Vector2 at)
    {
        var s = Scene();
        var outp = new List<int>();
        if (s == null)
        {
            return outp;
        }
        var mask = Mask();
        float ox = s.OriginX, oz = s.OriginZ;
        var chosen = Chosen.Select(StableHash.Of).ToHashSet();
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                var g = new Vector2(t.Position.X - ox, t.Position.Z - oz);
                if (t.Gone || !chosen.Contains(t.Prefab) || Vector2.Distance(g, at) > Brush.Radius)
                {
                    continue;
                }
                if (mask != null && mask((int)MathF.Round(g.Y) * s.W + (int)MathF.Round(g.X)) <= 0)
                {
                    continue;
                }
                outp.Add(i);
            }
        }
        return outp;
    }
}
