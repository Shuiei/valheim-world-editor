using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data's room and dungeon files read for the Dungeon tool (ExpandWorld.DungeonsOf): the mod's defaults,
/// openings, added objects, a blueprint room left out, room limits, and no file meaning the game's rooms.
/// </summary>
public class ExpandWorldDungeonsTests
{
    private static ExpandWorld.Entry E(params (string Key, object? Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => f.Value));

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] fields) => fields.ToDictionary(f => f.Key, f => f.Value);

    [Fact]
    public void BothSwitchesOffGiveNone() => Assert.Null(ExpandWorld.DungeonsOf(ExpandWorld.DataFiles.None));

    [Fact]
    public void ARoomIsReadWithTheModsDefaults()
    {
        var data = ExpandWorld.DataFiles.None with
        {
            Rooms =
            [
                E(("name", "cave_new_corridor01:Lit"), ("theme", "Cave, Glow"), ("weight", "3"), ("size", "12,12,8"),
                    ("connections", new List<object?> { Map(("position", "6,0,-3,90,0,0"), ("type", "lit"), ("exitOnly", "true"), ("door", "other")), "not a map" }),
                    ("objects", new List<object?> { "GlowingMushroom, 1, 2, 0.5, 0, 0, 0, 1, 1, 1, 0.5", "NoSuchThing, 0, 0, 0" })),
                E(("name", "cave_new_corridor01")),
            ],
        };
        var rooms = ExpandWorld.DungeonsOf(data)!.Rooms!;
        var copy = rooms[0];
        Assert.Equal(("cave_new_corridor01:Lit", "Cave, Glow", 3f, "12,12,8"), (copy.Name, copy.Theme, copy.Weight, copy.Size));
        Assert.Equal(new RoomConnection("6,0,-3,90,0,0", "lit", false, true, "other"), Assert.Single(copy.Connections));
        var mushroom = Assert.Single(copy.Objects);
        Assert.Equal(("GlowingMushroom", 0.5f), (mushroom.Prefab, mushroom.Chance));
        Assert.Contains(data.Notes, n => n.Contains("NoSuchThing", StringComparison.Ordinal));
        // Every value left out is the mod's default (RoomYaml).
        var plain = new RoomEntry("cave_new_corridor01", "", true, false, false, false, 0, 1f, false, "", [], []);
        Assert.Equal(plain, rooms[1] with { Connections = [], Objects = [], Changes = plain.Changes });
        Assert.False(rooms[1].Changes.Any);
    }

    [Fact]
    public void SwapsAndDataOfARoomAndADungeonAreRead()
    {
        var data = ExpandWorld.DataFiles.None with
        {
            DataEntries = [E(("name", "marked"), ("ints", "mark, 1"))],
            Rooms =
            [
                E(("name", "cave_new_corridor01"), ("objectSwap", new List<object?> { "caverock_ice_stalagtite, Rock_3:3, :1", "NoSuchThing, Rock_3" }),
                    ("objectData", new List<object?> { "all, marked" })),
            ],
            Dungeons = [E(("name", "DG_Cave"), ("objectSwap", new List<object?> { "Rock_3, GlowingMushroom" }), ("objectData", new List<object?> { "Rock_3, marked:1, :1" }))],
        };
        var rules = ExpandWorld.DungeonsOf(data)!;
        var room = Assert.Single(rules.Rooms!);
        Assert.Equal([new(0.75f, "Rock_3"), new(0.25f, "")], room.Changes.Swaps["caverock_ice_stalagtite"]);
        Assert.Equal([new ContentField("ints", TerrainEditor.Save.StableHash.Of("mark"), "1")], Assert.Single(room.Changes.Data["all"]).Value!);
        // A source that is no prefab of the game cannot be followed: left out, noted.
        Assert.False(room.Changes.Swaps.ContainsKey("NoSuchThing"));
        Assert.Contains(data.Notes, n => n.Contains("expand_rooms: cave_new_corridor01", StringComparison.Ordinal) && n.Contains("NoSuchThing", StringComparison.Ordinal));
        var cave = Assert.Single(rules.Dungeons);
        Assert.Equal([new(1f, "GlowingMushroom")], cave.Changes.Swaps["Rock_3"]);
        Assert.Equal([0.5f, 0.5f], cave.Changes.Data["Rock_3"].Select(w => w.Weight));
    }

    [Fact]
    public void ABlueprintRoomIsLeftOutAndNoted()
    {
        var data = ExpandWorld.DataFiles.None with { Rooms = [E(("name", "MyBlueprintRoom"))] };
        Assert.Empty(ExpandWorld.DungeonsOf(data)!.Rooms!);
        Assert.Contains(data.Notes, n => n.Contains("MyBlueprintRoom is a blueprint room", StringComparison.Ordinal));
    }

    [Fact]
    public void NoRoomFileIsTheGamesRooms()
    {
        var rules = ExpandWorld.DungeonsOf(ExpandWorld.DataFiles.None with { Rooms = [], Dungeons = [] })!;
        Assert.Null(rules.Rooms);
        Assert.Empty(rules.Dungeons);
    }

    [Fact]
    public void ADungeonIsReadWithTheModsDefaults()
    {
        var data = ExpandWorld.DataFiles.None with
        {
            Dungeons =
            [
                E(("name", "DG_Cave"), ("algorithm", "Dungeon"), ("bounds", "128"), ("themes", "Cave"), ("maxRooms", "40"), ("minRooms", "20"),
                    ("requiredRooms", "cave_new_corridor01, ,cave_new_corridor03"), ("excludedRooms", "cave_divider"),
                    ("roomLimits", Map(("cave_new_corridor05", "min 2, max 4"))),
                    ("doorTypes", new List<object?> { Map(("prefab", "cloth_hanging_door_double"), ("connectionType", "shrine"), ("chance", "0.8")) }),
                    ("doorChance", "0.3"), ("interiorTransform", "true")),
                E(("name", "DG_Other")),
            ],
        };
        var dungeons = ExpandWorld.DungeonsOf(data)!.Dungeons;
        var cave = dungeons[0];
        Assert.Equal(("Dungeon", "128", "Cave", 20, 40, 0.3f, true, false), (cave.Algorithm, cave.Bounds, cave.Themes, cave.MinRooms, cave.MaxRooms, cave.DoorChance, cave.InteriorTransform, cave.AddBaseSeed));
        Assert.Equal(["cave_new_corridor01", "cave_new_corridor03"], cave.Required);
        Assert.Equal(["cave_divider"], cave.Excluded);
        Assert.Equal(new RoomLimit("cave_new_corridor05", 2, 4, 2), Assert.Single(cave.Limits));
        Assert.Equal(new DoorEntry("cloth_hanging_door_double", "shrine", 0.8f), Assert.Single(cave.Doors));
        // The mod's defaults: one room at least and at most.
        Assert.Equal((1, 1, ""), (dungeons[1].MinRooms, dungeons[1].MaxRooms, dungeons[1].Bounds));
    }

    [Theory]
    [InlineData("min 3", 3, 0, 3)]
    [InlineData("required 2, max 5", 0, 5, 2)]
    [InlineData("max 1, min 4, required 2", 4, 1, 4)]
    [InlineData("min, other 3, max x", 0, 0, 0)]
    public void RoomLimitsAreReadAsTheModReadsThem(string text, int min, int max, int required) =>
        Assert.Equal(new RoomLimit("r", min, max, required), ExpandWorld.LimitOf("r", text));
}
