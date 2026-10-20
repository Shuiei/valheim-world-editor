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
            E(("prefab", "Bush01"), ("data", "rolled")),
        ], Regrow.GameRules, BiomeTable.Vanilla, 10000f, new HashSet<string>(), notes, Entries, folder);
        Assert.Equal([new ContentField("ints", StableHash.Of("mark"), "1"), new ContentField("ints", StableHash.Of("ews_drops"), StableHash.Of("MyDrops").ToString(System.Globalization.CultureInfo.InvariantCulture))], rules[0].data!);
        Assert.Equal(["Hut"], rules[0].blueprints!.Keys);
        // A blueprint is no prefab the game's list lacks.
        Assert.DoesNotContain(notes, n => n.Contains("no rule for", StringComparison.Ordinal));
        // Data the game works out as it spawns is left out, noted.
        Assert.Null(rules[1].data);
        Assert.Contains(notes, n => n.Contains("Bush01 grows without", StringComparison.Ordinal));
        // The mod looks a list up whole: never a blueprint.
        var list = Assert.Single(ExpandWorld.VegetationOf([E(("prefab", "Hut, Bush01"))], Regrow.GameRules, BiomeTable.Vanilla, 10000f, new HashSet<string>(), notes, Entries, folder));
        Assert.Null(list.blueprints);
    }

    [Fact]
    public void ABlueprintHoldingTheOneItIsInIsLeftOut()
    {
        string folder = Write("Outer.vbuild", "Inner 0 0 0 1 1 0 1\nBush01 0 0 0 1 2 0 2\n");
        Write("Inner.vbuild", "Outer 0 0 0 1 0 0 0\nPickable_Stone 0 0 0 1 3 0 3\n");
        List<string> why = [];
        var found = ExpandWorld.BlueprintsOf(folder, "Outer", Entries, why, [])!;
        // The game would spawn them without end: the inner one keeps its stone, not the outer one.
        Assert.Equal(["Pickable_Stone"], found["Inner"].Select(p => p.Prefab));
        Assert.Equal(["Inner", "Bush01"], found["Outer"].Select(p => p.Prefab));
        Assert.Contains(why, w => w.Contains("Inner holding Outer", StringComparison.Ordinal));
    }

    /// <summary>A height snapshot's rows: <paramref name="side"/> rows of as many nodes, each with a height.</summary>
    private static string Heights(int side) => string.Concat(Enumerable.Repeat(string.Join(';', Enumerable.Repeat("0", side)) + "\n", side));

    [Fact]
    public void ABlueprintLocationsRadiusIsTheModsMeasure()
    {
        // Half the diagonal of the pieces' bounds, which hold the origin: from (0, 0) to (20, 30).
        string folder = Write("My Hut.vbuild", "woodwall 0 0 0 1 10 0 10\nwood_floor 0 0 0 1 20 0 30\n");
        Assert.Equal(MathF.Sqrt(325f), ExpandWorld.BlueprintRadius(folder, "My_Hut", [])!.Value, 3);
        // A height snapshot reaching farther: 21 × 21 nodes a metre apart round the centre, its corners 14.1 m out.
        Write("Plot.blueprint", "#Pieces\npiece_bpcenterpoint;;0;0;0;0;0;0;1\nwoodwall;;1;0;1;0;0;0;1\n#TerrainHeight:0,0,0;0;1\n" + Heights(21));
        Assert.Equal(MathF.Sqrt(200f), ExpandWorld.BlueprintRadius(folder, "Plot", [])!.Value, 3);
        // One reaching beyond what the mod applies (3.5 zones) is dropped: the pieces' radius.
        Write("Field.blueprint", "#Pieces\npiece_bpcenterpoint;;0;0;0;0;0;0;1\nwoodwall;;1;0;1;0;0;0;1\n#TerrainHeight:0,0,0;0;1\n" + Heights(501));
        Assert.Equal(MathF.Sqrt(0.5f), ExpandWorld.BlueprintRadius(folder, "Field", [])!.Value, 3);
    }

    [Fact]
    public void ASnapshotRowOfAnotherWidthFailsTheBlueprint()
    {
        string folder = Write("Bad.blueprint", "#Pieces\nwoodwall;;1;0;1;0;0;0;1\n#TerrainHeight:0,0,0;0;1\n0;0\n0;0;0\n");
        List<string> notes = [];
        Assert.Null(ExpandWorld.BlueprintRadius(folder, "Bad", notes));
        Assert.Contains(notes, n => n.Contains("Bad.blueprint", StringComparison.Ordinal));
    }

    [Fact]
    public void ABlueprintLocationCarriesItsBlueprintAndLevelsByDefault()
    {
        Fixtures.UseLocations();
        string folder = Write("Hut.vbuild", "woodwall 0 0 0 1 4 0 0\nInner 0 0 0 1 0 0 2\n");
        Write("Inner.vbuild", "Bush01 0 0 0 1 0 0 0\n");
        Write("Plot.blueprint", "#Pieces\nwoodwall;;1;0;1;0;0;0;1\n#TerrainHeight:0,0,0;0;1\n0;0\n0;0\n");
        var rules = ExpandWorld.LocationsOf([E(("prefab", "Hut"), ("quantity", "2")), E(("prefab", "Plot"), ("quantity", "2"))], SeedLocations.Current.Locations, BiomeTable.Vanilla, 10000f, folder, [], Entries);
        var hut = rules[0];
        Assert.Equal("Hut", hut.Contents!.Blueprint);
        Assert.Equal(["Hut", "Inner"], hut.Contents.Blueprints!.Keys.Order(StringComparer.Ordinal));
        Assert.Null(hut.Contents.LeftToGame);
        // A blueprint levels its ground unless the file says not to: half its radius fully, half blended.
        Assert.Equal(new LevelArea(0.5f * hut.ExteriorRadius, 0.5f * hut.ExteriorRadius), hut.Contents.Level);
        // One with a height snapshot is built: the snapshot levels its ground, not the blueprint's own levelling.
        var plot = rules[1].Contents!;
        Assert.Null(plot.LeftToGame);
        Assert.Null(plot.Level);
        Assert.NotNull(plot.TerrainHeights);
        Assert.Null(plot.TerrainPaints);
    }

    [Fact]
    public void ASnapshotsValuesAreKeptCentredAndTurnedWithTheBlueprint()
    {
        Fixtures.UseLocations();
        // The centre piece 1 m up and turned a quarter: heights become heights above it, the grid moves and turns with it.
        string folder = Write("Turned.blueprint", "#Pieces\npiece_bpcenterpoint;;2;1;0;0;0.70710677;0;0.70710677\nwoodwall;;3;1;0;0;0;0;1\n"
            + "#TerrainHeight:0,0,0;0;1\n1.5;\n2,5;3\n#TerrainPaint:0,0,0;0;1\n1:0:0;0:1:0:0.5\n");
        var rules = ExpandWorld.LocationsOf([E(("prefab", "Turned"), ("quantity", "1"))], SeedLocations.Current.Locations, BiomeTable.Vanilla, 10000f, folder, [], Entries);
        var heights = rules[0].Contents!.TerrainHeights!;
        Assert.Equal([0.5f, null], heights.Rows[0].Select(v => v?.X));
        Assert.Equal([1.5f, 2f], heights.Rows[1].Select(v => v?.X));
        Assert.Equal(new Vector3(-2, -1, 0), heights.Center);
        // The grid's turn: the blueprint's turn (the centre piece's taken back) taken back again, the centre piece's own.
        Assert.Equal(90f, 2f * MathF.Atan2(heights.Turn.Y, heights.Turn.W) * 180f / MathF.PI, 3);
        // A paint's a is 1 when it is not given.
        Assert.Equal([new System.Numerics.Vector4(1, 0, 0, 1), new System.Numerics.Vector4(0, 1, 0, 0.5f)], rules[0].Contents!.TerrainPaints!.Rows[0]);
    }
}
