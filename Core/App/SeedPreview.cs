using System.Security.Cryptography;
using TerrainEditor.Save;
using ValheimGen;

namespace TerrainEditor.App;

/// <summary>
/// A seed seen from above before its world exists (the New world page): the game's own generator
/// (WorldGenerator) sampled on a coarse grid over the whole world, and what a player would care about:
/// how much land, which biomes, the main continent, where the start is (SeedLocations: where the game
/// will put its start temple), how far each biome is from there and, when asked, where the bosses and
/// traders will be.
/// </summary>
public static partial class SeedPreview
{
    /// <summary>The world's radius in metres (10 km): the preview covers -Radius..Radius on both axes.</summary>
    public const float Radius = WorldGenerator.WorldSize;

    /// <summary>The biomes counted, in the order players meet them. A cell's biome byte is an index in it.</summary>
    public static readonly Heightmap.Biome[] Biomes =
    [
        Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
        Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth,
    ];

    /// <summary>
    /// The places worth knowing about before playing: the start, the traders, the bosses' altars. Prefab is
    /// the game's location, Name what the player calls it, Kind "start", "trader" or "boss".
    /// </summary>
    public static readonly (string Prefab, string Name, string Kind)[] Landmarks =
    [
        ("StartTemple", "The start", "start"),
        ("Vendor_BlackForest", "Haldor", "trader"),
        ("BogWitch_Camp", "The Bog Witch", "trader"),
        ("Hildir_camp", "Hildir", "trader"),
        ("Eikthyrnir", "Eikthyr", "boss"),
        ("GDKing", "The Elder", "boss"),
        ("Bonemass", "Bonemass", "boss"),
        ("Dragonqueen", "Moder", "boss"),
        ("GoblinKing", "Yagluth", "boss"),
        ("Mistlands_DvergrBossEntrance1", "The Queen", "boss"),
        ("FaderLocation", "Fader", "boss"),
    ];

    /// <summary>
    /// The dungeons, by kind: the game's locations that lead into one (or are one, as the Ashlands'
    /// fortresses), and their biome (None: in several).
    /// </summary>
    public static readonly (string Name, Heightmap.Biome Biome, string[] Prefabs)[] Dungeons =
    [
        ("Burial chambers", Heightmap.Biome.BlackForest, new[] { "Crypt2", "Crypt3", "Crypt4" }),
        ("Troll caves", Heightmap.Biome.BlackForest, new[] { "TrollCave02" }),
        ("Sunken crypts", Heightmap.Biome.Swamp, new[] { "SunkenCrypt4" }),
        ("Frost caves", Heightmap.Biome.Mountain, new[] { "MountainCave02" }),
        ("Infested mines", Heightmap.Biome.Mistlands, new[] { "Mistlands_DvergrTownEntrance1", "Mistlands_DvergrTownEntrance2" }),
        ("Charred fortresses", Heightmap.Biome.AshLands, new[] { "CharredFortress" }),
        ("Hildir's dungeons", Heightmap.Biome.None, new[] { "Hildir_cave", "Hildir_crypt", "Hildir_plainsfortress" }),
    ];

    /// <summary>
    /// Where the seed's dungeons will be (Kind "dungeon", Name the Dungeons kind). The game's smaller
    /// locations before them are laid out too: a few seconds.
    /// </summary>
    public static List<Landmark> FindDungeons(WorldGenerator gen, bool parallel = false, CancellationToken cancel = default) =>
        FindDungeons(new SeedLocations.Layout(gen, parallel), cancel);

    /// <summary>The same, going on from a layout under way (a preview's: what it laid out is not done again).</summary>
    public static List<Landmark> FindDungeons(SeedLocations.Layout layout, CancellationToken cancel = default)
    {
        var byPrefab = Dungeons.SelectMany(d => d.Prefabs.Select(p => (p, d.Name))).ToDictionary(x => x.p, x => x.Name);
        return [.. layout.PlaceKinds(byPrefab.Keys, cancel).Select(p => new Landmark(byPrefab[p.Rule.Prefab], "dungeon", p.X, p.Z, false))];
    }

    /// <summary>The bosses of the first five biomes, for the "bosses within" wish.</summary>
    public static readonly string[] EarlyBosses = ["Eikthyr", "The Elder", "Bonemass", "Moder", "Yagluth"];

    /// <summary>
    /// One landmark: OneOf when the game lays out several spots and keeps only the first a player comes
    /// near (the traders), the rest vanishing.
    /// </summary>
    /// <param name="Name">
    /// What the player calls it (a <see cref="Landmarks"/> name, or a <see cref="Dungeons"/> kind).
    /// </param>
    /// <param name="Kind">"start", "trader", "boss" or "dungeon".</param>
    /// <param name="X">World x (metres).</param>
    /// <param name="Z">World z (metres).</param>
    /// <param name="OneOf">One of several spots of which the game keeps only one.</param>
    public sealed record Landmark(string Name, string Kind, float X, float Z, bool OneOf);

    /// <summary>What a preview says about a seed. Shares are 0..1.</summary>
    /// <param name="Land">Share of the world (inside its 10 km) that is land.</param>
    /// <param name="Shares">Share of the land in each biome.</param>
    /// <param name="MainContinent">Share of the land in the largest landmass.</param>
    /// <param name="Start">Where the start is likely to be.</param>
    /// <param name="LandNearStart">Share of land within 400 m of it.</param>
    /// <param name="StartContinent">Share of the land in the start's own landmass.</param>
    /// <param name="Distance">Metres from the start to the nearest of each biome (PositiveInfinity: none).</param>
    /// <param name="SameLand">Whether that nearest one is on the start's landmass.</param>
    /// <param name="Nearest">Metres from the start to the nearest of each landmark (null: not looked for).</param>
    /// <param name="TempleStart">
    /// Start is the game's start temple (else the Meadows nearest the middle: no rules from the game).
    /// </param>
    public sealed record Stats(
        float Land,
        IReadOnlyDictionary<Heightmap.Biome, float> Shares,
        float MainContinent,
        (float X, float Z) Start,
        float LandNearStart,
        float StartContinent,
        IReadOnlyDictionary<Heightmap.Biome, float> Distance,
        IReadOnlyDictionary<Heightmap.Biome, bool> SameLand,
        IReadOnlyDictionary<string, float>? Nearest = null,
        bool TempleStart = false);

    /// <summary>A seed's preview: Size x Size cells of Cell metres over the world, row 0 the south.</summary>
    /// <param name="Seed">The seed's name.</param>
    /// <param name="Size">Cells on a side.</param>
    /// <param name="Cell">A cell's side in metres.</param>
    /// <param name="Biome">
    /// Each cell's biome: index in <see cref="Biomes"/>, 255 (<see cref="Ocean"/>) ocean or outside.
    /// </param>
    /// <param name="Height">Each cell's ground height (<see cref="Outside"/> past the world's edge).</param>
    /// <param name="Stats">What it says about the seed.</param>
    /// <param name="Landmarks">Where the start, bosses and traders will be; null when not looked for.</param>
    /// <param name="Generator">The seed's, for a closer look at a part (Sample); null in search results.</param>
    /// <param name="Layout">Its locations laid out so far, to go on from (FindDungeons); null in search results.</param>
    public sealed record Preview(string Seed, int Size, float Cell, byte[] Biome, float[] Height, Stats Stats, IReadOnlyList<Landmark>? Landmarks = null, WorldGenerator? Generator = null, SeedLocations.Layout? Layout = null)
    {
        /// <summary>The world position of cell (i, j)'s middle.</summary>
        public (float X, float Z) CellCenter(int i, int j) => (-Radius + (i + 0.5f) * Cell, -Radius + (j + 0.5f) * Cell);
    }

    /// <summary>A cell's biome byte for the sea and outside the world.</summary>
    public const byte Ocean = 255;

    /// <summary>The height of a cell past the world's edge.</summary>
    public const float Outside = float.NegativeInfinity;

    /// <summary>The game's world generator for a seed name, at the current world generation version.</summary>
    public static WorldGenerator Generator(string seedName) => WorldGenerator.Create(new World
    {
        SeedName = seedName,
        Seed = WorldCreator.SeedOf(seedName),
        WorldGenVersion = WorldCreator.WorldGenVersion,
    });

    /// <summary>A random seed name as the game makes them: 10 letters and digits.</summary>
    public static string RandomSeed()
    {
        const string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return string.Create(10, 0, (span, _) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
            }
        });
    }
    /// <summary>What makes a seed good: each criterion adds to the score (0 when it is not asked for).</summary>
    /// <param name="BigStartLand">The start on a large landmass.</param>
    /// <param name="LandAroundStart">Little water right around the start.</param>
    /// <param name="MaxSwampDistance">The first Swamp within this many metres of the start (0: no wish).</param>
    /// <param name="MaxMountainDistance">The first Mountain within this many metres of the start (0: no wish).</param>
    /// <param name="MaxPlainsDistance">The first Plains within this many metres of the start (0: no wish).</param>
    /// <param name="BiomesOnStartLand">The nearest of each biome on the start's landmass (no sailing needed).</param>
    /// <param name="MinLand">At least this share of land.</param>
    /// <param name="MaxLand">At most this share.</param>
    /// <param name="MaxHaldorDistance">The nearest of Haldor's spots within this many metres (0: no wish).</param>
    /// <param name="MaxBogWitchDistance">The nearest of the Bog Witch's spots within this many metres (0: no wish).</param>
    /// <param name="MaxHildirDistance">The nearest of Hildir's spots within this many metres (0: no wish).</param>
    /// <param name="MaxBossDistance">The nearest altar of each of the first five bosses within this many metres.</param>
    public sealed record Wishes(
        bool BigStartLand = true,
        bool LandAroundStart = true,
        float MaxSwampDistance = 0,
        float MaxMountainDistance = 0,
        float MaxPlainsDistance = 0,
        bool BiomesOnStartLand = false,
        float MinLand = 0,
        float MaxLand = 1,
        float MaxHaldorDistance = 0,
        float MaxBogWitchDistance = 0,
        float MaxHildirDistance = 0,
        float MaxBossDistance = 0)
    {
        /// <summary>Whether the bosses and traders must be looked for.</summary>
        public bool NeedsLandmarks => MaxHaldorDistance > 0 || MaxBogWitchDistance > 0 || MaxHildirDistance > 0 || MaxBossDistance > 0;
    }
}
