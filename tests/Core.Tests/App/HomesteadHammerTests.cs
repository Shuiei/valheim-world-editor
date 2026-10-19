using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What Homestead's hammer builds of a blueprint (Homestead.HammerPlaces, from the game's data: a WearNTear, a cost, a
/// build tool), the marks the editor writes for the rest, the library's count of them (none for a file without marks)
/// and a blueprint's cost without them.
/// </summary>
public sealed class HomesteadHammerTests : IDisposable
{
    /// <summary>A hammer piece with a WearNTear and a cost in the fixtures.</summary>
    private const string Wall = "stone_wall_4x2";
    /// <summary>A world prop no catalogue of pieces has.</summary>
    private const string Prop = "StatueFreya";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-hh-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Deletes the temporary folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>A copy holding one object of each name, side by side on the ground.</summary>
    private static JsonObject Clip(params string[] names) =>
        BlueprintFormats.OneCellClip([.. names.Select((n, i) => (JsonNode)new JsonObject { ["name"] = n, ["dx"] = i, ["dy"] = 0, ["dz"] = 0, ["rx"] = 0, ["ry"] = 0, ["rz"] = 0, ["scale"] = 0 })], []);

    [Fact]
    public void APieceWithAWearNTearACostAndAToolIsTheHammers()
    {
        Assert.True(Homestead.HammerPlaces(Wall));
        Assert.True(Homestead.HammerPlaces("piece_workbench"));
    }

    // The T.W.I.G. is in the hammer's menu with a cost, but has no WearNTear (it is a creature).
    [Fact]
    public void APieceWithoutAWearNTearIsNot()
    {
        Assert.NotNull(PieceCost.Get("piece_TrainingDummy"));
        Assert.False(Homestead.HammerPlaces("piece_TrainingDummy"));
    }

    // A dungeon's chest: a WearNTear and a recipe, but in no build tool's menu.
    [Fact]
    public void APieceInNoBuildToolsMenuIsNot()
    {
        Assert.True(TerrainEditor.Editing.Stability.Known("TreasureChest_swamp"));
        Assert.False(Homestead.HammerPlaces("TreasureChest_swamp"));
    }

    [Fact]
    public void APieceWithoutACostIsNot()
    {
        Assert.NotNull(PieceCatalog.Get(TerrainEditor.Save.StableHash.Of("wood_floor_1x1")));
        Assert.Null(PieceCost.Get("wood_floor_1x1"));
        Assert.False(Homestead.HammerPlaces("wood_floor_1x1"));
        Assert.False(Homestead.HammerPlaces(Prop));
    }

    [Fact]
    public void TheWriterMarksAPropAndNotAWall()
    {
        string text = Homestead.Write(Clip(Wall, Prop, "Boar"), "Shrine", "Tester", null, DateTime.Now);
        Assert.Contains("\n" + BlueprintFormats.MarkedHeader + "\n", text);
        Assert.DoesNotContain("#EditorData:0;", text);
        Assert.Contains("\n#EditorData:1;editor;only;1\n", text);
        Assert.Contains("\n#EditorData:2;editor;only;1\n", text);
        // The marks are header lines: the pieces read as before, and the mark is no object field.
        var parsed = BlueprintFormats.Parse("Shrine.blueprint", text);
        Assert.Equal([Wall, Prop, "Boar"], parsed.Pieces.Select(p => p.Name));
        Assert.All(parsed.Pieces, p => Assert.True(p.Data is null or { Count: 0 }));
    }

    [Fact]
    public void TheLibraryCountsTheMarkedPiecesAndCostsTheRest()
    {
        var entry = Homestead.Read("Shrine.blueprint", Homestead.Write(Clip(Wall, Prop, Wall, "Boar"), "Shrine", "Tester", null, DateTime.Now).Split('\n'));
        Assert.Equal(4, entry.Pieces);
        Assert.Equal(2, entry.EditorOnly);
        Assert.Equal(new Dictionary<string, int> { [Wall] = 2 }, entry.Kinds);
        Assert.Equal("2 of 4 piece(s) only the editor can place", entry.EditorOnlyNote?.English);
    }

    // Marks with no piece at their line (a file cut short or edited by hand) are not counted: only the marks the
    // pieces have, which are also the ones left out of the cost.
    [Fact]
    public void AMarkPastTheLastPieceIsNotCounted()
    {
        var entry = Homestead.Read("Cut.blueprint", ["#Name:Cut", BlueprintFormats.MarkedHeader, $"{BlueprintFormats.DataHeader}1;{BlueprintFormats.OnlyMark}",
            $"{BlueprintFormats.DataHeader}2;{BlueprintFormats.OnlyMark}", $"{BlueprintFormats.DataHeader}9;{BlueprintFormats.OnlyMark}",
            "#Pieces", $"{Wall};Building;0;0;0;0;0;0;1;\"\";1;1;1", $"{Prop};Building;1;0;0;0;0;0;1;\"\";1;1;1"]);
        Assert.Equal(2, entry.Pieces);
        Assert.Equal(1, entry.EditorOnly);
        Assert.Equal(new Dictionary<string, int> { [Wall] = 1 }, entry.Kinds);
        Assert.Equal("1 of 2 piece(s) only the editor can place", entry.EditorOnlyNote?.English);
    }

    // An older editor's file, or Homestead's own: no marks, so nothing is said rather than a wrong zero.
    [Fact]
    public void AFileWithoutMarksDoesNotKnow()
    {
        var entry = Homestead.Read("Old.blueprint", ["#Name:Old", "#Pieces", $"{Wall};Building;0;0;0;0;0;0;1;\"\";1;1;1", $"{Prop};Building;1;0;0;0;0;0;1;\"\";1;1;1"]);
        Assert.Null(entry.EditorOnly);
        Assert.Null(entry.EditorOnlyNote);
        Assert.Equal(2, entry.Pieces);
        Assert.Equal(new Dictionary<string, int> { [Wall] = 1, [Prop] = 1 }, entry.Kinds);
    }

    [Fact]
    public void ABlueprintWithAWallAndAPropCostsOnlyTheWall()
    {
        var kinds = new Dictionary<string, int> { [Wall] = 3, [Prop] = 1 };
        var cost = Homestead.Cost(kinds);
        Assert.Equal(PieceCost.Of([KeyValuePair.Create(Wall, 3)]).Materials, cost.Materials);
        Assert.Empty(cost.Unknown);
        // Without the rule, the prop would count as a piece without a recipe.
        Assert.Contains(Prop, PieceCost.Of(kinds).Unknown.Keys);
        Assert.Equal(cost.Materials, Homestead.Cost([Wall, Prop, Wall, Wall]).Materials);
    }

    // The marks in the file are not trusted on a save: the game's data decides again.
    [Fact]
    public void SavingAgainWorksTheMarksOutAgain()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "Shrine.blueprint");
        File.WriteAllText(path, $"#Name:Shrine\n{BlueprintFormats.MarkedHeader}\n#EditorData:0;editor;only;1\n#Pieces\n{Wall};Building;0;0;0;0;0;0;1;\"\";1;1;1\n");
        Assert.Equal(1, Homestead.Read(path)!.EditorOnly);
        var parsed = BlueprintFormats.Parse(path, File.ReadAllText(path));
        string written = Homestead.Write(BlueprintFormats.OneCellClip(BlueprintFormats.PieceObjects(parsed.Pieces, 0, 0, 0), []), "Shrine", "Tester", null, DateTime.Now);
        Assert.Equal(0, Homestead.Read(path, written.Split('\n')).EditorOnly);
    }
}
