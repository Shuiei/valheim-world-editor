using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>
/// The roof's layout (CaveRoof.Build): boulders placed along the cave's line until its floor and wall feet are covered.
/// </summary>
public static partial class CaveRoof
{
    /// <summary>The roof over a cave.</summary>
    /// <param name="path">The Path tool: its seed, width, soft edge and headroom.</param>
    /// <param name="c">The cave's plan: its line, floor and depth along it.</param>
    /// <param name="w">The grid's width.</param>
    /// <param name="h">The grid's height.</param>
    /// <param name="ox">The grid's world corner, x.</param>
    /// <param name="oz">The grid's world corner, z.</param>
    /// <param name="rockAt">The boulders for a world point.</param>
    public static Result Build(PathTool path, PathTool.CavePlan c, int w, int h, float ox, float oz, Func<float, float, PathTool.CaveRock> rockAt)
    {
        var rnd = new Random(path.Seed);
        int n = c.Curve.Count;
        var rocks = new List<Rock>();
        var seal = new Dictionary<int, float>();
        if (n < 2)
        {
            return new Result(rocks, seal, 0);
        }
        float half = path.Width / 2, cover = half + path.Soft * 0.6f, reach = half + path.Soft + 3;
        var near = NearField(path, c, w, h, reach);
        // What must be under rock: the floor and the foot of the walls along the roofed stretch.
        var must = near.Where(kv => kv.Value.Roofed && kv.Value.Dist <= cover).Select(kv => kv.Key).ToHashSet();
        var covered = new HashSet<int>();
        var underside = new Dictionary<int, float>();
        void Place(float px, float pz)
        {
            var kind = rockAt(ox + px, oz + pz);
            var (prefab, footprint, _) = PathTool.RoofRocks[kind == PathTool.CaveRock.Auto ? PathTool.CaveRock.Forest : kind];
            float scale = Math.Clamp((path.Width + path.Soft * 2 + 4) / (footprint * 0.8f), 0.25f, 2.5f) * (0.8f + 0.5f * (float)rnd.NextDouble());
            // Upside down: the boulder's flat dome (the part the game shows above ground) is the ceiling,
            // nearly level across the cave; its deep keel stands up out of the ground like an outcrop.
            var rot = new Vector3(180 + ((float)rnd.NextDouble() - 0.5f) * 16, (float)rnd.NextDouble() * 360, ((float)rnd.NextDouble() - 0.5f) * 16);
            var low = Underside(prefab, px, pz, rot, scale, w, h);
            // High enough that its underside clears the ceiling everywhere over the cave's floor.
            float y = float.MinValue;
            foreach (var (p, u) in low)
            {
                if (near.TryGetValue(p, out var q) && q.Dist <= half + 0.5f)
                {
                    y = MathF.Max(y, q.Ceiling - u);
                }
            }
            if (y == float.MinValue)
            {
                return;
            }
            rocks.Add(new Rock(prefab, new Vector3(ox + px, y, oz + pz), rot, scale));
            foreach (var (p, u) in low)
            {
                covered.Add(p);
                underside[p] = underside.TryGetValue(p, out float o) ? MathF.Min(o, u + y) : u + y;
            }
        }
        // Along the roofed stretch, a boulder every so often; then more where the floor is still open.
        float total = c.Along[^1], s = 0;
        while (s <= total)
        {
            int i = Math.Max(1, Array.FindIndex(c.Along, a => a >= s));
            float t = (s - c.Along[i - 1]) / MathF.Max(1e-4f, c.Along[i] - c.Along[i - 1]);
            var p = Vector2.Lerp(c.Curve[i - 1].P, c.Curve[i].P, t);
            if (c.Depth[i - 1] + (c.Depth[i] - c.Depth[i - 1]) * t >= path.Headroom + 1.5f)
            {
                Place(p.X + ((float)rnd.NextDouble() - 0.5f) * half * 0.4f, p.Y + ((float)rnd.NextDouble() - 0.5f) * half * 0.4f);
            }
            s += MathF.Max(2, path.Width * 0.9f);
        }
        var tried = new HashSet<int>();
        for (int tries = 0; tries < 200; tries++)
        {
            // A point no boulder can cover (at the very edge of the grid) is not tried forever.
            int open = must.FirstOrDefault(p => !covered.Contains(p) && !tried.Contains(p), -1);
            if (open < 0)
            {
                break;
            }
            tried.Add(open);
            Place(open % w, open / w);
        }
        // The walls rise into the rock wherever its underside is above them (half a metre into it).
        foreach (var (p, q) in near)
        {
            if (q.Dist > half && underside.TryGetValue(p, out float u))
            {
                seal[p] = u - 0.5f;
            }
        }
        return new Result(rocks, seal, must.Count(p => !covered.Contains(p)));
    }
}
