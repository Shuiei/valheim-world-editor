using System.Text.Json.Serialization;

namespace ValheimGen;

/// <summary>
/// The biomes of a world: the game's nine (<see cref="Vanilla"/>), or those plus the ones Expand World Data's biome file
/// adds, each with what the file says about it (<see cref="BiomeInfo"/>). Every table the editor keyed by the biome
/// enum (names, colours, the terrain to draw, the point map's indices) goes through it, so a biome the game does not
/// have is drawn, named and placed like the others. The parts: this file (the biomes and their lookups),
/// BiomeTable.Parse (names to biomes and back) and BiomeTable.Index (the point map's indices).
/// </summary>
public sealed partial class BiomeTable
{
    /// <summary>
    /// The game's nine biomes with the names Expand World Data's files use for them, in the game's order (declared
    /// before <see cref="Vanilla"/>, which is made from it).
    /// </summary>
    public static readonly IReadOnlyList<(Heightmap.Biome Value, string Id)> GamesBiomes =
    [
        (Heightmap.Biome.Meadows, "Meadows"), (Heightmap.Biome.Swamp, "Swamp"), (Heightmap.Biome.Mountain, "Mountain"),
        (Heightmap.Biome.BlackForest, "BlackForest"), (Heightmap.Biome.Plains, "Plains"), (Heightmap.Biome.AshLands, "AshLands"),
        (Heightmap.Biome.DeepNorth, "DeepNorth"), (Heightmap.Biome.Ocean, "Ocean"), (Heightmap.Biome.Mistlands, "Mistlands"),
    ];

    /// <summary>The game's nine biomes, nothing changed about them.</summary>
    public static BiomeTable Vanilla { get; } = new([.. GamesBiomes.Select(b => new BiomeInfo { Value = b.Value, Id = b.Id, Terrain = b.Value, Nature = b.Value, Lava = b.Value == Heightmap.Biome.AshLands })]);

    /// <summary>A table of these biomes (the game's nine first, then the file's in its order).</summary>
    /// <param name="biomes">The biomes.</param>
    [JsonConstructor]
    public BiomeTable(IReadOnlyList<BiomeInfo> biomes)
    {
        Biomes = biomes;
        _byValue = biomes.ToDictionary(b => b.Value);
        _byName = new Dictionary<string, BiomeInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in biomes)
        {
            _byName[b.Id] = b;
        }
        LavaBiomes = biomes.Where(b => b.Lava).Aggregate(Heightmap.Biome.None, (set, b) => set | b.Value);
        All = biomes.Aggregate(Heightmap.Biome.None, (set, b) => set | b.Value);
    }

    /// <summary>The biomes, the game's nine first, then the file's in its order.</summary>
    public IReadOnlyList<BiomeInfo> Biomes { get; }

    /// <summary>The biomes with lava, as flags (the Ashlands alone for the game's).</summary>
    [JsonIgnore]
    public Heightmap.Biome LavaBiomes { get; }

    /// <summary>Every biome of the table, as flags (what an empty biome list in the mod's files means).</summary>
    [JsonIgnore]
    public Heightmap.Biome All { get; }

    /// <summary>Whether the table has biomes the game does not.</summary>
    [JsonIgnore]
    public bool HasCustom => Biomes.Count > GamesBiomes.Count;

    /// <summary>Whether any biome borrows another's ground (a "terrain" setting).</summary>
    [JsonIgnore]
    public bool HasTerrainOverrides => Biomes.Any(b => b.Terrain != b.Value);

    /// <summary>A biome's entry; null for a value the table has no biome of (none, a combination of flags).</summary>
    /// <param name="biome">The biome.</param>
    public BiomeInfo? Of(Heightmap.Biome biome) => _byValue.GetValueOrDefault(biome);

    /// <summary>The biome whose ground shape and look a biome uses (itself when the table has no other for it).</summary>
    /// <param name="biome">The biome.</param>
    public Heightmap.Biome Terrain(Heightmap.Biome biome) => _byValue.TryGetValue(biome, out var b) ? b.Terrain : biome;

    /// <summary>The biome whose plants a biome uses (itself when the table has no other for it).</summary>
    /// <param name="biome">The biome.</param>
    public Heightmap.Biome Nature(Heightmap.Biome biome) => _byValue.TryGetValue(biome, out var b) ? b.Nature : biome;

    /// <summary>The biomes by value.</summary>
    private readonly Dictionary<Heightmap.Biome, BiomeInfo> _byValue;

    /// <summary>The biomes by name (case does not matter, as the mod reads them).</summary>
    private readonly Dictionary<string, BiomeInfo> _byName;
}
