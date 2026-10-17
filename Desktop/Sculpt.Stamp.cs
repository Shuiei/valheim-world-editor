
namespace TerrainEditor.Desktop;

// Stamp once: the picture pressed into the ground in one go.
public static partial class Sculpt
{
    /// <summary>
    /// Stamp once: the ground under the stamp moves by <c>amount</c> metres times the picture, all at once. Returns the
    /// points changed, the rectangle around them, and whether the game's limit stopped some.
    /// </summary>
    public static (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) StampOnce(Ground g, Brush b, float cx, float cz, float amount, Func<int, float>? mask)
    {
        var touched = new List<int>();
        bool clamped = false;
        float reach = b.Reach, ox = g.OriginX, oz = g.OriginZ;
        int x0 = Math.Max(1, (int)MathF.Floor(cx - reach)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + reach));
        int z0 = Math.Max(1, (int)MathF.Floor(cz - reach)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + reach));
        foreach (var (gx, gz) in Unlocked(g, x0, z0, x1, z1))
        {
            int p = gz * g.W + gx;
            float w = b.Weight(gx - cx, gz - cz, ox + gx, oz + gz) * (mask?.Invoke(p) ?? 1);
            if (w <= 0)
            {
                continue;
            }
            clamped |= g.SetHeight(p, g.HeightOf(p) + amount * w);
            touched.Add(p);
        }
        return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1), clamped);
    }
}
