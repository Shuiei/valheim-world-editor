using TerrainEditor.App;
using ValheimGen;
using Xunit;
using static WorldEditor.Tests.ExpandWorldEntries;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data's location, vegetation and alt biome files as the editor converts them: the location kinds (clones,
/// blueprints, groups and distance rules, Expand World Size's multiplier), the vegetation rules (several prefabs, a
/// scale by axis, a centre of their own, the world's global keys), the alt biomes, and the rules a folder with the files
/// gives.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldPlacesTests : AppSettingsTestBase
{
    /// <summary>An entry from its fields (a list value for a field given several times).</summary>
    /// <summary>The game's location kinds the tests convert against (the fixture's prioritized ones).</summary>
    private static List<SeedLocations.Rule> Game()
    {
        Fixtures.UseLocations();
        return SeedLocations.Current.Locations;
    }

    [Fact]
    public void ALocationKindTakesItsPrefabsRadiiAndItsOwnSettings()
    {
        List<string> notes = [];
        var game = Game();
        var temple = game.First(r => r.Prefab == "StartTemple");
        var rules = ExpandWorld.LocationsOf(
        [
            E(("prefab", "StartTemple"), ("quantity", "2"), ("prioritized", "true"), ("centerFirst", "yes"), ("minDistance", "0.1"), ("maxDistance", "0.5"), ("biome", "meadows, swamp"), ("biomeArea", "edge"), ("enabled", "false"), ("groups", "a, b"), ("group", "c")),
            E(("prefab", "StartTemple:Copy"), ("exteriorRadius", "40"), ("interiorRadius", "3"), ("maxAltitude", "10000"), ("objectSwap", new List<object?> { "x:y" })),
            E(("prefab", "NoSuchLocation"), ("quantity", "1")),
            E(("prefab", "")),
        ], game, BiomeTable.Vanilla, 20000f, null, notes);
        Assert.Equal(2, rules.Count);
        var first = rules[0];
        Assert.Equal("StartTemple", first.Prefab);
        Assert.Null(first.BasePrefab);
        Assert.Equal("StartTemple", first.Base);
        Assert.Equal(temple.ExteriorRadius, first.ExteriorRadius);
        Assert.Equal(temple.InteriorRadius, first.InteriorRadius);
        Assert.Equal(2, first.Quantity);
        Assert.Equal(1, first.Prioritized);
        Assert.Equal(1, first.CenterFirst);
        Assert.Equal(0, first.Enable);
        Assert.Equal(2000f, first.MinDistance);
        Assert.Equal(10000f, first.MaxDistance);
        Assert.Equal((int)(Heightmap.Biome.Meadows | Heightmap.Biome.Swamp), first.Biome);
        Assert.Equal((int)Heightmap.BiomeArea.Edge, first.BiomeArea);
        Assert.Equal(["a", "b", "c"], first.Groups);
        Assert.Null(first.Custom);
        Assert.Null(first.Blueprint);
        var clone = rules[1];
        Assert.Equal("StartTemple:Copy", clone.Prefab);
        Assert.Equal("StartTemple", clone.BasePrefab);
        Assert.Equal(40f, clone.ExteriorRadius);
        Assert.Equal(3f, clone.InteriorRadius);
        Assert.Equal((int)Heightmap.Biome.All, clone.Biome);
        Assert.Equal((int)Heightmap.BiomeArea.Everything, clone.BiomeArea);
        Assert.Equal(10000f, clone.MaxAltitude);
        Assert.Equal(1, clone.Enable);
        Assert.True(clone.Custom);
        Assert.Contains(notes, n => n.Contains("NoSuchLocation"));
    }

    [Fact]
    public void AKindClearsItsAreaAsTheFileSaysOnlyWithARadiusOfItsOwn()
    {
        List<string> notes = [];
        var rules = ExpandWorld.LocationsOf(
        [
            E(("prefab", "StartTemple"), ("clearArea", "false")),
            E(("prefab", "StartTemple:Copy"), ("exteriorRadius", "40")),
            E(("prefab", "StartTemple:Kept"), ("exteriorRadius", "40"), ("clearArea", "true")),
        ], Game(), BiomeTable.Vanilla, 10000f, null, notes);
        Assert.Equal(1, Game().First(r => r.Prefab == "StartTemple").ClearArea);
        Assert.Equal([1, 0, 1], rules.Select(r => r.ClearArea));
    }

    [Fact]
    public void DistanceRulesMakeAMadeUpGroupAsTheModDoes()
    {
        List<string> notes = [];
        var rules = ExpandWorld.LocationsOf(
        [
            E(("prefab", "StartTemple"), ("group", "temples"), ("minDistanceFromSimilar", "300"), ("awayFrom", "Crypt2, dungeons, 500, Vendor_BlackForest, 1000")),
            E(("prefab", "StartTemple"), ("closeTo", new List<object?> { "Crypt2, 200", "Vendor_BlackForest, hidden, 800" })),
            E(("prefab", "StartTemple"), ("awayFrom", "500, Crypt2")),
        ], Game(), BiomeTable.Vanilla, 10000f, null, notes);
        var away = rules[0];
        Assert.Equal("_1", away.Group);
        Assert.Equal(1f, away.MinDistanceFromSimilar);
        Assert.Equal(["temples"], away.Groups);
        Assert.Equal([new SeedLocations.DistanceRule("Crypt2", 500f), new SeedLocations.DistanceRule("dungeons", 500f), new SeedLocations.DistanceRule("Vendor_BlackForest", 1000f)], away.AwayFrom);
        Assert.Null(away.CloseTo);
        var close = rules[1];
        Assert.Equal("_2", close.GroupMax);
        Assert.Equal(1f, close.MaxDistanceFromSimilar);
        Assert.Equal(2, close.CloseTo!.Count);
        Assert.Equal([new SeedLocations.DistanceRule("Vendor_BlackForest", 800f), new SeedLocations.DistanceRule("hidden", 800f)], close.CloseTo[1]);
        // A distance before any name, and names without a distance, are noted and left out.
        Assert.Null(rules[2].AwayFrom);
        Assert.Equal("", rules[2].Group);
        Assert.Contains(notes, n => n.Contains("has no names"));
        Assert.Contains(notes, n => n.Contains("no distance for Crypt2"));
    }

    [Fact]
    public void ABlueprintLocationTakesItsRadiusFromItsPieces()
    {
        string folder = Path.Combine(Data, "PlanBuild");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "MyHut.vbuild"), "woodwall 0 0 0 1 10 0 -10\nwood_floor 0 0 0 1 -10 3 10\n");
        List<string> notes = [];
        Assert.Equal(MathF.Sqrt(200f), ExpandWorld.BlueprintRadius(folder, "MyHut", notes)!.Value, 3);
        Assert.Null(ExpandWorld.BlueprintRadius(folder, "NoHut", notes));
        Assert.Null(ExpandWorld.BlueprintRadius(null, "MyHut", notes));
        var rules = ExpandWorld.LocationsOf(
        [
            E(("prefab", "MyHut"), ("quantity", "3"), ("minDistanceFromSimilar", "400"), ("group", "huts")),
            E(("prefab", "MyHut"), ("exteriorRadius", "12")),
        ], Game(), BiomeTable.Vanilla, 10000f, folder, notes);
        Assert.Equal(2, rules.Count);
        Assert.True(rules[0].Blueprint);
        Assert.Equal(MathF.Sqrt(200f) + 5f, rules[0].ExteriorRadius, 3);
        // Blueprints share no prefab: the mod keeps them apart by name and group through a made-up group.
        Assert.Equal([new SeedLocations.DistanceRule("MyHut", 400f), new SeedLocations.DistanceRule("huts", 400f)], rules[0].AwayFrom);
        Assert.Equal("_1", rules[0].Group);
        Assert.Equal(12f, rules[1].ExteriorRadius);
        Assert.Empty(notes);
    }

    [Fact]
    public void ExpandWorldSizeMultipliesTheQuantitiesAndScalesTheDistances()
    {
        var game = Game();
        var same = ExpandWorld.SizedLocations(game, new WorldRules { SizeInstalled = true, Radius = 20000f, DataInstalled = true });
        Assert.Equal(game.Select(r => r.Quantity), same.Select(r => r.Quantity));
        var more = ExpandWorld.SizedLocations(game, new WorldRules { LocationsMultiplier = 1.5f, DataInstalled = true });
        foreach (var (before, after) in game.Zip(more))
        {
            int expected = before.Prefab == "StartTemple" || before.Quantity == 0 || before.Enable == 0 ? before.Quantity : Math.Max(1, (int)Math.Round(before.Quantity * 1.5, MidpointRounding.ToEven));
            Assert.Equal(expected, after.Quantity);
            Assert.Equal(before.MinDistance, after.MinDistance);
        }
        Assert.Contains(more, r => r.Quantity != game.First(g => g.Prefab == r.Prefab).Quantity);
        var none = ExpandWorld.SizedLocations(game, new WorldRules { LocationsMultiplier = 0f });
        Assert.All(none.Where(r => r.Prefab != "StartTemple"), r => Assert.Equal(0, r.Quantity));
        // Without Expand World Data, Size scales the distances with the radius.
        var wide = ExpandWorld.SizedLocations(game, new WorldRules { SizeInstalled = true, Radius = 20000f });
        foreach (var (before, after) in game.Zip(wide))
        {
            Assert.Equal(before.MinDistance * 2f, after.MinDistance);
            Assert.Equal(before.MaxDistance * 2f, after.MaxDistance);
        }
        // The originals are left as they are.
        Assert.Equal(game.Select(r => r.Quantity), Game().Select(r => r.Quantity));
    }

    [Fact]
    public void AVegetationRuleFollowsTheFile()
    {
        List<string> notes = [];
        var game = Regrow.GameRules;
        Assert.True(game.Count > 0);
        string known = game[0].prefab, known2 = game.Skip(1).First(r => r.draws != game[0].draws).prefab;
        var rules = ExpandWorld.VegetationOf(
        [
            E(("prefab", $"{known}, {known2}, Nowhere"), ("min", "2"), ("max", "5"), ("forcePlacement", "true"), ("scaleMin", "0.5"), ("scaleMax", "2"), ("scaleUniform", "false"),
              ("biome", "meadows"), ("biomeArea", "median"), ("minDistance", "0.1"), ("maxDistance", "0.5"), ("groupSizeMin", "2"), ("groupSizeMax", "4"), ("maxAltitude", "50")),
            E(("prefab", known), ("centerX", "0.2"), ("centerY", "-0.1"), ("minDistance", "0.05"), ("scaleMin", "1,2,3"), ("scaleMax", "1,2,3"), ("clearArea", "true"), ("clearRadius", "3"),
              ("surroundCheckVegetation", "true"), ("surroundCheckDistance", "25"), ("surroundCheckLayers", "3"), ("surroundBetterThanAverage", "0.5")),
            E(("prefab", known), ("altBiome", "DarkMeadows")),
            E(("prefab", "")),
        ], game, BiomeTable.Vanilla, 20000f, new HashSet<string>(), notes);
        Assert.Equal(3, rules.Count);
        var several = rules[0];
        Assert.Equal(known, several.prefab);
        Assert.Equal([known, known2, "Nowhere"], several.prefabs!);
        Assert.Equal([game[0].draws, game.First(r => r.prefab == known2).draws, 0], several.prefabDraws!);
        Assert.Equal(game[0].draws, several.draws);
        Assert.Equal(2f, several.min);
        Assert.Equal(5f, several.max);
        Assert.Equal(1, several.forcePlacement);
        Assert.Equal(0.5f, several.scaleMin);
        Assert.Equal(2f, several.scaleMax);
        Assert.Equal([0.5f, 0.5f, 0.5f], several.scaleMinAxes!);
        Assert.Equal([2f, 2f, 2f], several.scaleMaxAxes!);
        Assert.Equal(0, several.scaleUniform);
        Assert.Equal((int)Heightmap.Biome.Meadows, several.biome);
        Assert.Equal((int)Heightmap.BiomeArea.Median, several.biomeArea);
        Assert.Equal(2000f, several.minDistanceFromCenter);
        Assert.Equal(10000f, several.maxDistanceFromCenter);
        Assert.Null(several.center);
        Assert.Equal(2, several.groupSizeMin);
        Assert.Equal(4, several.groupSizeMax);
        Assert.Equal(50f, several.maxAltitude);
        Assert.Equal(0f, several.minAltitude);
        Assert.Equal(1, several.enable);
        Assert.Null(several.altBiome);
        Assert.Equal((0, 0f), (several.clearArea, several.clearRadius));
        // A centre of its own: the mod's check in place of the game's; a scale the same at both ends is no scale by axis.
        var centred = rules[1];
        Assert.Null(centred.prefabs);
        Assert.Equal(0f, centred.minDistanceFromCenter);
        Assert.Equal([4000f, -2000f], centred.center!);
        Assert.Equal([1000f, 0f], centred.distance!);
        Assert.Null(centred.scaleMinAxes);
        Assert.Equal(1f, centred.scaleMin);
        Assert.Equal((1, 3f), (centred.clearArea, centred.clearRadius));
        Assert.Equal((1, 25f, 3, 0.5f), (centred.surroundCheckVegetation, centred.surroundCheckDistance, centred.surroundCheckLayers, centred.surroundBetterThanAverage));
        Assert.Equal((0, 20f, 2), (several.surroundCheckVegetation, several.surroundCheckDistance, several.surroundCheckLayers));
        Assert.Contains(notes, n => n.Contains("Nowhere"));
        // A rule of an alt biome is kept, to grow in that alt biome's areas.
        Assert.Equal("DarkMeadows", rules[2].altBiome);
        Assert.DoesNotContain(notes, n => n.Contains("alt biomes"));
    }

    [Fact]
    public void TheWorldsGlobalKeysTurnVegetationRulesOnAndOff()
    {
        List<string> notes = [];
        var game = Regrow.GameRules;
        string known = game[0].prefab;
        var keys = new HashSet<string> { "defeated_eikthyr", "defeated_gdking" };
        var rules = ExpandWorld.VegetationOf(
        [
            E(("prefab", known), ("requiredGlobalKey", "defeated_eikthyr")),
            E(("prefab", known), ("requiredGlobalKey", new List<object?> { "defeated_eikthyr", "defeated_bonemass, DEFEATED_GDKING" })),
            E(("prefab", known), ("requiredGlobalKey", "defeated_bonemass")),
            E(("prefab", known), ("forbiddenGlobalKey", "defeated_eikthyr")),
            E(("prefab", known), ("forbiddenGlobalKey", "defeated_bonemass")),
            E(("prefab", known), ("enabled", "false"), ("forbiddenGlobalKey", "defeated_bonemass")),
        ], game, BiomeTable.Vanilla, 10000f, keys, notes);
        Assert.Equal([1, 1, 0, 0, 1, 0], rules.Select(r => r.enable));
    }

    [Fact]
    public void TheGlobalKeyFieldsAreReadByTheirNamesSinceData179()
    {
        List<string> notes = [];
        var game = Regrow.GameRules;
        string known = game[0].prefab;
        var keys = new HashSet<string> { "defeated_eikthyr" };
        var rules = ExpandWorld.VegetationOf(
        [
            E(("prefab", known), ("requiredGlobalKeys", "defeated_bonemass")),
            E(("prefab", known), ("requiredGlobalKeys", "defeated_eikthyr")),
            E(("prefab", known), ("forbiddenGlobalKeys", "defeated_eikthyr")),
            // The new name wins over the old one when a file has both.
            E(("prefab", known), ("requiredGlobalKeys", "defeated_eikthyr"), ("requiredGlobalKey", "defeated_bonemass")),
        ], game, BiomeTable.Vanilla, 10000f, keys, notes);
        Assert.Equal([0, 1, 0, 1], rules.Select(r => r.enable));
    }

    [Fact]
    public void AScaleIsReadAsTheModReadsOne()
    {
        Assert.Equal([1f, 1f, 1f], ExpandWorld.ScaleOf("0"));
        Assert.Equal([2f, 2f, 2f], ExpandWorld.ScaleOf("2"));
        // The mod writes x, z, y; the game's order is x, y, z.
        Assert.Equal([1f, 3f, 2f], ExpandWorld.ScaleOf("1, 2, 3"));
        Assert.Equal([1.5f, 1.5f, 1.5f], ExpandWorld.ScaleOf("1.5, abc"));
    }

    [Fact]
    public void AltBiomesKeepEveryFieldTheLayoutReads()
    {
        List<string> notes = [];
        var alts = ExpandWorld.AltBiomesOf(
        [
            E(("name", "DarkMeadows"), ("biome", "Meadows, BlackForest"), ("chance", "0.5"), ("minAmountSpawned", "2"), ("maxAmountSpawned", "5"),
              ("minDistanceFromCenter", "1900"), ("minEdgeSize", "66"), ("maxEdgeSize", "299"), ("minAvgHeight", "30"), ("maxAvgHeight", "10000"),
              ("aboveWorldX", "-100"), ("belowWorldY", "250"), ("requireNeighbor", "Swamp"), ("notNeighbor", "Ocean"),
              ("incompatibleAltBiomes", new List<object?> { "Bones" }), ("blockVegetationNames", "beech1_meadows"),
              ("blockLocationNames", new List<object?> { "Crypt2", "StartTemple:Copy" }), ("terrainTextureOverride", "AshLands")),
            E(("name", "Off"), ("enabled", "false")),
            E(("name", "Plain"), ("biome", "Nowhere")),
        ], BiomeTable.Vanilla, notes);
        Assert.Equal(["DarkMeadows", "Off", "Plain"], alts.Select(a => a.Name));
        var dark = alts[0];
        Assert.Equal((int)(Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest), dark.Biome);
        Assert.Equal((0.5f, 2, 5, 1900f), (dark.Chance, dark.MinAmountSpawned, dark.MaxAmountSpawned, dark.MinDistanceFromCenter));
        Assert.Equal((66, 299, 30f, 10000f), (dark.MinEdgeSize, dark.MaxEdgeSize, dark.MinAvgHeight, dark.MaxAvgHeight));
        Assert.Equal((-100f, 0f, 250f, 0f), (dark.AboveWorldX, dark.BelowWorldX, dark.BelowWorldY, dark.AboveWorldY));
        Assert.Equal(((int)Heightmap.Biome.Swamp, (int)Heightmap.Biome.Ocean), (dark.RequireNeighbor, dark.NotNeighbor));
        Assert.Equal(["Bones"], dark.IncompatibleAltBiomes);
        Assert.Equal(["beech1_meadows"], dark.BlockVegetationNames);
        Assert.Equal(["Crypt2", "StartTemple:Copy"], dark.BlockLocationNames);
        Assert.Equal((int)Heightmap.Biome.AshLands, dark.TerrainTextureOverride);
        // A field left out is 0 or none (the mod's, not the game's defaults); "enabled" is kept as written.
        Assert.False(alts[1].Enabled);
        var plain = alts[2];
        Assert.Equal((0f, 0, 0, 0, 0, 0), (plain.Chance, plain.MaxAmountSpawned, plain.MaxEdgeSize, plain.RequireNeighbor, plain.Biome, plain.TerrainTextureOverride));
        Assert.Contains(notes, n => n.Contains("Nowhere"));
    }

    [Fact]
    public void AFolderWithTheFilesGivesRulesWithThePlaces()
    {
        Game();
        string bep = Path.Combine(Data, "bep");
        string data = Path.Combine(bep, "config", "expand_world");
        Directory.CreateDirectory(data);
        YamlStandIn.Install(bep);
        TestMods.Install(bep, TestMods.Data);
        File.WriteAllText(Path.Combine(bep, "config", "expand_world_data.cfg"), "[1. General]\nZone spawners = false\nRandom locations = true\n[3. Data]\nLocation data = true\nVegetation data = true\nAlt biome data = true\n");
        File.WriteAllText(Path.Combine(data, "expand_locations.yaml"), "- prefab: StartTemple:Copy\n  quantity: 2\n- prefab: StartTemple\n  quantity: 1\n");
        File.WriteAllText(Path.Combine(data, "expand_vegetation.yaml"), $"- prefab: {Regrow.GameRules[0].prefab}\n  requiredGlobalKeys: defeated_eikthyr\n");
        File.WriteAllText(Path.Combine(data, "expand_altbiomes.yaml"), "- name: DarkMeadows\n  blockLocationNames:\n  - StartTemple\n");
        var found = ExpandWorld.Look(bep, "test");
        var rules = ExpandWorld.Rules(new Dictionary<string, string>(), found, ExpandWorld.ReadData(found), ["defeated_eikthyr 1"]);
        Assert.NotNull(rules.Locations);
        Assert.Equal(["StartTemple:Copy", "StartTemple"], rules.Locations.Locations.Select(r => r.Prefab));
        Assert.Equal("DarkMeadows", Assert.Single(rules.Locations.AltBiomes).Name);
        Assert.Equal(1, Assert.Single(rules.Vegetation!).enable);
        Assert.False(rules.ZoneSpawners);
        Assert.True(rules.RandomLocations);
        string text = ExpandWorld.Describe(rules);
        Assert.Contains("locations (2 kinds)", text);
        Assert.Contains("vegetation (1 rules)", text);
        Assert.Contains("no zone spawners", text);
        Assert.Contains("random locations", text);
        // The rules tell generators apart by their places too.
        Assert.NotEqual(rules.Stamp, (rules with { Vegetation = null }).Stamp);
        // The layout takes the world's own kinds, the clone first. The file's alt biome, with no field saying where it
        // goes, is given to no area (as the mod reads it), so it blocks the other kind nowhere.
        var gen = WorldGenerator.Create(new World { Seed = 1, WorldGenVersion = 2, Rules = rules });
        Assert.True(SeedLocations.AvailableFor(gen));
        var placed = SeedLocations.Place(gen, SeedLocations.Which.All, cancel: TestContext.Current.CancellationToken);
        Assert.Contains(placed, p => p.Rule.Prefab == "StartTemple:Copy");
        Assert.Contains(placed, p => p.Rule.Prefab == "StartTemple");
    }

    [Fact]
    public void AKindsIconsAreTheFilesWords()
    {
        var rules = ExpandWorld.LocationsOf([E(("prefab", "StartTemple"), ("iconAlways", "true"), ("iconPlaced", "Hildir_camp, 2")), E(("prefab", "StartTemple:Copy"))], Game(), BiomeTable.Vanilla, 10000f, null, []);
        Assert.Equal(("true", "Hildir_camp, 2"), (rules[0].IconAlwaysName, rules[0].IconPlacedName));
        // Not given: no icon (the mod's default), not the game's.
        Assert.Equal(("", ""), (rules[1].IconAlwaysName, rules[1].IconPlacedName));
    }
}
