using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's changes to a world's area (ClaudeTools): what the ready tasks refuse, taking away and redoing, what an
/// object can be given to hold (and what it refuses), a dungeon above a point of the area, and a live world's changes
/// that wait to be applied.
/// </summary>
[Collection("World files")]
public class ClaudeToolsChangingTests
{
    // Wrong paints, a road of one point, a forest of nothing, a paint with no place: said, nothing changed. A
    // rectangle painted, a road with its heights and no paint, a level square: one pending step each.
    [AvaloniaFact]
    public async Task ReadyTasksSayWhatIsMissingAndDoWhatIsAsked()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var w = r.W.World!;
        Assert.Equal("The paint is dirt, cultivated, paved or clear.", await r.T.PaintArea("lava", 0, 0, 4, 4));
        Assert.Equal("Give a rectangle (x0, z0, x1, z1) or a circle (x, z, radius).", await r.T.PaintArea("dirt", x: 0, z: 0));
        Assert.Equal("A road needs at least 2 points, each [x, z].", await r.T.Road([[0f, 0f]]));
        Assert.Equal("A road needs at least 2 points, each [x, z].", await r.T.Road([[0f, 0f], [4f]]));
        Assert.Equal("The paint is paved, dirt or none.", await r.T.Road([[0f, 0f], [10f, 0f]], paint: "lava"));
        Assert.Equal("Give at least one prefab name (find_prefabs).", await r.T.Forest(0, 0, 10, 10, []));
        Assert.Equal((0, 0, 0, 0), w.Pending);

        // A rectangle given corner to corner the other way round: the same 5 x 5 points.
        Assert.StartsWith("painted 25 points", await r.T.PaintArea("Cultivated", 4, 4, 0, 0));
        Assert.Equal("Claude: paint cultivated", r.W.Session!.UndoLabel);
        Assert.StartsWith("road of 2 segment(s)", await r.T.Road([[-20f, -20f], [0f, -20f], [0f, 0f]], width: 3, paint: "none", heights: [40, 41, 42]));
        Assert.Equal("Claude: road", r.W.Session.UndoLabel);
        Assert.StartsWith("levelled to", await r.T.Flatten(10, 10, 20, 20, edge: 0));
        Assert.Equal("Claude: flatten", r.W.Session.UndoLabel);
        Assert.True(w.Pending.Zones > 0);
        Assert.Equal(0, w.Pending.Added);
    }

    // Taking away: ids that are not objects are said; what is taken away comes back with undo and goes again with
    // redo; with nothing undone, nothing to redo.
    [AvaloniaFact]
    public async Task ObjectsAreTakenAwayAndPutBack()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        Assert.Equal("Nothing to redo.", await r.T.Redo());
        Assert.Equal("None of those ids is an object of the area.", await r.T.RemoveObjects([-1, 1_000_000]));
        int id = ClaudeRun.J(await r.T.ListObjects(limit: 1))[0].GetProperty("id").GetInt32();
        string took = await r.T.RemoveObjects([id, id, -1]);
        Assert.Equal("Took 1 away (2 id(s) were not objects of the area). Now pending: 0 zone(s) of ground, 0 object(s) added, 1 taken away.", took);
        Assert.Equal("No object with that id in the area.", await r.T.SetContents(id, text: "gone"));
        Assert.Equal("Undone. Now pending: nothing.", await r.T.Undo());
        Assert.StartsWith("Redone. Now pending: 0 zone(s) of ground, 0 object(s) added, 1 taken away.", await r.T.Redo());
        Assert.Equal(1, r.W.World!.Pending.Deleted);
    }

    // What each kind of object holds: a chest's items (no more than its slots), a stand's orientation or pose alone,
    // a sign's text; what an object does not take is said, and so is nothing to change.
    [AvaloniaFact]
    public async Task ObjectsHoldWhatTheyTake()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        static int Placed(string json) => ClaudeRun.J(json).GetProperty("placed")[0].GetProperty("id").GetInt32();
        static int NewId(string said) => int.Parse(said[(said.LastIndexOf(' ') + 1)..].TrimEnd('.'), System.Globalization.CultureInfo.InvariantCulture);
        int chest = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = -10, Z = -10 }]));
        int stand = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "itemstand", X = -12, Z = -10 }]));
        int armour = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "ArmorStand", X = -14, Z = -10 }]));
        int sign = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "sign", X = -16, Z = -10 }]));
        int tree = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = -18, Z = -10 }]));

        Assert.Equal("No object with that id in the area.", await r.T.SetContents(-1, text: "x"));
        Assert.Contains("is not an item stand: only item stands take an orientation", await r.T.SetContents(chest, orientation: 1));
        Assert.Contains("is not an armour stand: only armour stands take a pose", await r.T.SetContents(chest, pose: 1));
        Assert.Equal("Nothing to change: give items, text, stars, an orientation or a pose.", await r.T.SetContents(chest));
        Assert.Contains("has 10 slots, not 11", await r.T.SetContents(chest, [.. Enumerable.Range(0, 11).Select(_ => new ClaudeTools.ItemSpec { Item = "Wood" })]));
        Assert.Contains("is not a container or a stand", await r.T.SetContents(tree, [new ClaudeTools.ItemSpec { Item = "Wood" }]));
        Assert.Contains("is not a sign: only signs take a text", await r.T.SetContents(tree, text: "hello"));

        // A full chest: ten stacks, laid out in its slots.
        string filled = await r.T.SetContents(chest, [.. Enumerable.Range(0, 10).Select(i => new ClaudeTools.ItemSpec { Item = "Coins", Stack = i + 1 })]);
        Assert.StartsWith("Changed", filled);
        Assert.Equal("Claude: filled piece_chest_wood", r.W.Session!.UndoLabel);
        Assert.StartsWith("Changed", await r.T.SetContents(stand, orientation: 2));
        Assert.StartsWith("Changed", await r.T.SetContents(armour, pose: 3));
        string written = await r.T.SetContents(sign, text: "Welcome");
        Assert.StartsWith("Changed", written);
        var s = r.W.Session;
        byte[] bytes = TerrainEditor.App.ObjectData.Bytes(s.Scene.World!, s.Edits, s.Scene.Things[NewId(written)].Id)!;
        Assert.Contains("Welcome", System.Text.Encoding.UTF8.GetString(bytes));
    }

    // A dungeon above a point of the area (with its portals there) is one pending step; a point outside the area
    // is refused.
    [AvaloniaFact]
    public async Task ADungeonGoesAboveAPointOfTheArea()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        Assert.StartsWith("(500, 0) is outside the open area (x -32 to 32, z -32 to 32)", await r.T.GenerateDungeon(size: 1, levels: 1, x: 500, z: 0));
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        string made = await r.T.GenerateDungeon(biome: "Meadows", size: 9, levels: 0, monsters: 5, loot: -1, light: 7, decor: -2, seed: 3, name: "Test hold", x: 5, z: 5);
        Assert.Contains("Now pending:", made);
        Assert.True(r.W.World.Pending.Added > 0, made);
    }

    // Live with Auto off, Claude's change waits for Apply live: another world is not opened over it.
    [AvaloniaFact]
    public async Task ALiveWorldsChangesWaitToBeApplied()
    {
        using var r = new ClaudeRun();
        await r.OpenLive();
        Assert.Contains("ground point", await r.T.RunScript("Ground.Raise(0, 0, 3);", "Claude: a bump"));
        Assert.Equal("Claude: a bump", r.W.Session!.UndoLabel);
        string refused = await r.T.OpenWorld(r.Dir);
        Assert.StartsWith("The open world has changes not applied (1 zone(s), 0 added, 0 deleted): ask the user to apply", refused);
        Assert.True(ClaudeRun.J(await r.T.EditorState()).GetProperty("world").GetProperty("live").GetBoolean());
    }
}
