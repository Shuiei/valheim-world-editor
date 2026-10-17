using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // ---- Drawing them into the cells.
    /// <summary>
    /// Points every eighth of the narrowest width along each line, waved sideways, each a random width;
    /// added to the cells after those drawn before (the order the ground sums them in).
    /// </summary>
    private void Draw(List<River> rivers, Streams which)
    {
        var cells = new Dictionary<Vector2i, List<RiverPoint>>();
        foreach (var river in rivers)
        {
            if (which != Streams.All && IsDeepnorth(river.P0.x, river.P0.y) != (which == Streams.DeepNorthOnly))
            {
                continue;
            }
            float step = (float)((double)river.WidthMin / 8.0);
            var dir = (river.P1 - river.P0).normalized;
            var side = new Vector2(0f - dir.y, dir.x);
            float length = Vector2.Distance(river.P0, river.P1);
            for (float along = 0f; along <= length; along = (float)((double)along + step))
            {
                float u = (float)((double)along / river.CurveWavelength);
                float sway = (float)(Math.Sin(u) * Math.Sin((double)u * (double)0.63412f) * Math.Sin((double)u * (double)0.33412f) * river.CurveWidth);
                float width = Rnd.Range(river.WidthMin, river.WidthMax);
                AddToCells(cells, river.P0 + dir * along + side * sway, width);
            }
        }
        foreach (var (cell, added) in cells)
        {
            _riverCells[cell] = _riverCells.TryGetValue(cell, out var before) ? [.. before, .. added] : [.. added];
        }
    }

    /// <summary>
    /// A point into every cell its circle reaches (rows south to north, cells west to east).
    /// </summary>
    private static void AddToCells(Dictionary<Vector2i, List<RiverPoint>> cells, Vector2 p, float width)
    {
        var home = GetRiverGrid(p.x, p.y);
        int reach = Mathf.CeilToInt((float)((double)width / 64.0));
        for (int y = home.y - reach; y <= home.y + reach; y++)
        {
            for (int x = home.x - reach; x <= home.x + reach; x++)
            {
                var cell = new Vector2i(x, y);
                if (!InsideRiverGrid(cell, p, width))
                {
                    continue;
                }
                if (!cells.TryGetValue(cell, out var list))
                {
                    cells.Add(cell, list = []);
                }
                list.Add(new RiverPoint(p, width));
            }
        }
    }

    /// <summary>
    /// Whether a circle reaches into a cell (the cell and the circle's box overlap).
    /// </summary>
    public static bool InsideRiverGrid(Vector2i cell, Vector2 p, float r)
    {
        var away = p - new Vector2((float)(cell.x * 64.0), (float)(cell.y * 64.0));
        float reach = (float)((double)r + 32.0);
        return Math.Abs(away.x) < reach && Math.Abs(away.y) < reach;
    }

    /// <summary>
    /// The 64 m river cell a point is in (cells centred on multiples of 64 m, as zones).
    /// </summary>
    public static Vector2i GetRiverGrid(float x, float z) =>
        new(Mathf.FloorToInt((float)(((double)x + 32.0) / 64.0)), Mathf.FloorToInt((float)(((double)z + 32.0) / 64.0)));

    /// <summary>
    /// How deep in a river a point is (0 outside, 1 on a point's line) and the rivers' width there (the
    /// widths averaged by depth).
    /// </summary>
    private void RiverAt(float x, float z, out float weight, out float width)
    {
        weight = 0f;
        width = 0f;
        if (!_riverCells.TryGetValue(GetRiverGrid(x, z), out var points))
        {
            return;
        }
        var at = new Vector2(x, z);
        float widths = 0f, depths = 0f;
        foreach (var rp in points)
        {
            float d2 = Vector2.SqrMagnitude(rp.At - at);
            if (!(d2 < rp.WidthSquared))
            {
                continue;
            }
            float depth = (float)(1.0 - (double)(float)Math.Sqrt(d2) / rp.Width);
            if (depth > weight)
            {
                weight = depth;
            }
            widths = (float)((double)widths + (double)rp.Width * depth);
            depths = (float)((double)depths + depth);
        }
        if (depths > 0f)
        {
            width = (float)((double)widths / depths);
        }
    }
}
