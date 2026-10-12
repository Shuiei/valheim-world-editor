using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's tools for the whole world and for copies (ClaudeTools.World): searching the world, its locations, copies
/// in a row or a ring, and a blueprint of the library put into a world's area; and the helpers they use (Known,
/// LocationName).
/// </summary>
[Collection("World files")]
public class ClaudeToolsWorldTests
{
    /// <summary>The id of the first object place_pieces put down.</summary>
    private static int Placed(string json) => ClaudeRun.J(json).GetProperty("placed")[0].GetProperty("id").GetInt32();

    // Known: what players call the start, a trader, a boss's altar and a dungeon's entrance, whatever the case.
    [Fact]
    public void LocationsAreKnownByWhatPlayersCallThem()
    {
        Assert.Equal(("The start", "start"), ClaudeTools.Known["StartTemple"]);
        Assert.Equal(("Haldor", "trader"), ClaudeTools.Known["vendor_blackforest"]);
        Assert.Equal(("The Elder", "boss"), ClaudeTools.Known["GDKing"]);
        Assert.Equal(("Burial chambers", "dungeon"), ClaudeTools.Known["Crypt3"]);
        Assert.False(ClaudeTools.Known.ContainsKey("WoodHouse1"));
    }

    // LocationName: the rules' name first, then the prefab catalogue's; neither: none.
    [Fact]
    public void LocationsAreNamedFromTheRulesFirst()
    {
        var rules = new Dictionary<int, string> { [StableHash.Of("MyPlace")] = "MyPlace" };
        Assert.Equal("MyPlace", ClaudeTools.LocationName(StableHash.Of("MyPlace"), rules));
        Assert.Null(ClaudeTools.LocationName(StableHash.Of("no such location at all"), rules));
    }

    // Without a world: said. A wrong kind of search: said. A tree put down is found by name, with its id in the
    // open area; nearest a point first.
    [AvaloniaFact]
    public async Task TheWorldIsSearched()
    {
        using var r = new ClaudeRun();
        Assert.Equal("Open a world first (open_world).", await r.T.SearchWorld("Beech1"));
        Assert.Equal("Open a world first (open_world).", await r.T.ListLocations());
        await r.OpenArea();
        Assert.Equal("what is kinds, items or texts.", await r.T.SearchWorld("Beech1", "colours"));
        int tree = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = 7, Z = 7 }]));
        var found = ClaudeRun.J(await r.T.SearchWorld("Beech1", x: 7, z: 7));
        Assert.True(found.GetProperty("found").GetInt32() >= 1);
        var nearest = found.GetProperty("hits")[0];
        Assert.Equal(tree, nearest.GetProperty("areaId").GetInt32());
        Assert.Equal(7, nearest.GetProperty("x").GetSingle(), 1);
    }

    // The locations: those asked for by kind are all of that kind, each with its place.
    [AvaloniaFact]
    public async Task TheWorldsLocationsAreListed()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var all = ClaudeRun.J(await r.T.ListLocations(limit: 2000));
        Assert.Equal(all.GetProperty("count").GetInt32(), all.GetProperty("locations").GetArrayLength());
        foreach (var l in ClaudeRun.J(await r.T.ListLocations("boss")).GetProperty("locations").EnumerateArray())
        {
            Assert.Equal("boss", l.GetProperty("kind").GetString());
        }
        foreach (var l in ClaudeRun.J(await r.T.ListLocations(x: 0, z: 0, radius: 500)).GetProperty("locations").EnumerateArray())
        {
            Assert.True(MathF.Sqrt(l.GetProperty("x").GetSingle() * l.GetProperty("x").GetSingle() + l.GetProperty("z").GetSingle() * l.GetProperty("z").GetSingle()) <= 500.1f);
        }
    }

    // Copies in a row (each a step further), and a ring round a pivot; copies on top of the originals, too many, or
    // out of the area are refused and nothing changes.
    [AvaloniaFact]
    public async Task ObjectsAreCopiedInARowAndARing()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var w = r.W.World!;
        int post = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "wood_pole", X = 0, Z = -10, Snap = false }]));
        Assert.StartsWith("The copies would be on top of the originals", await r.T.CopyObjects([post]));
        Assert.Equal("copies is 1 to 100.", await r.T.CopyObjects([post], dx: 1, copies: 101));
        Assert.Contains("outside the area", await r.T.CopyObjects([post], dx: 10, copies: 5));
        Assert.Equal(1, w.Pending.Added);

        var row = ClaudeRun.J(await r.T.CopyObjects([post], dx: 2, copies: 3)).GetProperty("copied");
        Assert.Equal(3, row.GetArrayLength());
        Assert.Equal(6, row[2].GetProperty("x").GetSingle(), 2);
        Assert.Equal(post, row[2].GetProperty("was").GetInt32());
        Assert.Equal("Claude: copied", r.W.Session!.UndoLabel);

        // A ring of four round (0, 0): the post at the south goes west, north and east.
        var ring = ClaudeRun.J(await r.T.CopyObjects([post], turn: 90, pivotX: 0, pivotZ: 0, copies: 3, label: "ring")).GetProperty("copied");
        Assert.Equal(-10, ring[0].GetProperty("x").GetSingle(), 2);
        Assert.Equal(10, ring[1].GetProperty("z").GetSingle(), 2);
        Assert.Equal(10, ring[2].GetProperty("x").GetSingle(), 2);
        Assert.Equal(270, ring[2].GetProperty("yaw").GetSingle(), 1);
        Assert.Equal("Claude: ring", r.W.Session.UndoLabel);
        Assert.Equal(7, w.Pending.Added);
    }

    // A blueprint into a world's area: its lowest piece at the height asked, turned, one step labelled for Claude;
    // a middle outside the area, or pieces that would leave it, change nothing.
    [AvaloniaFact]
    public async Task ABlueprintGoesIntoAWorld()
    {
        using var r = new ClaudeRun();
        r.Hut("Shed", "A shed");
        await r.OpenArea();
        var w = r.W.World!;
        Assert.StartsWith("(500, 0) is outside the open area", await r.T.AddBlueprint("Shed", 500, 0));
        Assert.StartsWith("Nothing added: ", await r.T.AddBlueprint("Shed", 31.9f, 0));
        Assert.Equal(0, w.Pending.Added);

        string added = await r.T.AddBlueprint("Shed", 10, 10, turn: 90, y: 50);
        Assert.StartsWith("Added 2 piece(s) of \"Shed\"", added);
        Assert.Equal("Claude: added Shed", r.W.Session!.UndoLabel);
        Assert.Equal(2, w.Pending.Added);
        var pieces = ClaudeRun.J(await r.T.ListObjects(name: "wood", x: 10, z: 10, radius: 3));
        Assert.Equal(2, pieces.GetArrayLength());
        foreach (var p in pieces.EnumerateArray())
        {
            Assert.Equal(90, p.GetProperty("yaw").GetSingle(), 1);
            Assert.True(p.GetProperty("y").GetSingle() >= 49.5f);
        }
    }
}
