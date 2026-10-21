using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

// What Expand World does, and does not, change about a world Riverheim made (the game's own patch order, checked on a
// server running both; docs/riverheim.md lists every setting).
public static partial class Riverheim
{
    /// <summary>The edge of Riverheim's own world, metres before the stretch (its generator's world size).</summary>
    private const float WorldEdge = 10500f;

    /// <summary>
    /// What Expand World's settings do to a Riverheim world, where it is not what they do to the game's (the log says it
    /// as the world opens): Expand World Data's world rules choose the biomes over Riverheim's, Expand World Size's
    /// altitude, offsets, height seed and biome stretch change nothing, a radius that does not reach as far as
    /// Riverheim's stretched world, and Expand World Rivers lays out no water of its own (its water level still applies).
    /// </summary>
    /// <param name="ew">The world's Expand World settings.</param>
    public static List<string> Notes(ExpandWorldSettings ew)
    {
        (bool When, string Note)[] notes =
        [
            (ew.WorldData, "Expand World Data's world rules choose the biomes, over Riverheim's (Riverheim's own advice: turn World data and Biome data off)"),
            (ew.ShapesGround, "Expand World Size's altitude, offsets and height seed do not change Riverheim's ground"),
            (ew.BiomeStretch != 1f, "Expand World Size's biome stretch does not change Riverheim's biomes (only the world stretch does)"),
            (ew.Radius != WorldEdge * ew.Stretch, $"Riverheim's world ends at {WorldEdge * ew.Stretch:0.#} m (10500 m times the stretch) and Expand World Size's radius and edge at {ew.Radius:0.#} m (Riverheim asks for them to match)"),
            (ew.Rivers, "Expand World Rivers' lakes, rivers and streams are not laid out (Riverheim has its own); its water level still applies"),
        ];
        return [.. notes.Where(n => n.When).Select(n => n.Note)];
    }
}
