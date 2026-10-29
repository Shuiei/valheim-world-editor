namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The Expand World settings a world opens with, as the editor reads them (the game's own values without the mods), for
/// a world generator's notes (<see cref="IWorldGenerator.Notes"/>). Changing them changes nothing.
/// </summary>
public sealed class ExpandWorldSettings
{
    /// <summary>Expand World Size's world stretch (1: none).</summary>
    public float Stretch { get; set; } = 1f;

    /// <summary>Expand World Size's biome stretch (1: none).</summary>
    public float BiomeStretch { get; set; } = 1f;

    /// <summary>The world's radius with its edge, metres (10500 for the game's).</summary>
    public float Radius { get; set; } = 10500f;

    /// <summary>Whether Expand World Size changes the game's continents: its altitude, offsets or height seed.</summary>
    public bool ShapesGround { get; set; }

    /// <summary>Whether Expand World Data's world rules choose the biomes.</summary>
    public bool WorldData { get; set; }

    /// <summary>Whether Expand World Rivers is installed and on.</summary>
    public bool Rivers { get; set; }
}
