using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using ValheimGen;

/// <summary>
/// The regrow check: Regrow nature on zones the game generated (the AltDump test plugin's zone file: "Z x z n alt
/// biomes" a line) against what the game put there: how many of the game's trees, rocks... stand where Regrow puts
/// one, for the zones in alt biome areas and the others apart, and per kind an alt biome adds or blocks (or every
/// kind) in each (see Program.cs for the usage).
/// </summary>
internal static class RegrowCheck
{
    /// <summary>Prints the counts; returns 0.</summary>
    /// <param name="args">regrow, the world folder, the zone file, then --no-alts to regrow as if no alt biome existed,
    /// --all-kinds to count every kind.</param>
    /// <param name="world">The world.</param>
    /// <param name="terrain">Its terrain.</param>
    internal static int Run(string[] args, WorldSave world, TerrainService terrain)
    {
        if (GameRules.Use(null) is not { } rules)
        {
            return 1;
        }
        bool noAlts = args.Contains("--no-alts"), allKinds = args.Contains("--all-kinds");
        if (noAlts)
        {
            SeedLocations.Use(new SeedLocations.RuleSet { Locations = rules.Locations });
        }
        var altKinds = rules.AltBiomes.SelectMany(a => a.BlockVegetationNames).Concat(Regrow.GameRules.Where(r => r.altBiome != null).Select(r => r.prefab)).ToHashSet();
        var zones = File.ReadAllLines(args[2]).Select(l => l.Split(' ', 5)).Select(p => (X: int.Parse(p[1]), Z: int.Parse(p[2]), Alt: p.Length > 4 && p[4] != "")).ToList();
        var groups = Count(world, terrain, zones, allKinds ? null : altKinds);
        Console.WriteLine($"{zones.Count} zones ({zones.Count(z => z.Alt)} in alt biome areas){(noAlts ? ", regrown without alt biomes" : "")}:");
        foreach (var (key, c) in groups.OrderBy(kv => kv.Key.StartsWith(' ') ? 1 : 0).ThenByDescending(kv => kv.Value[0]))
        {
            Console.WriteLine($"  {key}: game {c[0]}, matched {c[1]} ({(c[0] == 0 ? 0 : 100.0 * c[1] / c[0]):0.0}%), regrown {c[2]}");
        }
        return 0;
    }

    /// <summary>
    /// Per group (alt biome areas, others) and per kind of <paramref name="kinds"/> (all: null): the game's objects of
    /// the vegetation's kinds in the zones, those a spot of Regrow's matches (the same kind within 5 cm), and Regrow's
    /// spots in the zones.
    /// </summary>
    private static Dictionary<string, int[]> Count(WorldSave world, TerrainService terrain, List<(int X, int Z, bool Alt)> zones, HashSet<string>? kinds)
    {
        var vegetation = Regrow.GameRules.Select(r => r.prefab).ToHashSet();
        var byZone = world.Objects.GroupBy(o => (ZoneOf(o.Position.X), ZoneOf(o.Position.Z))).ToDictionary(g => g.Key, g => g.ToList());
        var edits = new EditStore(world);
        var groups = new Dictionary<string, int[]>();
        void Add(bool alt, string name, int column, int n)
        {
            foreach (string key in Keys(alt, name, kinds))
            {
                (groups.TryGetValue(key, out int[]? c) ? c : groups[key] = new int[3])[column] += n;
            }
        }
        foreach (var (zx, zz, alt) in zones)
        {
            var spots = Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz);
            foreach (var (_, prefab, position, _, _) in byZone.GetValueOrDefault((zx, zz)) ?? [])
            {
                if (PrefabCatalog.NameOf(prefab) is { } name && vegetation.Contains(name))
                {
                    Add(alt, name, 0, 1);
                    Add(alt, name, 1, spots.Any(s => s.Name == name && MathF.Abs(s.X - position.X) < 0.05f && MathF.Abs(s.Z - position.Z) < 0.05f) ? 1 : 0);
                }
            }
            foreach (var s in spots.Where(s => ZoneOf(s.X) == zx && ZoneOf(s.Z) == zz))
            {
                Add(alt, s.Name, 2, 1);
            }
        }
        return groups;
    }

    /// <summary>The zone column (or row) of a coordinate.</summary>
    private static int ZoneOf(float v) => (int)MathF.Floor((v + 32) / 64);

    /// <summary>
    /// The counts an object adds to: its zones' group, and its kind (marked "a" in an alt biome area, "o" elsewhere) when
    /// an alt biome adds or blocks it, or for every kind (<paramref name="kinds"/> null).
    /// </summary>
    private static IEnumerable<string> Keys(bool alt, string name, HashSet<string>? kinds)
    {
        yield return alt ? "zones in alt biome areas" : "other zones";
        if (kinds == null || kinds.Contains(name))
        {
            yield return (alt ? "  a " : "  o ") + name;
        }
    }
}
