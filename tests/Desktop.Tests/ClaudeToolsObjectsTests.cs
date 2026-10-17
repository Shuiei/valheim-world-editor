using System.Numerics;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's tools for objects already there (ClaudeTools.Objects): moving and turning a group, exact places and sizes,
/// replacing with another kind, and reading and changing an object's values, in a world and on the Workshop's plot;
/// and the helpers they share (Turned, Wrapped, Bounds, Usable, Rewrite).
/// </summary>
[Collection("World files")]
public class ClaudeToolsObjectsTests
{
    /// <summary>The id of the first object place_pieces put down.</summary>
    private static int Placed(string json) => ClaudeRun.J(json).GetProperty("placed")[0].GetProperty("id").GetInt32();

    /// <summary>The new id at the end of an answer ("…; its id is now 12." or "… Now pending: …").</summary>
    private static int NewId(string said)
    {
        const string Mark = "its id is now ";
        int at = said.IndexOf(Mark, StringComparison.Ordinal) + Mark.Length;
        return int.Parse(said[at..].Split('.')[0], System.Globalization.CultureInfo.InvariantCulture);
    }

    // Ids are indices into the things of the area they were given for: once another area is opened (or the same one
    // again), or the world was read again after a save, they would hit whatever objects have those indices now, so
    // every tool taking ids refuses them until objects are listed again.
    [AvaloniaFact]
    public async Task IdsOfAnotherAreaAreRefusedUntilListedAgain()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int post = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "wood_pole2", X = 3, Z = 3 }]));
        var other = TestScenes.Flat();
        r.W.View.Show(other.Scene, null);
        r.W.Edit(other);
        const string Stale = "The ids are from another area: list_objects (or get_selection) again for this one.";
        Assert.Equal(Stale, await r.T.RemoveObjects([post]));
        Assert.Equal(Stale, await r.T.GetObject(post));
        Assert.Equal(Stale, await r.T.MoveObjects([post], dx: 1));
        Assert.Equal(Stale, await r.T.SetTransform([new ClaudeTools.TransformSpec { Id = post, X = 1 }]));
        Assert.Equal(Stale, await r.T.ReplaceObjects([post], "Birch1"));
        Assert.Equal(Stale, await r.T.CopyObjects([post], dx: 2));
        Assert.Equal(Stale, await r.T.SetContents(post, text: "hello"));
        Assert.Equal(Stale, await r.T.SetObjectData(post, [new ClaudeTools.FieldSpec { Section = "strings", Key = "text", Value = "hello" }]));
        Assert.Equal(Stale, await r.T.SelectObjects([post], focus: false));
        Assert.Equal(0, other.Scene.Things.Count(t => t.Gone));
        // A listing or a selection with nothing in it gives no id: the old ones stay refused.
        Assert.Equal("[]", await r.T.ListObjects(name: "nothing is called this"));
        Assert.Equal("[]", await r.T.GetSelection());
        Assert.Equal(Stale, await r.T.GetObject(post));
        // Ids given for this area are taken.
        int here = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "wood_pole2", X = 3, Z = 3 }]));
        Assert.Equal("wood_pole2", ClaudeRun.J(await r.T.GetObject(here)).GetProperty("name").GetString());
        // The world read again (after a save): its things are listed anew, the ids given before are refused until a
        // listing gives this area's.
        other.Scene.Reads++;
        Assert.Equal("The ids are from before the world was read again: list_objects (or get_selection) again for this one.", await r.T.GetObject(here));
        Assert.NotEqual("[]", await r.T.ListObjects(name: "wood_pole2"));
        Assert.Equal("wood_pole2", ClaudeRun.J(await r.T.GetObject(here)).GetProperty("name").GetString());
    }

    /// <summary>A value of get_object's answer by key, or null.</summary>
    private static JsonElement? Value(JsonElement o, string key) =>
        o.GetProperty("values").EnumerateArray().Cast<JsonElement?>().FirstOrDefault(v => v!.Value.GetProperty("key").GetString() == key);

    // Two objects moved and turned a quarter round their middle: they swap sides as a rigid group, each turned too,
    // with new ids; the chest keeps its contents. A move out of the area, or one with nothing to do, changes nothing.
    [AvaloniaFact]
    public async Task AGroupMovesAndTurnsAndKeepsWhatItHolds()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int chest = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = -4, Z = 0, Snap = false }]));
        int sign = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "sign", X = 4, Z = 0, Snap = false }]));
        chest = NewId(await r.T.SetContents(chest, [new ClaudeTools.ItemSpec { Item = "Coins", Stack = 25 }]));

        Assert.Equal("Nothing to do: give dx, dy, dz, turn or ground.", await r.T.MoveObjects([chest, sign]));
        Assert.Contains("outside the area", await r.T.MoveObjects([chest, sign], dx: 100));

        var moved = ClaudeRun.J(await r.T.MoveObjects([chest, sign, -5], dz: 2, turn: 90)).GetProperty("moved");
        Assert.Equal("Claude: moved", r.W.Session!.UndoLabel);
        var c = moved[0];
        Assert.Equal(chest, c.GetProperty("was").GetInt32());
        Assert.NotEqual(chest, c.GetProperty("id").GetInt32());
        // Turned 90° round (0, 0): west (-4, 0) goes north (0, 4), then 2 m north.
        Assert.Equal(0, c.GetProperty("x").GetSingle(), 2);
        Assert.Equal(6, c.GetProperty("z").GetSingle(), 2);
        Assert.Equal(90, c.GetProperty("yaw").GetSingle(), 1);
        Assert.Equal(-2, moved[1].GetProperty("z").GetSingle(), 2);

        var o = ClaudeRun.J(await r.T.GetObject(c.GetProperty("id").GetInt32()));
        Assert.Equal("Coins", o.GetProperty("contents")[0].GetProperty("item").GetString());
        Assert.Equal(25, o.GetProperty("contents")[0].GetProperty("stack").GetInt32());
        Assert.Equal("No object with that id in the area.", await r.T.GetObject(chest));
    }

    // A chest whose contents are in an item format older than the editor reads: get_object says they cannot be read
    // and still answers with the rest.
    [AvaloniaFact]
    public async Task ContentsInAnOlderItemFormatCannotBeRead()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int chest = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = -4, Z = 0, Snap = false }]));
        Assert.True(r.W.Inspector.Open(chest));
        r.W.Inspector.Added.Add(("bytes", "items", Convert.ToBase64String(new byte[] { 100, 0, 0, 0, 0, 0 })));
        await r.W.Inspector.Apply();
        int changed = r.W.Session!.Scene.Things.FindLastIndex(t => t.Prefab == Save.StableHash.Of("piece_chest_wood") && !t.Gone);
        var o = ClaudeRun.J(await r.T.GetObject(changed));
        Assert.Equal("cannot be read", o.GetProperty("contents").GetString());
        Assert.Equal("piece_chest_wood", o.GetProperty("name").GetString());
    }

    // Exact places, turns and sizes, each object its own; what is left out stays; a size out of range and a place
    // out of the area are named and left out.
    [AvaloniaFact]
    public async Task ObjectsArePlacedTurnedAndSizedExactly()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int tree = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = 10, Z = 10 }]));
        int other = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = -10, Z = 10 }]));
        float y = ClaudeRun.J(await r.T.GetObject(tree)).GetProperty("y").GetSingle();

        Assert.StartsWith("Nothing changed: ", await r.T.SetTransform([new ClaudeTools.TransformSpec { Id = tree, Scale = 50 }]));
        var answer = ClaudeRun.J(await r.T.SetTransform(
        [
            new ClaudeTools.TransformSpec { Id = tree, X = 12, Yaw = 45, Scale = 1.5f },
            new ClaudeTools.TransformSpec { Id = other, Z = 500 },
        ]));
        Assert.Contains("outside the area", answer.GetProperty("problems")[0].GetString());
        var o = ClaudeRun.J(await r.T.GetObject(answer.GetProperty("placed")[0].GetProperty("id").GetInt32()));
        Assert.Equal(12, o.GetProperty("x").GetSingle(), 2);
        Assert.Equal(10, o.GetProperty("z").GetSingle(), 2);
        Assert.Equal(y, o.GetProperty("y").GetSingle(), 2);
        Assert.Equal(45, o.GetProperty("yaw").GetSingle(), 1);
        Assert.Equal(1.5, o.GetProperty("scale").GetSingle(), 2);
    }

    // On the ground at a new size: its lowest point at that size touches the ground.
    [AvaloniaFact]
    public async Task AnObjectSizedOnTheGroundStandsOnItsScaledBottom()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int pole = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_maypole", X = 10, Z = 10 }]));
        var one = ClaudeRun.J(await r.T.SetTransform([new ClaudeTools.TransformSpec { Id = pole, OnGround = true }]));
        pole = one.GetProperty("placed")[0].GetProperty("id").GetInt32();
        float y1 = ClaudeRun.J(await r.T.GetObject(pole)).GetProperty("y").GetSingle();
        var two = ClaudeRun.J(await r.T.SetTransform([new ClaudeTools.TransformSpec { Id = pole, Scale = 2, OnGround = true }]));
        float y2 = ClaudeRun.J(await r.T.GetObject(two.GetProperty("placed")[0].GetProperty("id").GetInt32())).GetProperty("y").GetSingle();
        float bottom = Editing.Hammer.Bottom("piece_maypole", Quaternion.Identity);
        Assert.NotEqual(0, bottom);
        Assert.Equal(y1 - bottom, y2, 2);
    }

    // Objects become another kind where they are; an unknown kind is said and nothing changes.
    [AvaloniaFact]
    public async Task ObjectsAreReplacedWithAnotherKind()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int tree = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = 5, Z = 5, Yaw = 30 }]));
        Assert.Equal("no_such_tree is not an object the game has (find_prefabs).", await r.T.ReplaceObjects([tree], "no_such_tree"));
        var replaced = ClaudeRun.J(await r.T.ReplaceObjects([tree], "Birch1")).GetProperty("replaced")[0];
        Assert.Equal("Birch1", replaced.GetProperty("name").GetString());
        Assert.Equal(5, replaced.GetProperty("x").GetSingle(), 2);
        Assert.Equal(30, replaced.GetProperty("yaw").GetSingle(), 1);
        Assert.Equal("Claude: replaced 1 with Birch1", r.W.Session!.UndoLabel);
        // The new one keeps the old one's size (it got none: the prefab's own).
        var s = r.W.Session!;
        int big = s.Commit("big", null, [], [(new Editing.NewObject(0, Save.StableHash.Of("Beech1"), new Vector3(8, 30, 8), Vector3.Zero, 2f), false)])[0];
        int grown = ClaudeRun.J(await r.T.ReplaceObjects([big], "Birch1")).GetProperty("replaced")[0].GetProperty("id").GetInt32();
        Assert.Equal(2f, s.Scene.Things[grown].Scale);
    }

    // A world's object: values set (a portal's tag), changed (taken away) and read back by name; wrong kinds and
    // values are said and nothing changes.
    [AvaloniaFact]
    public async Task AnObjectsValuesAreReadAndChanged()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int portal = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "portal_wood", X = 0, Z = 8 }]));
        var before = ClaudeRun.J(await r.T.GetObject(portal));
        Assert.Equal("portal_wood", before.GetProperty("name").GetString());
        Assert.True(before.GetProperty("built").GetBoolean());

        Assert.Equal("Nothing to change: give values.", await r.T.SetObjectData(portal, []));
        Assert.StartsWith("tag: the kind is strings", await r.T.SetObjectData(portal, [new ClaudeTools.FieldSpec { Section = "bytes", Key = "tag", Value = "x" }]));
        Assert.Equal("health: \"lots\" is not a value of kind floats.", await r.T.SetObjectData(portal, [new ClaudeTools.FieldSpec { Section = "floats", Key = "health", Value = "lots" }]));
        Assert.Equal("Every value needs a key (its name).", await r.T.SetObjectData(portal, [new ClaudeTools.FieldSpec { Key = " ", Value = "x" }]));
        Assert.Equal(1, r.W.World!.Pending.Added);

        string said = await r.T.SetObjectData(portal, [new ClaudeTools.FieldSpec { Key = "tag", Value = "home" }, new ClaudeTools.FieldSpec { Section = "floats", Key = "health", Value = "400" }]);
        Assert.StartsWith("Changed portal_wood; its id is now ", said);
        Assert.Equal("Claude: edited portal_wood", r.W.Session!.UndoLabel);
        var after = ClaudeRun.J(await r.T.GetObject(NewId(said)));
        Assert.Equal("home", Value(after, "tag")!.Value.GetProperty("value").GetString());
        Assert.Equal("400", Value(after, "health")!.Value.GetProperty("value").GetString());

        said = await r.T.SetObjectData(NewId(said), [new ClaudeTools.FieldSpec { Section = "floats", Key = "health" }]);
        after = ClaudeRun.J(await r.T.GetObject(NewId(said)));
        Assert.Null(Value(after, "health"));
        Assert.Equal("home", Value(after, "tag")!.Value.GetProperty("value").GetString());
    }

    // Turned as yaw turns: a quarter turn takes north to east and east to south; a whole turn changes nothing.
    [Fact]
    public void PointsTurnAsYawTurns()
    {
        var (x, z) = ClaudeTools.Turned(0, 1, 90);
        Assert.Equal(1, x, 4);
        Assert.Equal(0, z, 4);
        (x, z) = ClaudeTools.Turned(1, 0, 90);
        Assert.Equal(0, x, 4);
        Assert.Equal(-1, z, 4);
        (x, z) = ClaudeTools.Turned(3, -2, 360);
        Assert.Equal(3, x, 3);
        Assert.Equal(-2, z, 3);
    }

    // Wrapped: each angle brought within 0 to 360 degrees, either way round.
    [Fact]
    public void AnglesAreBroughtWithinATurn()
    {
        Assert.Equal(new Vector3(0, 23, 350), ClaudeTools.Wrapped(new Vector3(360, 383, -10)));
        Assert.Equal(new Vector3(90, 0, 180), ClaudeTools.Wrapped(new Vector3(90, -720, 540)));
    }

    // Bounds: the open zone around (0, 0) is 64 m across, centred on it.
    [AvaloniaFact]
    public async Task TheAreasBoundsAreItsZones()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        Assert.Equal((-32f, -32f, 32f, 32f), ClaudeTools.Bounds(r.W.Session!.Scene));
    }

    // Usable: each object once; ids out of range and objects taken away are named, not kept.
    [AvaloniaFact]
    public async Task OnlyObjectsOfTheAreaAreUsable()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int a = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = 1, Z = 1 }]));
        int gone = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "Beech1", X = 3, Z = 3 }]));
        await r.T.RemoveObjects([gone]);
        var (ok, problems) = ClaudeTools.Usable(r.W.Session!.Scene, [a, a, gone, -1, 1_000_000]);
        Assert.Equal([a], ok);
        Assert.Equal([$"{gone}: not an object of the area", "-1: not an object of the area", "1000000: not an object of the area"], problems);
    }

    // Rewrite in a world: the copy holds the new value, in one step named as asked; a value that does not fit its
    // kind is said and nothing changes.
    [AvaloniaFact]
    public async Task AWorldsObjectIsRewrittenWithItsNewValues()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        int sign = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "sign", X = 2, Z = 2 }]));
        var s = r.W.Session!;
        var (none, problem) = r.T.Rewrite(sign, [new("ints", "level", "many")], null, "Claude: bad");
        Assert.Null(none);
        Assert.StartsWith("Could not change it: ", problem);
        Assert.NotEqual("Claude: bad", s.UndoLabel);

        var (id, _) = r.T.Rewrite(sign, [new("strings", "text", "Hello")], null, "Claude: test");
        Assert.Equal("Claude: test", s.UndoLabel);
        Assert.True(s.Scene.Things[sign].Gone);
        byte[] bytes = TerrainEditor.App.ObjectData.Bytes(s.Scene.World!, s.Edits, s.Scene.Things[id!.Value].Id)!;
        Assert.Contains("Hello", System.Text.Encoding.UTF8.GetString(bytes));
    }

    // Rewrite on the plot: the values go with the piece, a value set again replaced, contents written with the
    // default-loot mark.
    [AvaloniaFact]
    public async Task APlotsObjectIsRewrittenWithItsNewValues()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        int chest = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = 0, Z = 0 }]));
        var s = r.W.Session!;
        int id = r.T.Rewrite(chest, [new("strings", "creatorName", "Odin")], [new("Wood", null, Stack: 5)], "Claude: test").Id!.Value;
        id = r.T.Rewrite(id, [new("strings", "creatorName", "Thor")], null, "Claude: test").Id!.Value;
        var data = s.Edits.FindAdded(s.Scene.Things[id].Id)!.Data!;
        Assert.Single(data, f => f.Key == Save.StableHash.Of("creatorName") && f.Value == "Thor");
        Assert.Contains(data, f => f.Key == TerrainEditor.App.ObjectData.ItemsKey);
        Assert.Contains(data, f => f.Key == TerrainEditor.App.ObjectData.AddedDefaultItemsKey && f.Value == "1");
    }
}
