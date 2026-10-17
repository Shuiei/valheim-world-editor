#nullable disable

namespace ValheimGen;

/// <summary>
/// Valheim's World: what the generator needs to know about the world it makes.
/// </summary>
public sealed class World
{
    /// <summary>
    /// The seed (the seed name's stable hash).
    /// </summary>
    public int m_seed;

    /// <summary>
    /// The seed as players type it.
    /// </summary>
    public string m_seedName = "";

    /// <summary>
    /// The world generator version the world was made with (older worlds keep older rules).
    /// </summary>
    public int m_worldGenVersion = 2;

    /// <summary>
    /// The main menu's background world: simpler terrain and no rivers.
    /// </summary>
    public bool m_menu;
}
