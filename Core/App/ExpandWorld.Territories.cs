using ValheimGen;

namespace TerrainEditor.App;

// The territories from the territory files' entries (expand_territories.yaml).
public static partial class ExpandWorld
{
    /// <summary>
    /// The territories from the territory files' entries, as the mod loads them: by name (trimmed, lower-cased; the last
    /// of two the same wins), only those with something set (a height transform, a colour, a forest multiplier, no
    /// building, status effects), an entry without a name passed over.
    /// </summary>
    /// <param name="entries">The entries, in order.</param>
    /// <param name="notes">Where a value not understood is noted.</param>
    internal static List<Territory> TerritoriesOf(IReadOnlyList<Entry> entries, List<string> notes)
    {
        Dictionary<string, Territory> territories = [];
        foreach (var entry in entries)
        {
            var f = new Fields(entry, "territories", notes);
            string name = TerritoryKey(f.Str("territory"));
            if (name == "")
            {
                continue;
            }
            var height = HeightOf(f);
            var water = WaterOf(f);
            var effects = StatusEffectsOf(f);
            var territory = new Territory
            {
                Name = name,
                Height = height,
                ForestMultiplier = f.F("forestMultiplier", 1f),
                TerrainColor = f.Colour("colorTerrain"),
                MapColor = f.Colour("colorMap"),
                Water = water,
                StatusEffects = effects,
                NoBuild = f.B("noBuild", false),
            };
            bool valid = territory.NoBuild || f.Count("statusEffects") > 0 || !height.IsIdentity || territory.MapColor != null || territory.TerrainColor != null
                || !water.IsNone || territory.ForestMultiplier != 1f;
            if (valid)
            {
                territories[name] = territory;
            }
        }
        return [.. territories.Values];
    }

    /// <summary>A biome's or a territory's water colours, those its entry gives.</summary>
    private static WaterColours WaterOf(Fields f) =>
        new(f.Colour("colorWaterTop"), f.Colour("colorWaterBottom"), f.Colour("colorWaterShallow"), f.Colour("colorWaterSurface"));
}
