using ValheimGen;

namespace TerrainEditor.App;

// The alt biomes from the alt biome files' entries (expand_altbiomes.yaml): what the layout needs of them.
public static partial class ExpandWorld
{
    /// <summary>
    /// The alt biomes from the alt biome files' entries, as the mod's FromData makes them: every entry, in file order,
    /// with the fields the layout reads (where it may be given, how often, what it keeps out); a field left out is 0
    /// or none, not the game's default (no most areas or chance: never given). They replace the game's alt biome
    /// list whole, as the mod's do; the game's alt biomes' own vegetation and kinds still grow in an alt biome of the
    /// same name.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="table">The world's biomes (for the names).</param>
    /// <param name="notes">Where a value not understood is noted.</param>
    internal static List<SeedLocations.AltBiomeRule> AltBiomesOf(IReadOnlyList<Entry> entries, BiomeTable table, List<string> notes)
    {
        List<SeedLocations.AltBiomeRule> alts = [];
        foreach (var entry in entries)
        {
            var f = new Fields(entry, "altbiomes", notes);
            int Biomes(string key, string fallback) => (int)table.Parse(f.Str(key, fallback), name => notes.Add($"expand_altbiomes: {name} is no biome (left out)"));
            alts.Add(new SeedLocations.AltBiomeRule
            {
                Name = f.Str("name"),
                Enabled = f.B("enabled", true),
                Biome = Biomes("biome", ""),
                MinDistanceFromCenter = f.F("minDistanceFromCenter", 0f),
                MinAmountSpawned = Int(f.Str("minAmountSpawned")) ?? 0,
                MaxAmountSpawned = Int(f.Str("maxAmountSpawned")) ?? 0,
                Chance = f.F("chance", 0f),
                RequireNeighbor = Biomes("requireNeighbor", "None"),
                NotNeighbor = Biomes("notNeighbor", "None"),
                IncompatibleAltBiomes = [.. f.Rows("incompatibleAltBiomes")],
                MinEdgeSize = Int(f.Str("minEdgeSize")) ?? 0,
                MaxEdgeSize = Int(f.Str("maxEdgeSize")) ?? 0,
                MinAvgHeight = f.F("minAvgHeight", 0f),
                MaxAvgHeight = f.F("maxAvgHeight", 0f),
                BelowWorldX = f.F("belowWorldX", 0f),
                AboveWorldX = f.F("aboveWorldX", 0f),
                BelowWorldY = f.F("belowWorldY", 0f),
                AboveWorldY = f.F("aboveWorldY", 0f),
                BlockVegetationNames = [.. f.Rows("blockVegetationNames")],
                BlockLocationNames = [.. f.Rows("blockLocationNames")],
            });
        }
        return alts;
    }
}
