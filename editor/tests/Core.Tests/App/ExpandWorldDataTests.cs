using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data's files as the editor reads them: the switches, the files of a kind in the mod's order, the biome
/// table, the world rules and the territories made from their entries, and the rules a folder with them gives.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldDataTests : AppSettingsTestBase
{
    private static readonly Heightmap.Biome First = (Heightmap.Biome)0x400, Second = (Heightmap.Biome)0x800;

    /// <summary>An entry from its fields.</summary>
    private static ExpandWorld.Entry E(params (string Key, string Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => (object?)f.Value));

    /// <summary>
    /// A BepInEx folder with Expand World Data installed: its settings file with the switches given, the library under plugins
    /// (unless told not to), and the data files given under config/expand_world.
    /// </summary>
    private string Bep(string cfg, bool library = true, params (string Name, string Text)[] files)
    {
        string bep = Path.Combine(Data, "bep");
        // A test may make the folder twice: the second one starts afresh.
        if (Directory.Exists(bep))
        {
            Directory.Delete(bep, true);
        }
        string data = Path.Combine(bep, "config", "expand_world");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(Path.Combine(bep, "plugins"));
        File.WriteAllText(Path.Combine(bep, "config", "expand_world_data.cfg"), cfg);
        TestMods.Install(bep, TestMods.Data);
        if (library)
        {
            YamlStandIn.Install(bep);
        }
        foreach (var (name, text) in files)
        {
            File.WriteAllText(Path.Combine(data, name), text);
        }
        return bep;
    }

    private const string AllOn = "[3. Data]\nBiome data = true\nWorld data = true\nTerritory data = true\n";

    [Fact]
    public void TheSwitchesDecideWhatIsRead()
    {
        var off = ExpandWorld.ReadData(ExpandWorld.Look(Bep("[3. Data]\nBiome data = false\n", true, ("expand_world.yaml", "- biome: meadows\n")), "test"));
        Assert.False(off.Any);
        Assert.Null(off.World);
        var on = ExpandWorld.ReadData(ExpandWorld.Look(Bep("[3. Data]\nWorld data = true\n", true, ("expand_world.yaml", "- biome: meadows\n")), "test"));
        Assert.True(on.Any);
        Assert.Null(on.Biomes);
        Assert.Equal("meadows", Assert.Single(on.World!).Fields["biome"]);
        Assert.Empty(on.Notes);
        Assert.False(ExpandWorld.ReadData(ExpandWorld.Found.None).Any);
    }

    [Fact]
    public void AKindsFilesAreReadInReverseAlphabeticalOrderAndABadOneIsPassedOver()
    {
        string bep = Bep(AllOn, true, ("expand_world.yaml", "- biome: meadows\n"), ("expand_world_extra.yaml", "- biome: swamp\n"), ("expand_biomes.yaml", "- biome: [broken\n"), ("expand_territories.yaml", ""));
        var data = ExpandWorld.ReadData(ExpandWorld.Look(bep, "test"));
        Assert.Equal(["swamp", "meadows"], data.World!.Select(e => e.Fields["biome"]));
        Assert.Empty(data.Biomes!);
        Assert.Empty(data.Territories!);
        Assert.Contains(data.Notes, n => n.StartsWith("expand_biomes.yaml could not be read", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutTheLibraryTheFilesAreNotReadAndAMissingFileMeansTheDefaults()
    {
        var data = ExpandWorld.ReadData(ExpandWorld.Look(Bep(AllOn, false, ("expand_world.yaml", "- biome: meadows\n")), "test"));
        Assert.Null(data.World);
        // No file of the kind at all: nothing to read, library or no library.
        Assert.Empty(data.Biomes!);
        Assert.Contains(data.Notes, n => n.Contains(YamlReader.DllName));
        var rules = ExpandWorld.Rules(new Dictionary<string, string>(), ExpandWorld.Look(Path.Combine(Data, "bep"), "test"), data);
        Assert.Null(rules.World);
        Assert.False(rules.BiomeData);
        // The world switch on with no file: the mod writes its default rules, and so has them.
        var fresh = ExpandWorld.ReadData(ExpandWorld.Look(Bep("[3. Data]\nWorld data = true\n"), "test"));
        Assert.Empty(fresh.World!);
        Assert.Equal(10, ExpandWorld.Rules(new Dictionary<string, string>(), ExpandWorld.Look(Path.Combine(Data, "bep"), "test"), fresh).World!.Count);
    }

    [Fact]
    public void TheBiomeTableFollowsTheFile()
    {
        List<string> notes = [];
        var table = ExpandWorld.BiomesOf(
        [
            E(("biome", "meadows"), ("altitudeDelta", "5"), ("name", "Fields")),
            E(("biome", "Highlands"), ("terrain", "Mountain"), ("colorMap", "0.5, 0.5, 0.5"), ("colorTerrain", "1, 0, 0"), ("paint", "dirt")),
            E(("biome", "Lowlands"), ("nature", "swamp"), ("lava", "true"), ("lavaAmount", "0.5"), ("lavaStretch", "2")),
            E(("biome", "AshLands"), ("colorTerrain", "0, 0, 0, 0")),
        ], notes)!;
        Assert.Empty(notes);
        Assert.Equal(11, table.Biomes.Count);
        var meadows = table.Of(Heightmap.Biome.Meadows)!;
        Assert.Equal(5f, meadows.Height.Delta);
        Assert.True(meadows.HasData);
        Assert.Equal("Fields", meadows.Name);
        Assert.Equal("Meadows", meadows.Id);
        var high = table.Of(First)!;
        Assert.Equal("Highlands", high.Id);
        Assert.Equal(Heightmap.Biome.Mountain, high.Terrain);
        Assert.Equal(Heightmap.Biome.Mountain, high.Nature);
        Assert.True(high.HasData);
        Assert.Equal(new Rgba(0.5f, 0.5f, 0.5f, 1f), high.MapColor);
        Assert.Equal(new Rgba(1f, 0f, 0f, 1f), high.TerrainColor);
        Assert.Equal(new Rgba(1f, 0f, 0f, 1f), high.Paint);
        // Red and opaque: lava, as the mod guesses from the colour.
        Assert.True(high.Lava);
        var low = table.Of(Second)!;
        Assert.Equal(Second, low.Terrain);
        Assert.Equal(Heightmap.Biome.Swamp, low.Nature);
        Assert.True(low.Lava);
        Assert.True(low.HasData);
        Assert.Equal(0.5f, low.LavaAmount);
        Assert.Equal(2f, low.LavaStretch);
        // The Ashlands given a see-through colour lose their lava; the Swamp, not in the file, keeps its defaults.
        Assert.False(table.Of(Heightmap.Biome.AshLands)!.Lava);
        Assert.Equal(First | Second, table.LavaBiomes);
        Assert.True(table.Of(Heightmap.Biome.Swamp)!.IsGames);
    }

    [Fact]
    public void AFileTheModRefusesLeavesTheGamesBiomes()
    {
        List<string> notes = [];
        Assert.Null(ExpandWorld.BiomesOf([E(("biome", "meadows")), E(("biome", "MEADOWS"))], notes));
        Assert.Contains(notes, n => n.Contains("listed twice"));
        List<ExpandWorld.Entry> tooMany = [.. Enumerable.Range(0, 24).Select(i => E(("biome", "B" + i)))];
        notes.Clear();
        Assert.Null(ExpandWorld.BiomesOf(tooMany, notes));
        Assert.Contains(notes, n => n.Contains("too many"));
        // Twenty-three fit: the last two take 0x80000000 and the unused 0x80.
        var fits = ExpandWorld.BiomesOf([.. tooMany.Take(23)], notes)!;
        Assert.Equal(unchecked((Heightmap.Biome)0x80000000), fits.Of(fits.Parse("B21"))!.Value);
        Assert.Equal((Heightmap.Biome)0x80, fits.Of(fits.Parse("B22"))!.Value);
    }

    [Fact]
    public void WorldRulesAreConvertedAsTheModConvertsThem()
    {
        List<string> notes = [];
        var wiggles = new ExpandWorld.Wiggles(400f, 0.02f, 10f, 50f);
        var (rules, sectors) = ExpandWorld.WorldOf(
        [
            E(("biome", "ashlands"), ("minDistance", "0.2"), ("maxDistance", "0.6"), ("centerY", "0.4"), ("minSector", "-0.1"), ("maxSector", "1.2"), ("wiggleDistance", "false"), ("seed", "swamp"), ("territory", " Hot Zone "), ("altBiome", "a, b")),
            E(("biome", "meadows, swamp"), ("seed", "42"), ("boiling", "0.5"), ("sizeX", "0.1"), ("rotation", "90"), ("amount", "0.3"), ("stretch", "2"), ("wiggleSectorWidth", "0.05"), ("nowhere", "ignored")),
            E(("biome", "Nowhere"), ("minAltitude", "abc")),
        ], BiomeTable.Vanilla, 20000f, wiggles, notes);
        Assert.True(sectors);
        var ash = rules[0];
        Assert.Equal(Heightmap.Biome.AshLands, ash.Biome);
        Assert.Equal(4000f, ash.MinDistance);
        Assert.Equal(12000f, ash.MaxDistance);
        Assert.Equal(8000f, ash.CenterY);
        Assert.Equal(0.9f, ash.MinSector, 5);
        Assert.Equal(0.2f, ash.MaxSector, 5);
        Assert.Equal(0f, ash.WiggleDistanceWidth);
        Assert.Equal(10f, ash.WiggleDistanceLength);
        Assert.Equal(400f, ash.WiggleSectorLength);
        Assert.Equal(0.02f, ash.WiggleSectorWidth);
        Assert.Equal(Heightmap.Biome.Swamp, ash.SeedBiome);
        Assert.Null(ash.Seed);
        Assert.Equal("hot zone", ash.Territory);
        Assert.Equal(["a", "b"], ash.AltBiomes);
        // The Ashlands boil unless told otherwise.
        Assert.Equal(1f, ash.Boiling);
        Assert.False(ash.HasRectangle);
        var both = rules[1];
        Assert.Equal(Heightmap.Biome.Meadows | Heightmap.Biome.Swamp, both.Biome);
        Assert.Equal(42, both.Seed);
        Assert.Equal(0.5f, both.Boiling);
        Assert.True(both.HasRectangle);
        Assert.Equal(1000f, both.HalfSizeX);
        Assert.Equal(float.PositiveInfinity, both.HalfSizeY);
        Assert.Equal(0f, both.RotationCos, 5);
        Assert.Equal(1f, both.RotationSin, 5);
        Assert.Equal(50f, both.WiggleDistanceWidth);
        Assert.Equal(0.05f, both.WiggleSectorWidth);
        Assert.Equal(400f, both.WiggleRectangleLength);
        Assert.Equal(50f, both.WiggleRectangleWidth);
        Assert.Equal(0.3f, both.Amount);
        Assert.Equal(2f, both.Stretch);
        // A name that is no biome, and a value that is no number, are noted and left out.
        Assert.Equal(Heightmap.Biome.None, rules[2].Biome);
        Assert.Equal(-1000f, rules[2].MinAltitude);
        Assert.Contains(notes, n => n.Contains("Nowhere"));
        Assert.Contains(notes, n => n.Contains("minAltitude"));
    }

    [Fact]
    public void TheModsDefaultRulesAreTheTenItWrites()
    {
        var (rules, sectors) = ExpandWorld.WorldOf(ExpandWorld.DefaultWorld, BiomeTable.Vanilla, 10000f, ExpandWorld.Wiggles.Default, []);
        Assert.False(sectors);
        Assert.Equal(10, rules.Count);
        Assert.Equal(Heightmap.Biome.AshLands, rules[0].Biome);
        Assert.Equal(1f, rules[0].Boiling);
        Assert.Equal(4000f, rules[0].CenterY);
        Assert.Equal(12000f, rules[0].MinDistance);
        Assert.Equal(Heightmap.Biome.Meadows, rules[9].Biome);
        Assert.Equal(0f, rules[4].WiggleDistanceWidth);
    }

    [Fact]
    public void TerritoriesKeepOnlyThoseWithSomethingSet()
    {
        List<string> notes = [];
        var territories = ExpandWorld.TerritoriesOf(
        [
            E(("territory", "Plain")),
            E(("territory", " High "), ("altitudeDelta", "10")),
            E(("territory", "Red"), ("colorTerrain", "1,0,0"), ("noBuild", "yes")),
            E(("territory", "high"), ("forestMultiplier", "2")),
            E(("altitudeDelta", "5")),
        ], notes);
        Assert.Empty(notes);
        Assert.Equal(["high", "red"], territories.Select(t => t.Name));
        // The last of two the same wins.
        Assert.Equal(2f, territories[0].ForestMultiplier);
        Assert.Equal(0f, territories[0].Height.Delta);
        Assert.True(territories[1].NoBuild);
        Assert.Equal(new Rgba(1f, 0f, 0f, 1f), territories[1].TerrainColor);
    }

    [Fact]
    public void AFolderWithTheFilesGivesRulesWithThem()
    {
        string cfg = AllOn + "[1. General]\nLegacy generation = true\n[2. Features]\nWiggle frequency = 25\nCustom water color = true\n[4. Poles]\nAshlands gap = false\nAshlands width restriction = 5000\n";
        string bep = Bep(cfg, true,
            ("expand_biomes.yaml", "- biome: Highlands\n  terrain: mountain\n  altitudeMultiplier: 1.5\n  colorWaterTop: 1, 0, 0, 1\n"),
            ("expand_world.yaml", "- biome: highlands\n  maxDistance: 0.1\n  territory: top\n- biome: meadows\n"),
            ("expand_territories.yaml", "- territory: top\n  altitudeDelta: 3\n  colorWaterSurface: 0, 0, 1, 1\n"));
        var found = ExpandWorld.Look(bep, "test");
        Assert.True(found.Data);
        var rules = ExpandWorld.Rules(new Dictionary<string, string>(), found);
        Assert.True(rules.DataInstalled);
        Assert.True(rules.BiomeData);
        Assert.True(rules.LegacyGeneration);
        Assert.Equal(25f, rules.WiggleFrequency);
        Assert.False(rules.AshlandsGap);
        Assert.Equal(5000f, rules.AshlandsWidthRestriction);
        Assert.Equal(10, rules.Biomes.Biomes.Count);
        Assert.Equal(1.5f, rules.Biomes.Of(First)!.Height.Multiplier);
        Assert.Equal(2, rules.World!.Count);
        Assert.Equal(First, rules.World[0].Biome);
        Assert.Equal(1000f, rules.World[0].MaxDistance);
        Assert.Equal("top", Assert.Single(rules.Territories).Name);
        Assert.False(rules.IsVanilla);
        string text = WorldRulesText.Describe(rules);
        Assert.Contains("biome data (10 biomes)", text);
        Assert.Contains("world rules (2, legacy)", text);
        Assert.Contains("territories (1)", text);
        // The generator takes them: the middle is Highlands ground, 3 m up for the territory.
        var gen = WorldGenerator.Create(new World { Seed = 1, WorldGenVersion = 2, Rules = rules });
        Assert.Equal(First, gen.GetBiome(100f, 100f));
        Assert.Equal(Heightmap.Biome.Meadows, gen.GetBiome(3000f, 0f));
        Assert.Equal("top", gen.TerritoryAt(100f, 100f)!.Name);
        // The water colours, with the switch on.
        Assert.True(rules.CustomWaterColor);
        Assert.Equal(new WaterColours(Top: new Rgba(1f, 0f, 0f, 1f)), rules.Biomes.Of(First)!.Water);
        Assert.Equal(new WaterColours(Surface: new Rgba(0f, 0f, 1f, 1f)), rules.Territories[0].Water);
    }

    [Fact]
    public void TheRoomAndDungeonFilesAreReadWithTheirSwitches()
    {
        string bep = Bep("[3. Data]\nRoom data = true\nDungeon data = true\n", true,
            ("expand_rooms.yaml", "- name: cave_new_corridor01:Lit\n  theme: Cave\n  connections:\n  - position: 1,2,3\n    type: lit\n"),
            ("expand_dungeons.yaml", "- name: DG_Cave\n  algorithm: Dungeon\n  roomLimits:\n    cave_new_corridor05: max 2\n"));
        var rules = ExpandWorld.Rules(new Dictionary<string, string>(), ExpandWorld.Look(bep, "test"));
        var room = Assert.Single(rules.Dungeons!.Rooms!);
        Assert.Equal(("cave_new_corridor01:Lit", "lit"), (room.Name, Assert.Single(room.Connections).Type));
        Assert.Equal(new RoomLimit("cave_new_corridor05", 0, 2, 0), Assert.Single(Assert.Single(rules.Dungeons.Dungeons).Limits));
        Assert.Contains("rooms (1), dungeons (1)", WorldRulesText.Describe(rules));
        // The switches off: the game's.
        Assert.Null(ExpandWorld.Rules(new Dictionary<string, string>(), ExpandWorld.Look(Bep("[3. Data]\nRoom data = false\n", true, ("expand_rooms.yaml", "- name: x\n")), "test")).Dungeons);
    }

    [Fact]
    public void StatusEffectsAreReadAsTheModReadsThem()
    {
        List<string> notes = [];
        List<object?> effects =
        [
            new Dictionary<string, object?> { ["name"] = "Freezing", ["night"] = "true" },
            new Dictionary<string, object?> { ["name"] = "Wet" },
            "not a map",
        ];
        var entry = new ExpandWorld.Entry(new Dictionary<string, object?> { ["territory"] = "Cold", ["statusEffects"] = effects });
        var territory = Assert.Single(ExpandWorld.TerritoriesOf([entry], notes));
        // Neither day nor night said: both.
        Assert.Equal([new StatusEffect("Freezing", false, true), new StatusEffect("Wet", true, true)], territory.StatusEffects);
        var biome = new ExpandWorld.Entry(new Dictionary<string, object?> { ["biome"] = "Meadows", ["statusEffects"] = new List<object?> { new Dictionary<string, object?> { ["name"] = "Rested", ["day"] = "yes" } } });
        var meadows = ExpandWorld.BiomesOf([biome], notes)!.Of(Heightmap.Biome.Meadows)!;
        Assert.True(meadows.HasData);
        Assert.Equal([new StatusEffect("Rested", true, false)], meadows.StatusEffects);
        Assert.Empty(notes);
    }

    [Fact]
    public void WaterColoursAloneKeepATerritoryButGiveABiomeNoSettings()
    {
        List<string> notes = [];
        var territory = Assert.Single(ExpandWorld.TerritoriesOf([E(("territory", "Sea"), ("colorWaterShallow", "0,1,0"))], notes));
        Assert.Equal(new WaterColours(Shallow: new Rgba(0f, 1f, 0f, 1f)), territory.Water);
        var table = ExpandWorld.BiomesOf([E(("biome", "Meadows"), ("colorWaterBottom", "0,0,1"))], notes)!;
        var meadows = table.Of(Heightmap.Biome.Meadows)!;
        Assert.False(meadows.HasData);
        Assert.Equal(new WaterColours(Bottom: new Rgba(0f, 0f, 1f, 1f)), meadows.Water);
        Assert.Empty(notes);
    }
}
