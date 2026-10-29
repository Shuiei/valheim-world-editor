
namespace ValheimGen;

/// <summary>
/// Valheim's Heightmap: only its biome enums.
/// </summary>
public static class Heightmap
{
    /// <summary>
    /// The game's biomes, as flags, with the game's own numbers.
    /// </summary>
    public enum Biome
    {
        /// <summary>No biome.</summary>
        None = 0,
        /// <summary>Meadows.</summary>
        Meadows = 1,
        /// <summary>Swamp.</summary>
        Swamp = 2,
        /// <summary>Mountains.</summary>
        Mountain = 4,
        /// <summary>Black Forest.</summary>
        BlackForest = 8,
        /// <summary>Plains.</summary>
        Plains = 0x10,
        /// <summary>Ashlands.</summary>
        AshLands = 0x20,
        /// <summary>Deep North.</summary>
        DeepNorth = 0x40,
        /// <summary>Ocean.</summary>
        Ocean = 0x100,
        /// <summary>Mistlands.</summary>
        Mistlands = 0x200,
        /// <summary>
        /// Every biome.
        /// </summary>
        All = 0x37F,
        /// <summary>
        /// Every biome but Ocean.
        /// </summary>
        Land = 0x27F
    }

    /// <summary>
    /// Where in a biome a zone or point lies, for location placement (flags).
    /// </summary>
    public enum BiomeArea
    {
        /// <summary>
        /// On a biome's edge: the zone's corner and the corners round it are not all in one biome.
        /// </summary>
        Edge = 1,
        /// <summary>
        /// In a biome's middle.
        /// </summary>
        Median = 2,
        /// <summary>
        /// Both.
        /// </summary>
        Everything = 3
    }
}
