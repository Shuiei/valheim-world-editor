
namespace TerrainEditor.Save;

/// <summary>
/// A brand-new world from a seed, in the chunked save format the editor reads (as the game with the Deep North save
/// writes it, see WorldSave): save #1 with its metadata (.fwl2), its zone list and the random events' state (.db2), its
/// chunk index (.chunks) and the commit marker (.ok), written last. The ground is the seed's own (the world generator);
/// the zones within <c>radius</c> of the middle are generated with the game's vegetation rules (Regrow), the rest is
/// left for the game to generate; zone 0, 0 also gets an unedited terrain compiler object, so the editor can save
/// ground edits. The seed's locations in the generated zones (the start temple, altars, ruins...) are spawned as the
/// game would spawn them (Sites), and each generated zone gets its zone control object, as the game's own have; a zone
/// whose location the editor cannot make (a dungeon's entrance, whose interior the game lays out) is left whole to the
/// game. The world is marked as not having its locations laid out yet, so the game lays out the rest when it first
/// loads it.
/// </summary>
public static partial class WorldCreator
{
    /// <summary>
    /// The save format version written into every file of the new world: the one the game writes for a new world today
    /// (Valheim 1.0.20 and later, which still opens a 41 world but first migrates it, making a backup of it; 1.0.17 and
    /// earlier refuse 42).
    /// </summary>
    public const int SaveFileVersion = WorldVersion.Latest;

    /// <summary>
    /// The generator's version for new worlds (World.m_worldGenVersion) and the location list's
    /// (ZoneSystem.m_locationVersion), as the game writes them today.
    /// </summary>
    public const int WorldGenVersion = 2, LocationVersion = 32;

    /// <summary>The largest square of generated zones around the middle (radius in zones).</summary>
    public const int MaxRadius = 16;

    /// <summary>What <c>Create</c> made.</summary>
    /// <param name="Directory">The world's folder.</param>
    /// <param name="Seed">The seed number of the seed name.</param>
    /// <param name="Zones">How many zones were generated (0 with radius 0).</param>
    /// <param name="Objects">How many objects were written, the terrain compiler object included.</param>
    /// <param name="Flat">The bare zone left for pictures, or null when none was asked for.</param>
    /// <param name="FlatSpan">
    /// How many metres the bare zone's ground spans from lowest to highest (0 without one).
    /// </param>
    /// <param name="Locations">How many locations were spawned in the generated zones.</param>
    public sealed record Created(string Directory, int Seed, int Zones, int Objects, (int X, int Z)? Flat = null, float FlatSpan = 0, int Locations = 0);

    /// <summary>
    /// The most even zone of dry Meadows within <c>radius</c> zones of the middle (not on its edge), none of
    /// <paramref name="avoid"/>: the one whose ground spans the fewest metres. Null when none is wholly above the water.
    /// </summary>
    /// <param name="terrain">The world's terrain.</param>
    /// <param name="radius">How far from the middle to look, in zones.</param>
    /// <param name="avoid">Zones not to take (those holding a location); none by default.</param>
    /// <returns>
    /// The zone and its ground's span in metres (dry: its lowest point at least 1.5 m above the water).
    /// </returns>
    public static (int X, int Z, float Span)? FlattestZone(ValheimGen.TerrainService terrain, int radius, IReadOnlySet<(int X, int Z)>? avoid = null)
    {
        (int, int, float)? best = null;
        for (int z = -radius + 1; z < radius; z++)
        {
            for (int x = -radius + 1; x < radius; x++)
            {
                if (avoid?.Contains((x, z)) == true)
                {
                    continue;
                }
                float[] h = terrain.BaseZone(x, z);
                float lo = h.Min(), hi = h.Max();
                if (lo < terrain.WaterLevel + 1.5f || best is { } b && hi - lo >= b.Item3)
                {
                    continue;
                }
                bool meadows = true;
                for (int k = 0; k < 9 && meadows; k++)
                {
                    meadows = terrain.BiomeAt(x * 64 + (k % 3 - 1) * 28, z * 64 + (k / 3 - 1) * 28) == ValheimGen.Heightmap.Biome.Meadows;
                }
                if (meadows)
                {
                    best = (x, z, hi - lo);
                }
            }
        }
        return best;
    }
}
