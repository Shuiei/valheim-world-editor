using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using static WorldEditor.Tests.ExpandWorldEntries;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data's blueprint files as the editor reads them: PlanBuild and BuildShare rows, the centre the mod puts
/// them on, the blueprints they nest, the file a name finds, and what cannot be made.
/// </summary>
[Collection("DataDir")]
public class ExpandWorldBlueprintsTests : AppSettingsTestBase
{
    /// <summary>The data entries the blueprints name.</summary>
    private static readonly ExpandWorld.DataTable Entries = new([E(("name", "marked"), ("ints", "mark, 1")), E(("name", "rolled"), ("ints", "mark, 1;3"))]);

    /// <summary>A blueprint file in the test's folder (under a sub-folder: the mod looks at any depth).</summary>
    private string Write(string file, string text)
    {
        string folder = Path.Combine(Data, "PlanBuild", "sub");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, file), text);
        return Path.Combine(Data, "PlanBuild");
    }

    [Fact]
    public void APlanBuildBlueprintIsCentredOnItsCentrePiece()
    {
        string folder = Write("Hut.blueprint", "#Name:Hut\n#Pieces\npiece_bpcenterpoint;;10;0;10;0;0.70710677;0;0.70710677\nBush01;Misc;12;1;10;0;0;0;1;;2;2;2;marked;0.5\n");
        List<string> why = [], notes = [];
        var parts = ExpandWorld.BlueprintsOf(folder, "Hut", Entries, why, notes)!["Hut"];
        // The centre piece is never made; the others are turned by its turn taken back.
        Assert.Equal(0f, parts[0].Chance);
        Assert.Equal("Bush01", parts[1].Prefab);
        Assert.True(Vector3.Distance(new Vector3(0, 1, 2), parts[1].Position) < 1e-5f, parts[1].Position.ToString());
        Assert.Equal(new Vector3(2), parts[1].Scale);
        Assert.Equal(0.5f, parts[1].Chance);
        Assert.Equal([new ContentField("ints", StableHash.Of("mark"), "1")], parts[1].Data!);
        Assert.Empty(why);
        Assert.Empty(notes);
    }

    [Fact]
    public void WithoutACentrePieceTheMiddleOfItsBoundsWithTheOriginIsTheCentre()
    {
        string folder = Write("Camp.vbuild", "Bush01 0 0 0 1 10 5 10\nPickable_Stone 0 0 0 1 20 7 30 marked 0.25\n");
        var parts = ExpandWorld.BlueprintsOf(folder, "Camp", Entries, [], [])!["Camp"];
        // Bounds from the origin to (20, 7, 30): the middle (10, 15) on the ground, 5 cm above the lowest piece.
        Assert.True(Vector3.Distance(new Vector3(0, -0.05f, -5), parts[0].Position) < 1e-5f, parts[0].Position.ToString());
        Assert.True(Vector3.Distance(new Vector3(10, 1.95f, 15), parts[1].Position) < 1e-5f, parts[1].Position.ToString());
        Assert.Equal(0.25f, parts[1].Chance);
        Assert.NotNull(parts[1].Data);
    }

    [Fact]
    public void ANameFindsItsFileWithUnderscoresForSpacesAndBringsTheBlueprintsItNests()
    {
        string folder = Write("My Camp.vbuild", "Inner 0 0 0 1 1 0 1\nNothing_Like_It 0 0 0 1 2 0 2\nBush01 0 0 0 1 3 0 3 rolled\n");
        Write("Inner.vbuild", "Bush01 0 0 0 1 0 0 0\n");
        List<string> why = [];
        var found = ExpandWorld.BlueprintsOf(folder, "My_Camp", Entries, why, [])!;
        Assert.Equal(["Inner", "My_Camp"], found.Keys.Order(StringComparer.Ordinal));
        // A part of no prefab, data worked out as the game spawns: said.
        Assert.Contains(why, w => w.Contains("Nothing_Like_It", StringComparison.Ordinal));
        Assert.Contains(why, w => w.Contains("rolled", StringComparison.Ordinal) || w.Contains("1;3", StringComparison.Ordinal));
        Assert.Null(ExpandWorld.BlueprintsOf(folder, "Nowhere", Entries, [], []));
        Assert.Null(ExpandWorld.BlueprintsOf(null, "My_Camp", Entries, [], []));
    }

    [Fact]
    public void AFileWithABadNumberIsNotRead()
    {
        string folder = Write("Broken.vbuild", "Bush01 0 0 0 one 1 1 1\n");
        List<string> notes = [];
        Assert.Null(ExpandWorld.BlueprintsOf(folder, "Broken", Entries, [], notes));
        Assert.Contains(notes, n => n.Contains("Broken.vbuild", StringComparison.Ordinal));
    }

    [Fact]
    public void AVegetationRuleGetsItsDataItsDropsAndItsBlueprint()
    {
        string folder = Write("Hut.vbuild", "Bush01 0 0 0 1 1 0 1\n");
        List<string> notes = [];
        var rules = ExpandWorld.VegetationOf(
        [
            E(("prefab", "Hut"), ("data", "marked"), ("drops", "MyDrops")),
            E(("prefab", "Hut, Bush01")),
            E(("prefab", "Bush01"), ("data", "rolled")),
        ], Regrow.GameRules, BiomeTable.Vanilla, 10000f, new HashSet<string>(), notes, Entries, folder);
        Assert.Equal([new ContentField("ints", StableHash.Of("mark"), "1"), new ContentField("ints", StableHash.Of("ews_drops"), StableHash.Of("MyDrops").ToString(System.Globalization.CultureInfo.InvariantCulture))], rules[0].data!);
        Assert.Equal(["Hut"], rules[0].blueprints!.Keys);
        // The mod looks a list up whole: never a blueprint.
        Assert.Null(rules[1].blueprints);
        // Data the game works out as it spawns is left out, noted.
        Assert.Null(rules[2].data);
        Assert.Contains(notes, n => n.Contains("Bush01 grows without", StringComparison.Ordinal));
        Assert.DoesNotContain(notes, n => n.Contains("no rule for Hut", StringComparison.Ordinal));
    }
}
