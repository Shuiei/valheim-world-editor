using System.Numerics;
using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// A room to dress by hand in a test: its frame, and the material, biome, style and settings it was planned with.
/// </summary>
internal sealed record DressingRoom(DungeonDresser.Frame Frame, DungeonKit.Material Material, DungeonKit.Biome Biome, DungeonKit.Style Style, DungeonGen.Settings Settings);

/// <summary>
/// The dresser's test setup: a wide room with no doorway near its middle, and the pieces placed at a point.
/// </summary>
internal static class DungeonDresserSupport
{
    /// <summary>
    /// A wide room of a Black Forest dungeon in the style named, with no doorway within eight metres of its middle, so
    /// that furniture placed about the middle is not blocked by a doorway's clear space. Taken from the first seed that has one.
    /// </summary>
    public static DressingRoom ClearRoom(string style)
    {
        var biome = DungeonKit.BiomeOf("Black Forest");
        var mat = DungeonKit.MaterialOf(biome.Material);
        var st = DungeonKit.StyleOf(style);
        for (int seed = 1; seed <= 60; seed++)
        {
            var s = new DungeonGen.Settings(Biome: biome.Name, Style: st.Name, Size: 3, Levels: 1, BossKey: false, Seed: seed);
            var plan = DungeonPlanner.Make(s, st, new Random(seed));
            foreach (var sp in plan.Spaces.Where(sp => !sp.Corridor && sp.Shape == DungeonPlan.Shape.Rect))
            {
                var f = new DungeonDresser.Frame(sp, mat, plan.Levels[sp.Level]);
                if (f.HalfA >= 5 && f.HalfD >= 5 && f.Mouths.All(m => Vector2.Distance(new(m.X, m.Z), new(f.Cx, f.Cz)) > 8))
                {
                    return new DressingRoom(f, mat, biome, st, s);
                }
            }
        }
        throw new InvalidOperationException($"No clear room in the {style} style in 60 seeds.");
    }

    /// <summary>Dresses a room as the hall variant named (see <c>DungeonDresser.DressHallAs</c>) and returns the pieces placed.</summary>
    public static DungeonOut DressHall(DressingRoom room, string variant)
    {
        var o = new DungeonOut();
        DungeonDresser.DressHallAs(room.Frame, variant, room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        return o;
    }

    /// <summary>Dresses the middle of a room as the cluster kind named (see <c>DungeonDresser.DressClusterAs</c>) and returns the pieces placed.</summary>
    public static DungeonOut DressCluster(DressingRoom room, string kind)
    {
        var o = new DungeonOut();
        DungeonDresser.DressClusterAs(room.Frame, kind, room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        return o;
    }

    /// <summary>The pieces placed at a world point (x and z within a centimetre of it).</summary>
    public static IEnumerable<DungeonGen.Item> At(DungeonOut o, Vector3 p) =>
        o.Items.Where(i => Vector2.Distance(new(i.Position.X, i.Position.Z), new(p.X, p.Z)) < 0.01f);
}
