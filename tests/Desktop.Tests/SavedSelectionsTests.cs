using Xunit;

using static TerrainEditor.Desktop.Tests.SavedSelectionsData;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Saved selections (per world, found again by kind and place after the ids changed): the store's edge cases, and what
/// the store keeps for each world.
/// </summary>
[Collection("Saved selections")]
public sealed class SavedSelectionsTests : SavedSelectionsFolder
{
    [Fact]
    public void SelectionsAreKeptPerWorldAndReplacedByName()
    {
        Assert.Empty(SavedSelections.For("A"));
        Assert.True(SavedSelections.Keep("A", "trees", [new SavedSelections.Item(Beech, 1.23456f, 2, 3)]));
        Assert.True(SavedSelections.Keep("A", "walls", [new SavedSelections.Item(Wall, 4, 5, 6)]));
        Assert.True(SavedSelections.Keep("B", "trees", []));
        Assert.Equal(["trees", "walls"], SavedSelections.For("A").Select(s => s.Name));
        // Rounded to the millimetre.
        Assert.Equal(1.235f, SavedSelections.For("A")[0].Items[0].X, 4);
        // The same name again: replaced, and now the last.
        SavedSelections.Keep("A", "trees", [new SavedSelections.Item(Beech, 9, 9, 9), new SavedSelections.Item(Beech, 8, 8, 8)]);
        var a = SavedSelections.For("A");
        Assert.Equal(["walls", "trees"], a.Select(s => s.Name));
        Assert.Equal(2, a[1].Items.Count);
        Assert.Single(SavedSelections.For("B"));
        // Forgetting: only that world's, and the world goes when it has none left.
        Assert.True(SavedSelections.Forget("B", "trees"));
        Assert.Empty(SavedSelections.For("B"));
        Assert.DoesNotContain("\"B\"", File.ReadAllText(SavedSelections.PathOverride!));
        Assert.True(SavedSelections.Forget("A", "no such selection"));
        Assert.True(SavedSelections.Forget("nowhere", "trees"));
        Assert.Equal(2, SavedSelections.For("A").Count);
    }

    [Fact]
    public void ABrokenOrUnwritableFileMeansNoSelections()
    {
        File.WriteAllText(SavedSelections.PathOverride!, "{ not json");
        Assert.Empty(SavedSelections.For("A"));
        File.WriteAllText(SavedSelections.PathOverride!, "{\"A\": null, \"B\": [null, {\"Name\": \"x\", \"Items\": null}, {\"Name\": \"ok\", \"Items\": []}]}");
        Assert.Empty(SavedSelections.For("A"));
        Assert.Equal("ok", Assert.Single(SavedSelections.For("B")).Name);
        File.WriteAllText(SavedSelections.PathOverride!, "[1, 2]");
        Assert.Empty(SavedSelections.For("A"));
        // A folder in the file's place: nothing written, said so.
        File.Delete(SavedSelections.PathOverride!);
        Directory.CreateDirectory(SavedSelections.PathOverride!);
        Assert.False(SavedSelections.Keep("A", "x", [new SavedSelections.Item(Beech, 0, 0, 0)]));
        Assert.Empty(SavedSelections.For("A"));
    }

    [Fact]
    public void ABrokenFileIsKeptAsideBeforeItIsWrittenOver()
    {
        // A broken file counted as none, and the next Keep or Forget wrote over it: every world's selections lost. It
        // is copied aside as selections.json.bad first; when that cannot be done, nothing is written over it.
        string path = SavedSelections.PathOverride!;
        Directory.CreateDirectory(path + ".bad");
        File.WriteAllText(path, "{ not json");
        Assert.False(SavedSelections.Keep("A", "x", [new SavedSelections.Item(Beech, 0, 0, 0)]));
        Assert.False(SavedSelections.Forget("A", "x"));
        Assert.Equal("{ not json", File.ReadAllText(path));
        Directory.Delete(path + ".bad");
        Assert.True(SavedSelections.Keep("A", "x", [new SavedSelections.Item(Beech, 0, 0, 0)]));
        Assert.Equal("{ not json", File.ReadAllText(path + ".bad"));
        Assert.Equal("x", Assert.Single(SavedSelections.For("A")).Name);
        // Written through a temporary file, none left behind.
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void FindingMatchesKindAndPlaceWithinTolerances()
    {
        var things = new List<WorldScene.Thing>
        {
            T(1, Beech, 10, 30, 10),
            T(2, Beech, 10.04f, 30.19f, 9.96f),  // within 5 cm across and 20 cm up of the second item
			T(3, Wall, 20, 30, 20),
            T(4, Beech, 50, 30, 50, gone: true),
            T(5, Beech, 60.06f, 30, 60),         // 6 cm off: not it
		};
        var (ids, missing) = SavedSelections.Find(S(
            I(Beech, 10, 30, 10), I(Beech, 10, 30, 10), I(Wall, 20, 30.25f, 20), I(Beech, 50, 30, 50), I(Beech, 60, 30, 60), I(Wall, 10, 30, 10)), things);
        // Two items at the same place take two different objects; the wall is 25 cm too high, the
        // deleted one and the one 6 cm away are not found, nor a wall where a tree stands.
        Assert.Equal(new[] { 0, 1 }, ids);
        Assert.Equal(4, missing);
        Assert.Empty(SavedSelections.Find(S(), things).Ids);
        Assert.Equal(0, SavedSelections.Find(S(), things).Missing);
        Assert.Equal(1, SavedSelections.Find(S(I(Beech, 0, 0, 0)), []).Missing);
    }

    [Fact]
    public void FindingAcrossMetreCellsTakesTheFirstMatchingThing()
    {
        // Both within 5 cm of the items but on either side of a metre line: the later object is in the items' own
        // cell, the earlier one in the next cell, and the earlier one is still taken first.
        var things = new List<WorldScene.Thing>
        {
            T(1, Beech, 1.02f, 30, 5),
            T(2, Beech, 0.97f, 30, 5),
            T(3, Beech, 0.99f, 30, -0.03f),
        };
        var (ids, missing) = SavedSelections.Find(S(I(Beech, 0.99f, 30, 5), I(Beech, 0.99f, 30, 5), I(Beech, 0.99f, 30, 5), I(Beech, 1, 30, 0.01f)), things);
        Assert.Equal(new[] { 0, 1, 2 }, ids);
        Assert.Equal(1, missing);
    }
}
