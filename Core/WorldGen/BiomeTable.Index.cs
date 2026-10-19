namespace ValheimGen;

// The biome point map's indices (the game's Heightmap.BiomeIndex, and Expand World Data's mapping of every flag).
public sealed partial class BiomeTable
{
    /// <summary>
    /// How many indices the point map keeps points under: 0 (none) and one per flag bit, as the mod's mapping numbers
    /// them (the highest, bit 31, is 32).
    /// </summary>
    public const int IndexCount = 33;

    /// <summary>
    /// The game's BiomeIndex order: none, then the biomes.
    /// </summary>
    private static readonly Heightmap.Biome[] GamesOrder =
    [
        Heightmap.Biome.None, Heightmap.Biome.Meadows, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain, Heightmap.Biome.BlackForest,
        Heightmap.Biome.Plains, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean, Heightmap.Biome.Mistlands,
    ];

    /// <summary>
    /// A single biome's index in the biome point map: the game's BiomeIndex for its nine, -1 for any other value. With
    /// Expand World Data installed (its ToBiomeIndex replaces the game's), every flag has one: bit b is index b + 1,
    /// except bit 7 (the unused 0x80, the last biome the mod can add) which is 10, bit 8 (Ocean) 8 and bit 9 (Mistlands)
    /// 9, so the game's nine keep their indices; a combination of flags has none (-1).
    /// </summary>
    /// <param name="biome">The biome.</param>
    /// <param name="dataInstalled">Whether Expand World Data is installed (its mapping applies then, data or no data).</param>
    public static int IndexOf(Heightmap.Biome biome, bool dataInstalled)
    {
        if (!dataInstalled)
        {
            return Array.IndexOf(GamesOrder, biome);
        }
        if (biome == Heightmap.Biome.None)
        {
            return 0;
        }
        uint flags = (uint)biome;
        if (!System.Numerics.BitOperations.IsPow2(flags))
        {
            return -1;
        }
        int bit = System.Numerics.BitOperations.TrailingZeroCount(flags);
        return bit switch
        {
            7 => 10,
            8 => 8,
            9 => 9,
            _ => bit + 1,
        };
    }

    /// <summary>The biome at a point map index (the inverse of <see cref="IndexOf"/>); none for an index without one.</summary>
    /// <param name="index">The index.</param>
    /// <param name="dataInstalled">Whether Expand World Data is installed.</param>
    public static Heightmap.Biome AtIndex(int index, bool dataInstalled)
    {
        if (!dataInstalled)
        {
            return index >= 0 && index < GamesOrder.Length ? GamesOrder[index] : Heightmap.Biome.None;
        }
        if (index is <= 0 or >= IndexCount)
        {
            return Heightmap.Biome.None;
        }
        int bit = index switch
        {
            10 => 7,
            8 => 8,
            9 => 9,
            _ => index - 1,
        };
        return (Heightmap.Biome)(1u << bit);
    }
}
