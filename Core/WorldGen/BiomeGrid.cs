namespace ValheimGen;

/// <summary>
/// The grid of AltBiomeWorldData's biome points: 2048 points a side, 12 m apart, point 1024 at the middle as the game
/// has it; Expand World Size sizes it to the world (BiomeMapSize: as many points as the total radius needs at the
/// game's density, fewer when the world is stretched, each point the stretch times wider).
/// </summary>
/// <param name="Size">Points a side.</param>
/// <param name="Middle">The map coordinate of the world's middle (half the size).</param>
/// <param name="Pixel">Metres between two points.</param>
public readonly record struct BiomeGrid(int Size, float Middle, float Pixel)
{
    /// <summary>The game's grid.</summary>
    public static readonly BiomeGrid Game = new(2048, 1024f, 12f);

    /// <summary>Half a point, metres: a point's middle from its corner.</summary>
    public float Half => Pixel / 2f;

    /// <summary>
    /// The grid a world's rules give: the game's, or the one Expand World Size makes for its radius and stretch
    /// (the same as the game's at the game's radius without a stretch).
    /// </summary>
    public static BiomeGrid Of(WorldRules rules)
    {
        if (rules.TotalRadius == 10500f && rules.Stretch == 1f)
        {
            return Game;
        }
        int size = (int)MathF.Ceiling((float)(2048.0 * rules.TotalRadius / 10500.0 / rules.Stretch));
        return new BiomeGrid(size, size / 2f, 12f * rules.Stretch);
    }

    /// <summary>A map coordinate to world metres (the point's middle; MapSpaceToWorldSpace).</summary>
    public float MapToWorld(float x) => (x - Middle) * Pixel + Half;

    /// <summary>
    /// World metres to the map coordinate of the point's cell holding them (WorldSpaceToMapSpace, cut toward zero as
    /// the game casts it); <see cref="MapToWorld"/> of it snaps a position to its cell's middle.
    /// </summary>
    public int WorldToMap(float v) => (int)((v - Half) / Pixel + Middle);
}
