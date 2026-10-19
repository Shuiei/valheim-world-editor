using ValheimGen;

namespace TerrainEditor.App;

// The alt biomes from the alt biome files' entries (expand_altbiomes.yaml): what the layout needs of them.
public static partial class ExpandWorld
{
    /// <summary>
    /// The alt biomes from the alt biome files' entries, as the layout knows them: each enabled one's name and the
    /// location kinds it keeps out of its areas. They replace the game's alt biome list whole, as the mod's do.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="notes">Where a value not understood is noted.</param>
    internal static List<SeedLocations.AltBiomeRule> AltBiomesOf(IReadOnlyList<Entry> entries, List<string> notes)
    {
        List<SeedLocations.AltBiomeRule> alts = [];
        foreach (var entry in entries)
        {
            var f = new Fields(entry, "altbiomes", notes);
            string name = f.Str("name");
            if (name == "" || !f.B("enabled", true))
            {
                continue;
            }
            alts.Add(new SeedLocations.AltBiomeRule { Name = name, BlockLocationNames = [.. f.Rows("blockLocationNames")] });
        }
        return alts;
    }
}
