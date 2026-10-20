using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

/// <summary>
/// The vegetation check: Regrow nature with the first rules of Expand World Data's vegetation file (blueprints, data,
/// drops) on zones the game generated (the AltDump test plugin's zone file), against what the game put there: each
/// object Regrow grows, the same prefab within 5 cm on the ground, then its data (see Program.cs for the usage). The
/// rules come first in the file, so nothing the game placed before them changes their draws.
/// </summary>
internal static class VegetationCheck
{
    /// <summary>The keys compared: the test's own (named vwe_…), the drop table and the scale.</summary>
    private static readonly HashSet<int> Keys = [.. new[] { "vwe_all", "vwe_bush", "vwe_own", "ews_drops", "scale" }.Select(StableHash.Of)];

    /// <summary>Prints the counts and the first differences; returns 0 when every object and value matches.</summary>
    /// <param name="args">vegetation, the world folder, the BepInEx folder, the zone file, how many rules.</param>
    /// <param name="world">The world.</param>
    internal static int Run(string[] args, WorldSave world)
    {
        if (GameRules.Use(null) is null)
        {
            return 1;
        }
        var found = ExpandWorld.Look(args[2], "check");
        var rules = ExpandWorld.Rules(new Dictionary<string, string>(), found, ExpandWorld.ReadData(found), world.StartingGlobalKeys);
        world.Rules = rules with { Vegetation = [.. rules.Vegetation!.Take(int.Parse(args[4], CultureInfo.InvariantCulture))] };
        var terrain = new ValheimGen.TerrainService(world, null);
        var edits = new EditStore(world);
        var areas = Regrow.ClearAreas.Of(world, terrain.Generator);
        var objects = world.Objects.ToList();
        int grown = 0, placed = 0, valued = 0, shown = 0;
        foreach (string line in File.ReadAllLines(args[3]))
        {
            string[] p = line.Split(' ', 5);
            int zx = int.Parse(p[1], CultureInfo.InvariantCulture), zz = int.Parse(p[2], CultureInfo.InvariantCulture);
            foreach (var s in Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz, areas))
            {
                grown++;
                int hash = StableHash.Of(s.Name);
                int match = objects.FindIndex(o => o.Prefab == hash && MathF.Abs(o.Position.X - s.X) < 0.05f && MathF.Abs(o.Position.Z - s.Z) < 0.05f);
                if (match < 0)
                {
                    Say(ref shown, $"no {s.Name} at ({s.X:0.00}, {s.Z:0.00})");
                    continue;
                }
                placed++;
                string mine = ContentsCheck.Values(Built(s, hash), Keys), game = ContentsCheck.Values(ZdoData.Parse(world.ObjectBytes(objects[match].Id)), Keys);
                objects.RemoveAt(match);
                if (mine == game)
                {
                    valued++;
                }
                else
                {
                    Say(ref shown, $"{s.Name} values differ\n    editor: {mine}\n    game:   {game}");
                }
            }
        }
        Console.WriteLine($"{grown} objects grown, {placed} where the game put one, {valued} of those with the game's values.");
        return placed == grown && valued == grown ? 0 : 1;
    }

    /// <summary>A spot's object as New world writes it.</summary>
    private static ZdoData Built(Regrow.Spot s, int hash)
    {
        var z = ZdoData.Parse(ZdoBuilder.Blank(hash, PrefabCatalog.Get(hash)?.Flags ?? 0, new(s.X, s.Y, s.Z), new(s.Rx, s.Ry, s.Rz), s.Scale3));
        foreach (var f in s.Data ?? [])
        {
            z.Set(f.Section, f.Key, f.Value);
        }
        return z;
    }

    /// <summary>Says a difference, the first 25 only.</summary>
    private static void Say(ref int shown, string what)
    {
        if (shown++ < 25)
        {
            Console.WriteLine("  " + what);
        }
    }
}
