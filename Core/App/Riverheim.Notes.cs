using ValheimGen;

namespace TerrainEditor.App;

// What Expand World does, and does not, change about a world Riverheim made (the game's own patch order, checked on a
// server running both; docs/riverheim.md lists every setting).
public static partial class Riverheim
{
    /// <summary>The edge of Riverheim's own world, metres before the stretch (its generator's world size).</summary>
    private const float WorldEdge = 10500f;

    /// <summary>
    /// What the Expand World settings of these rules do to a Riverheim world, where it is not what they do to the game's
    /// (the log says it as the world opens): Expand World Data's world rules choose the biomes over Riverheim's, Expand
    /// World Size's altitude, offsets, height seed and biome stretch change nothing, a radius that does not reach as far
    /// as Riverheim's stretched world, and Expand World Rivers lays out no water of its own (its water level still
    /// applies). None for a world Riverheim did not make.
    /// </summary>
    /// <param name="rules">The world's rules, Riverheim's among them.</param>
    public static List<string> Notes(WorldRules rules)
    {
        if (rules.Riverheim == null)
        {
            return [];
        }
        (bool When, string Note)[] notes =
        [
            (rules.World != null, "Expand World Data's world rules choose the biomes, over Riverheim's (Riverheim's own advice: turn World data and Biome data off)"),
            (ShapesGround(rules), "Expand World Size's altitude, offsets and height seed do not change Riverheim's ground"),
            (rules.BiomeStretch != 1f, "Expand World Size's biome stretch does not change Riverheim's biomes (only the world stretch does)"),
            (rules.TotalRadius != WorldEdge * rules.Stretch, $"Riverheim's world ends at {WorldEdge * rules.Stretch:0.#} m (10500 m times the stretch) and Expand World Size's radius and edge at {rules.TotalRadius:0.#} m (Riverheim asks for them to match)"),
            (rules.RiversInstalled, "Expand World Rivers' lakes, rivers and streams are not laid out (Riverheim has its own); its water level still applies"),
        ];
        return [.. notes.Where(n => n.When).Select(n => n.Note)];
    }

    /// <summary>
    /// Whether the rules change the game's continents (Expand World Size's altitude, offsets or height seed), which
    /// Riverheim's ground does not ask.
    /// </summary>
    private static bool ShapesGround(WorldRules r) =>
        r.AltitudeMultiplier != 1f || r.AltitudeDelta != 0f || r.OffsetX != null || r.OffsetY != null || r.HeightSeed != null;
}
