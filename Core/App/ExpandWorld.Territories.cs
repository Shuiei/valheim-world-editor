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
            var height = new HeightTransform
            {
                Multiplier = f.F("altitudeMultiplier", 1f),
                Delta = f.F("altitudeDelta", 0f),
                DepthMultiplier = f.F("waterDepthMultiplier", 1f),
                Minimum = f.F("minimumAltitude", -1000f),
                Maximum = f.F("maximumAltitude", 10000f),
                ExcessFactor = f.F("excessFactor", 0.5f),
            };
            var territory = new Territory
            {
                Name = name,
                Height = height,
                ForestMultiplier = f.F("forestMultiplier", 1f),
                TerrainColor = f.Colour("colorTerrain"),
                MapColor = f.Colour("colorMap"),
                NoBuild = f.B("noBuild", false),
            };
            bool valid = territory.NoBuild || f.Count("statusEffects") > 0 || !height.IsIdentity || territory.MapColor != null || territory.TerrainColor != null
                || f.Has("colorWaterTop") || f.Has("colorWaterBottom") || f.Has("colorWaterShallow") || f.Has("colorWaterSurface") || territory.ForestMultiplier != 1f;
            if (valid)
            {
                territories[name] = territory;
            }
        }
        return [.. territories.Values];
    }
}
