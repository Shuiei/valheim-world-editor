using ValheimGen;

namespace ExpandWorld.WorldEditor;

// The biome table from the biome files' entries (expand_biomes.yaml).
public static partial class ExpandWorldMod
{
    /// <summary>
    /// The world's biomes from the biome files' entries, as the mod loads them: an entry named as one of the game's
    /// nine (case does not matter) sets that biome's values; any other name is a new biome, given the next flag after
    /// Mistlands in file order (0x400, 0x800... up to 0x80000000, then the unused 0x80). The game's biomes the file does
    /// not name keep their defaults. Null, with a note, when the mod would refuse the file whole: a biome named twice,
    /// or more new biomes than there are flags.
    /// </summary>
    /// <param name="entries">The biome files' entries, in order.</param>
    /// <param name="notes">Where a refusal or an odd value is noted.</param>
    internal static BiomeTable? BiomesOf(IReadOnlyList<Entry> entries, List<string> notes)
    {
        Dictionary<string, Heightmap.Biome> names = new(StringComparer.OrdinalIgnoreCase) { ["None"] = Heightmap.Biome.None };
        foreach (var (value, id) in BiomeTable.GamesBiomes)
        {
            names[id] = value;
        }
        var last = Heightmap.Biome.Mistlands;
        List<(Heightmap.Biome Biome, string Id, Fields Fields)> items = [];
        foreach (var entry in entries)
        {
            var fields = new Fields(entry, "biomes", notes);
            string id = fields.Str("biome");
            if (!names.TryGetValue(id, out var biome))
            {
                if (last == (Heightmap.Biome)0x80)
                {
                    notes.Add("expand_biomes: too many biomes (the mod refuses the file): the game's biomes are used");
                    return null;
                }
                biome = last = last == unchecked((Heightmap.Biome)0x80000000) ? (Heightmap.Biome)0x80 : (Heightmap.Biome)(2 * (uint)last);
                names[id] = biome;
            }
            if (items.Any(i => i.Biome == biome))
            {
                notes.Add($"expand_biomes: {id} is listed twice (the mod refuses the file): the game's biomes are used");
                return null;
            }
            items.Add((biome, id, fields));
        }
        List<BiomeInfo> biomes = [];
        foreach (var (value, id) in BiomeTable.GamesBiomes)
        {
            biomes.Add(items.FirstOrDefault(i => i.Biome == value) is { Fields: { } f } item ? BiomeOf(value, id, f, names) : BiomeTable.Vanilla.Of(value)!);
        }
        foreach (var (biome, id, fields) in items.Where(i => !BiomeTable.GamesBiomes.Any(g => g.Value == i.Biome)))
        {
            biomes.Add(BiomeOf(biome, id, fields, names));
        }
        return new BiomeTable(biomes);
    }

    /// <summary>One biome's values from its entry (the mod's BiomeData, terrain and nature lookups).</summary>
    private static BiomeInfo BiomeOf(Heightmap.Biome biome, string id, Fields f, Dictionary<string, Heightmap.Biome> names)
    {
        var terrain = names.TryGetValue(f.Str("terrain"), out var t) ? t : biome;
        var nature = names.TryGetValue(f.Str("nature"), out var n) ? n : terrain;
        Rgba? terrainColour = f.Colour("colorTerrain"), mapColour = f.Colour("colorMap");
        var height = HeightOf(f);
        float forest = f.F("forestMultiplier", 1f), mapMultiplier = f.F("mapColorMultiplier", 1f), lavaAmount = f.F("lavaAmount", 1f);
        string lava = f.Str("lava");
        var colour = terrainColour ?? new Rgba(0f, 0f, 0f, 0f);
        return new BiomeInfo
        {
            Value = biome,
            Id = id,
            Name = f.Str("name"),
            Terrain = terrain,
            Nature = nature,
#pragma warning disable S1244 // Exactly the mod's defaults: any other value, however close, is a change.
            HasData = f.Count("statusEffects") > 0 || !height.IsIdentity || mapMultiplier != 1f || (mapColour?.A ?? 0f) != 0f || forest != 1f || lavaAmount != 1f,
#pragma warning restore S1244
            Height = height,
            ForestMultiplier = forest,
            MapColorMultiplier = mapMultiplier,
            MapColor = mapColour,
            TerrainColor = terrainColour,
            Water = WaterOf(f),
            StatusEffects = StatusEffectsOf(f),
            Paint = f.Str("paint") is { Length: > 0 } paint ? ParsePaint(paint) : null,
            Lava = lava == "" ? colour.A > 0.929f && colour.R > 0.929f : lava == "true",
            LavaAmount = lavaAmount,
            LavaStretch = f.F("lavaStretch", 1f),
        };
    }
}
