using System.Text.Json;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's tools for objects already there (ClaudeTools.Objects): moving and turning a group, exact places and sizes,
/// replacing with another kind, and reading and changing an object's values, in a world and on the Workshop's plot.
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
        Assert.StartsWith("Nothing moved: -1: not an object of the area", await r.T.MoveObjects([-1], dx: 1));
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

    // On the Workshop's plot (no world files behind it): a chest filled, moved and given a value keeps its contents
    // and values through each change.
    [AvaloniaFact]
    public async Task ThePlotsObjectsKeepTheirValuesWhenMoved()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        int chest = Placed(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = 0, Z = 0 }]));
        chest = NewId(await r.T.SetContents(chest, [new ClaudeTools.ItemSpec { Item = "Amber", Stack = 3 }]));
        chest = ClaudeRun.J(await r.T.MoveObjects([chest], dx: 6)).GetProperty("moved")[0].GetProperty("id").GetInt32();
        chest = NewId(await r.T.SetObjectData(chest, [new ClaudeTools.FieldSpec { Section = "strings", Key = "creatorName", Value = "Thor" }]));

        var o = ClaudeRun.J(await r.T.GetObject(chest));
        Assert.Equal(6, o.GetProperty("x").GetSingle(), 2);
        Assert.Equal("Amber", o.GetProperty("contents")[0].GetProperty("item").GetString());
        Assert.Equal("Thor", Value(o, "creatorName")!.Value.GetProperty("value").GetString());
        Assert.Equal(5, o.GetProperty("slots").GetProperty("columns").GetInt32());
    }
}
