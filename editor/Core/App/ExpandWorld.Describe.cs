using ValheimGen;

namespace TerrainEditor.App;

// What a world's rules say, in a few words.
public static partial class ExpandWorld
{
    /// <summary>What the rules say, in a few words, for the log.</summary>
    public static string Describe(WorldRules r)
    {
        if (r.IsVanilla)
        {
            return "the game's own values";
        }
        List<string> parts = [$"radius {r.Radius:0.#} + {r.EdgeSize:0.#} m", $"grid {r.GridWidth} zones", $"water {r.WaterLevel:0.#} m"];
        AddUnless(parts, r.Stretch == 1f && r.BiomeStretch == 1f, $"stretch {r.Stretch:0.##} (biomes {r.BiomeStretch:0.##})");
        AddUnless(parts, r.AltitudeMultiplier == 1f && r.AltitudeDelta == 0f && r.WaterDepthMultiplier == 1f, $"altitude x{r.AltitudeMultiplier:0.##} {r.AltitudeDelta:+0.#;-0.#;+0} m, depth x{r.WaterDepthMultiplier:0.##}");
        AddUnless(parts, r.Rivers && r.Streams, $"rivers {OnOff(r.Rivers)}, streams {OnOff(r.Streams)}");
        AddUnless(parts, !r.BiomeData, $"biome data ({r.Biomes.Biomes.Count} biomes)");
        AddUnless(parts, r.World == null, $"world rules ({r.World?.Count ?? 0}{(r.LegacyGeneration ? ", legacy" : "")})");
        AddUnless(parts, r.Territories.Count == 0, $"territories ({r.Territories.Count})");
        AddUnless(parts, r.Locations == null, $"locations ({r.Locations?.Locations.Count ?? 0} kinds)");
        AddUnless(parts, r.Vegetation == null, $"vegetation ({r.Vegetation?.Count ?? 0} rules)");
        AddUnless(parts, r.Dungeons == null, DungeonsText(r.Dungeons));
        AddUnless(parts, r.ZoneSpawners, "no zone spawners");
        AddUnless(parts, !r.RandomLocations, "random locations");
        AddUnless(parts, r.Generator == null, $"{r.Generator?.Name} {r.Generator?.Version} ({r.Generator?.Settings})");
        return string.Join(", ", parts);
    }

    /// <summary>The room and dungeon files in a few words ("rooms (40), dungeons (12)"); empty for none.</summary>
    private static string DungeonsText(DungeonRules? d) => d == null ? ""
        : $"rooms ({(d.Rooms == null ? "the game's" : d.Rooms.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))}), dungeons ({d.Dungeons.Count})";

    /// <summary>Adds a part to the description unless the settings it describes are the game's own.</summary>
    private static void AddUnless(List<string> parts, bool plain, string part)
    {
        if (!plain)
        {
            parts.Add(part);
        }
    }

    /// <summary>"on" or "off".</summary>
    private static string OnOff(bool b) => b ? "on" : "off";
}
