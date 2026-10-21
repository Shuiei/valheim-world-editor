using System.Text.Json.Serialization;

namespace ValheimGen;

/// <summary>
/// A world made with Riverheim (<see cref="WorldRules.Riverheim"/>): the player's Riverheim lays its ground out
/// (<see cref="LayOut"/>), in place of the game's continents, biomes, rivers and lakes.
/// </summary>
/// <param name="Version">The player's Riverheim's version (another version may lay the same world out otherwise).</param>
/// <param name="Preset">The preset the world was made with (kept in its world file).</param>
/// <param name="PresetVersion">The preset's version the world was made with.</param>
public sealed record RiverheimRules(string Version, string Preset, int PresetVersion)
{
    /// <summary>
    /// Lays a world out for a seed (seconds of work; the answer is shared by the generators of that world). Not part of
    /// the rules' settings: two rules with the same Riverheim version and preset lay out the same ground.
    /// </summary>
    [JsonIgnore]
    internal Func<int, IGroundGenerator> LayOut { get; init; } = _ => throw new InvalidOperationException("No Riverheim to lay the world out.");
}
