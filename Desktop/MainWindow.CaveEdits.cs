using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The Cave action: the trench, the trees and rocks in it taken away and the roof's boulders, in one undo step.
    /// </summary>
    /// <param name="s">The session the cave goes into, one undo step.</param>
    /// <param name="path">The line, its width, soft edge, depth, headroom, rock and seed.</param>
    /// <param name="brush">The brush whose noise roughens a natural line.</param>
    /// <param name="mask">Which ground may change (null: all of it).</param>
    /// <param name="label">The history's name for the step; null: "Cave along" and its length.</param>
    /// <returns>What was done, for the status line (in the language in use) and Claude (in English).</returns>
    internal static Said ApplyCave(EditSession s, PathTool path, Brush brush, Func<int, float>? mask, string? label = null)
    {
        var scene = s.Scene;
        var cave = path.PlanCave(s.Ground);
        float ox = scene.OriginX, oz = scene.OriginZ, water = scene.Water;
        PathTool.CaveRock RockAt(float x, float z)
        {
            if (path.Rock != PathTool.CaveRock.Auto)
            {
                return path.Rock;
            }
            var biome = scene.Terrain?.BiomeAt(x, z);
            return biome switch
            {
                ValheimGen.Heightmap.Biome.Plains => PathTool.CaveRock.Heath,
                ValheimGen.Heightmap.Biome.Mountain or ValheimGen.Heightmap.Biome.DeepNorth => PathTool.CaveRock.Mountain,
                ValheimGen.Heightmap.Biome.Ocean => PathTool.CaveRock.Coast,
                _ => PathTool.CaveRock.Forest,
            };
        }
        var built = CaveRoof.Build(path, cave, s.Ground.W, s.Ground.H, ox, oz, RockAt);
        var roof = built.Rocks
            .Select(r => (r.Prefab, Hash: TerrainEditor.Save.StableHash.Of(r.Prefab), r.Position, r.Rotation, r.Scale))
            .Where(r => scene.World?.CanCreate(r.Hash) != false)
            .Select(r => (new NewObject(0, r.Hash, r.Position, r.Rotation, r.Scale), false)).ToList();
        // Natural objects in the trench (deeper than half a metre there) would float or stand in the way.
        var inTrench = PathTool.NearDeep(cave, path.Width / 2 + path.Soft);
        var remove = new List<int>();
        lock (scene.Things)
        {
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (!t.Gone && EditSession.Natural(t) && inTrench(new System.Numerics.Vector2(t.Position.X - ox, t.Position.Z - oz)))
                {
                    remove.Add(i);
                }
            }
        }
        bool clamped = false;
        int open = 0;
        s.Commit(label ?? Lang.T("cave.undo", PathTool.Length(cave.Curve)), g =>
        {
            var (t, rect, c) = path.Apply(g, brush, water, mask, cave);
            clamped = c;
            // The walls up into the rock where its underside is above them (no daylight at the sides).
            foreach (var (p, top) in built.Seal)
            {
                if (!g.Locked(p % g.W, p / g.W) && g.HeightOf(p) < top)
                {
                    g.SetHeight(p, top);
                    t.Add(p);
                    if (g.HeightOf(p) < top - 0.5f)
                    {
                        open++;
                    }
                }
            }
            return (t, (rect.X0 - 4, rect.Z0 - 4, rect.X1 + 4, rect.Z1 + 4));
        }, remove, roof);
        return CaveOutcome(roof.Count, built.Uncovered, remove.Count, PathTool.Length(cave.Curve), open, clamped);
    }
}
