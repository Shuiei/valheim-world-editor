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
        return string.Join(", ", parts);
    }

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
