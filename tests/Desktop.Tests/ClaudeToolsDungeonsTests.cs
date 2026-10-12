using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's dungeon tools (ClaudeTools.Dungeons) on the test world's zone 0, 0 with a one-room Frost Cave made there:
/// what each says when there is no dungeon or a wrong index, the dungeon listed with its rooms, open ends and the rooms
/// it can take, a room added and taken out, the open ends capped and a door put in and out, each a step named for
/// Claude, with the dungeon's new id given; and how opening types are named (TypeName).
/// </summary>
[Collection("World files")]
public class ClaudeToolsDungeonsTests
{
    /// <summary>The test world's zone 0, 0 open, with a Frost Cave of its entrance room alone (three open ends).</summary>
    private static async Task<ClaudeRun> WithCave()
    {
        var r = new ClaudeRun();
        await r.OpenArea();
        int cave = StableHash.Of("DG_Cave");
        var at = new Vector3(10, 5100, 10);
        var entrance = new Dungeons.Placed(StableHash.Of("cave_new_entrance02"), at, Quaternion.Identity);
        byte[] blank = ZdoBuilder.Blank(cave, Terrain.PrefabCatalog.Get(cave)!.Flags, at, Vector3.Zero, 0f);
        r.W.Session!.Commit("A dungeon", null, [], [(new NewObject(0, cave, at, Vector3.Zero, 0, null, false, Dungeons.WithRooms(blank, [entrance])), false)]);
        return r;
    }

    /// <summary>The id of the area's one dungeon.</summary>
    private static async Task<int> Id(ClaudeRun r) => ClaudeRun.J(await r.T.ListDungeons())[0].GetProperty("id").GetInt32();

    /// <summary>The new id at the end of an answer ("… The dungeon's id is now 12 (2 rooms). …").</summary>
    private static int NewId(string said)
    {
        const string Mark = "The dungeon's id is now ";
        int at = said.IndexOf(Mark, StringComparison.Ordinal) + Mark.Length;
        return int.Parse(said[at..].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
    }

    // The Workshop has no dungeons of the game; an id that is no dungeon is said.
    [AvaloniaFact]
    public async Task WithoutADungeonEachToolSaysSo()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        Assert.StartsWith("Dungeons are listed in a world's area", await r.T.ListDungeons());
        Assert.StartsWith("Dungeons are edited in a world's area", await r.T.DungeonRoomList(0));
        await r.OpenArea();
        Assert.Equal("[]", await r.T.ListDungeons());
        Assert.Equal("No dungeon with id 0 in the area (list_dungeons).", await r.T.AddDungeonRoom(0, 0, "cave_new_corridor03"));
    }

    // The cave listed (its kind, one room, three open ends); its rooms, open ends and the rooms it can take (a cave's,
    // not a crypt's).
    [AvaloniaFact]
    public async Task ADungeonIsListedWithItsRooms()
    {
        using var r = await WithCave();
        var d = ClaudeRun.J(await r.T.ListDungeons())[0];
        Assert.Equal(("Frost Cave", 1, 3), (d.GetProperty("kind").GetString(), d.GetProperty("rooms").GetInt32(), d.GetProperty("openEnds").GetInt32()));
        var rooms = ClaudeRun.J(await r.T.DungeonRoomList(await Id(r)));
        Assert.Equal("cave_new_entrance02", rooms.GetProperty("rooms")[0].GetProperty("name").GetString());
        Assert.Equal(3, rooms.GetProperty("openEnds").GetArrayLength());
        var can = rooms.GetProperty("canTake").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Contains("cave_new_corridor03", can);
        Assert.DoesNotContain(can, n => n!.StartsWith("sunkencrypt", StringComparison.Ordinal));
    }

    // A wrong end or room is said; a corridor added (no contents, no door) gives the dungeon a new id and 2 rooms;
    // a wrong room index is said; the corridor taken out leaves 1.
    [AvaloniaFact]
    public async Task ARoomIsAddedAndTakenOut()
    {
        using var r = await WithCave();
        int id = await Id(r);
        Assert.Equal("The dungeon has 3 free opening(s): end is 0 to 2 (dungeon_rooms).", await r.T.AddDungeonRoom(id, 5, "cave_new_corridor03"));
        Assert.StartsWith("sunkencrypt_corridor is not a room this Frost Cave can take", await r.T.AddDungeonRoom(id, 0, "sunkencrypt_corridor"));
        int things = r.W.Session!.Scene.Things.Count;
        string added = await r.T.AddDungeonRoom(id, 0, "cave_new_corridor03", contents: false, doors: false);
        Assert.StartsWith("Added cave_new_corridor03.", added);
        Assert.Contains("(2 rooms)", added);
        Assert.Equal("Claude: added cave_new_corridor03", r.W.Session.UndoLabel);
        // The dungeon object again, nothing else.
        Assert.Equal(things + 1, r.W.Session.Scene.Things.Count);
        id = NewId(added);
        Assert.Equal("The dungeon has 2 room(s): room is 0 to 1.", await r.T.RemoveDungeonRoom(id, 2));
        string removed = await r.T.RemoveDungeonRoom(id, 1);
        Assert.Contains("(1 rooms)", removed);
        Assert.Equal("Claude: removed cave_new_corridor03", r.W.Session.UndoLabel);
    }

    // The open ends capped in one step; again, nothing to close.
    [AvaloniaFact]
    public async Task OpenEndsAreClosed()
    {
        using var r = await WithCave();
        string closed = await r.T.CloseDungeonEnds(await Id(r));
        Assert.StartsWith("Closed 3 open end(s)", closed);
        Assert.Equal("Claude: closed open ends", r.W.Session!.UndoLabel);
        Assert.Equal("No open end to close.", await r.T.CloseDungeonEnds(NewId(closed)));
    }

    // A door put at a joint and taken out; asking for what is there already is said; a wrong joint too.
    [AvaloniaFact]
    public async Task ADoorIsPutInAndTakenOut()
    {
        using var r = await WithCave();
        int id = NewId(await r.T.AddDungeonRoom(await Id(r), 0, "cave_new_corridor03", contents: false, doors: false));
        Assert.Equal("The dungeon has 1 joint(s) that take a door: joint is 0 to 0.", await r.T.SetDungeonDoor(id, 3));
        Assert.Equal("There is no door there.", await r.T.SetDungeonDoor(id, 0, door: false));
        Assert.Contains("Now pending:", await r.T.SetDungeonDoor(id, 0));
        Assert.Equal("Claude: added a dungeon door", r.W.Session!.UndoLabel);
        Assert.Equal("There is a door there already.", await r.T.SetDungeonDoor(id, 0));
        await r.T.SetDungeonDoor(id, 0, door: false);
        Assert.Equal("Claude: removed a dungeon door", r.W.Session.UndoLabel);
    }

    // TypeName: the game's plain openings (no type) read "plain"; others as they are.
    [Fact]
    public void OpeningTypesAreNamed()
    {
        Assert.Equal("plain", ClaudeTools.TypeName(""));
        Assert.Equal("cave", ClaudeTools.TypeName("cave"));
    }
}
