using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's tools for the whole world and for copies (ClaudeTools.World): searching the world, its locations, zones
/// marked for reset, a seed previewed and a world made, copies in a row or a ring, and a blueprint of the library put
/// into a world's area; and the helpers they use (Known, LocationName, ZonesOf, SeedPicture).
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

    // LocationName: the rules' name first, then the known locations' (the start, traders, bosses, dungeons: named
    // even before the game's rules are read), then the prefab catalogue's; none of them: none.
    [Fact]
    public void LocationsAreNamedFromTheRulesFirst()
    {
        var rules = new Dictionary<int, string> { [StableHash.Of("MyPlace")] = "MyPlace" };
        Assert.Equal("MyPlace", ClaudeTools.LocationName(StableHash.Of("MyPlace"), rules));
        Assert.Equal("StartTemple", ClaudeTools.LocationName(StableHash.Of("StartTemple"), []));
        Assert.Equal("Eikthyrnir", ClaudeTools.LocationName(StableHash.Of("Eikthyrnir"), []));
        Assert.Equal("Crypt2", ClaudeTools.LocationName(StableHash.Of("Crypt2"), []));
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

    // The locations: the list stops at the limit; those asked for are of that kind or have the word in their name.
    [AvaloniaFact]
    public async Task TheWorldsLocationsAreListed()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        // The test world has more locations than one answer gives: the list stops at the limit, the count does not.
        var all = ClaudeRun.J(await r.T.ListLocations(limit: 2000));
        Assert.Equal(Math.Min(all.GetProperty("count").GetInt32(), 2000), all.GetProperty("locations").GetArrayLength());
        // "boss" finds the bosses' altars by kind, and what has it in its name (the start's BossStone_… stones).
        foreach (var l in ClaudeRun.J(await r.T.ListLocations("boss")).GetProperty("locations").EnumerateArray())
        {
            Assert.True(l.GetProperty("kind").GetString() == "boss" || l.GetProperty("name").GetString()!.Contains("boss", StringComparison.OrdinalIgnoreCase), l.ToString());
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
        // A fence 12 m long round its anchor: put 6 m from the area's edge, one end is outside.
        static System.Text.Json.Nodes.JsonNode Post(float dx) => new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = "wood_pole2",
            ["dx"] = dx,
            ["dy"] = 1,
            ["dz"] = 0,
            ["rx"] = 0,
            ["ry"] = 0,
            ["rz"] = 0,
            ["scale"] = 0,
        };
        var clip = new System.Text.Json.Nodes.JsonObject { ["objects"] = new System.Text.Json.Nodes.JsonArray(Post(-6), Post(6)) };
        File.WriteAllText(Path.Combine(r.Homestead, Homestead.FileName("Fence")), Homestead.Write(clip, "Fence", "Tester", null, DateTime.Now));
        await r.OpenArea();
        var w = r.W.World!;
        Assert.StartsWith("(500, 0) is outside the open area", await r.T.AddBlueprint("Shed", 500, 0));
        Assert.StartsWith("Nothing added: 1 piece(s) would be outside the area", await r.T.AddBlueprint("Fence", 28, 0));
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

    // ZonesOf: the 64 m zones a rectangle touches, given corner to corner either way; zone 0 runs from -32 to 32.
    [Fact]
    public void ARectangleTouchesItsZones()
    {
        Assert.Equal([(0, 0)], ClaudeTools.ZonesOf(-31, -31, 31, 31));
        Assert.Equal([(-1, 0), (0, 0), (-1, 1), (0, 1)], ClaudeTools.ZonesOf(10, 40, -40, 10));
    }

    // In the Workshop, or outside the area: said. Zones of the area marked (buildings kept), then unmarked; nothing
    // marked: said.
    [AvaloniaFact]
    public async Task ZonesAreMarkedForResetAndUnmarked()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        Assert.Equal("Zones are reset in a world, not on the Workshop's plot.", await r.T.ResetZones(0, 0, 10, 10));
        await r.OpenArea();
        Assert.StartsWith("The rectangle touches no zone of the open area", await r.T.ResetZones(500, 500, 510, 510));
        Assert.Equal("None of those zones was marked for reset.", await r.T.ResetZones(0, 0, 10, 10, cancel: true));
        Assert.DoesNotContain("unmarked", r.W.MessageText.Text, StringComparison.Ordinal);
        // Half outside the one-zone area: only its own zone.
        Assert.StartsWith("Marked 1 zone(s) for reset when saved (0, 0), the players' buildings kept.", await r.T.ResetZones(0, 0, 100, 10));
        Assert.Equal("Claude: reset 1 zone(s)", r.W.Session!.UndoLabel);
        Assert.Single(r.W.Session.Resets);
        Assert.StartsWith("Unmarked 1 zone(s).", await r.T.ResetZones(0, 0, 10, 10, cancel: true));
        Assert.Empty(r.W.Session.Resets);
    }

    // SeedPicture: a 640-pixel square JPEG.
    [Fact]
    public void ASeedsPictureIsASquare()
    {
        var p = SeedPreview.Make("abc", 32, cancel: TestContext.Current.CancellationToken);
        using var bitmap = SkiaSharp.SKBitmap.Decode(ClaudeTools.SeedPicture(p));
        Assert.Equal((640, 640), (bitmap.Width, bitmap.Height));
    }

    // A bad seed is said; a seed's preview gives its land, start and biomes, with a picture.
    [AvaloniaFact]
    public async Task ASeedIsPreviewed()
    {
        var bad = await ClaudeTools.PreviewSeed("12345678901");
        Assert.True(bad.IsError);
        var good = await ClaudeTools.PreviewSeed(" abc ");
        var text = ClaudeRun.J(Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(good.Content[0]).Text);
        Assert.Equal("abc", text.GetProperty("seed").GetString());
        Assert.InRange(text.GetProperty("land").GetSingle(), 0.01f, 1);
        Assert.NotEmpty(text.GetProperty("biomes").EnumerateArray());
        Assert.IsType<ModelContextProtocol.Protocol.ImageContentBlock>(good.Content[1]);
    }

    // A world made in the folder given is opened on its map; a name taken there is refused (Check) and nothing
    // changes.
    [AvaloniaFact]
    public async Task ANewWorldIsMadeAndOpened()
    {
        using var r = new ClaudeRun();
        string root = Path.Combine(Path.GetTempPath(), "vwe-claude-world-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            string made = await r.T.CreateWorld("Fresh", "abc", root);
            Assert.StartsWith("Made Fresh (seed abc).", made);
            Assert.Equal("Fresh", r.W.World!.World.Name);
            Assert.StartsWith("There is a world called Fresh already in ", await r.T.CreateWorld("Fresh", "xyz", root));
        }
        finally
        {
            await r.W.LeaveWorld();
            Directory.Delete(root, true);
        }
    }
}
