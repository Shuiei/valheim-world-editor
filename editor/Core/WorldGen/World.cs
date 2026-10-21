#nullable disable

namespace ValheimGen;

/// <summary>
/// Valheim's World: what the generator needs to know about the world it makes.
/// </summary>
internal sealed class World
{
    /// <summary>
    /// The seed (the seed name's stable hash).
    /// </summary>
    public int Seed;

    /// <summary>
    /// The seed as players type it.
    /// </summary>
    public string SeedName = "";

    /// <summary>
    /// The world generator version the world was made with (older worlds keep older rules).
    /// </summary>
    public int WorldGenVersion = 2;

    /// <summary>
    /// The main menu's background world: simpler terrain and no rivers.
    /// </summary>
    public bool Menu;

    /// <summary>
    /// What a world generation mod changed about the world (the game's own values by default).
    /// </summary>
    public WorldRules Rules = WorldRules.Vanilla;
}
