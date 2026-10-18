using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>An opening's type as Claude reads it: the game's plain openings have none ("plain").</summary>
    internal static string TypeName(string type) => type.Length == 0 ? "plain" : type;

    /// <summary>A rotation's turn round the vertical, in degrees.</summary>
    private static float YawOf(System.Numerics.Quaternion q) => MathF.Round(BlueprintFormats.ToEuler(q).Y, 1);

    /// <summary>list_dungeons: the dungeons of the open area.</summary>
    [McpServerTool(Name = "list_dungeons", ReadOnly = true, Title = "The dungeons of the area")]
    [Description("The game's dungeons in the open area (Frost Caves, Burial Chambers, Sunken Crypts, Infested Mines…): the dungeon object's id, its kind, where it is (dungeons are 5000 m up, above their entrance), how many rooms and open ends it has. A dungeon nobody has been near is not laid out yet and has no rooms. Open the area round a dungeon's entrance (list_locations) to find it. dungeon_rooms gives one's rooms; generate_dungeon makes new ones of pieces instead.")]
    public Task<string> ListDungeons() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene || _w.InWorkshop)
        {
            return "Dungeons are listed in a world's area: open_area first.";
        }
        int reads = scene.Reads;
        var tool = new DungeonTool { Scene = scene };
        var dungeons = tool.All.Select(d =>
        {
            tool.Open(d);
            return new
            {
                id = d.Index,
                kind = DungeonTool.KindName(d.Kind).English,
                x = MathF.Round(d.Thing.Position.X, 1),
                y = MathF.Round(d.Thing.Position.Y, 1),
                z = MathF.Round(d.Thing.Position.Z, 1),
                rooms = d.Rooms.Count,
                openEnds = d.Problem == null ? tool.FreeEnds.Count : 0,
                problem = d.Problem?.English,
            };
        }).ToList();
        // A dungeon's id is its object's, one of the area's ids like list_objects gives (get_object, select_objects).
        if (dungeons.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(dungeons);
    });

    /// <summary>dungeon_rooms: a dungeon's rooms, free openings, door joints and the rooms it can take.</summary>
    [McpServerTool(Name = "dungeon_rooms", ReadOnly = true, Title = "A dungeon's rooms")]
    [Description("A dungeon of the open area (by id, list_dungeons): its rooms (index, name, place, yaw), its free openings where a room can join (index, type, place, whether it is the entrance's), the joints between rooms that can take a door (index, the two rooms, whether a door is there), how many pairs of rooms overlap, and the rooms of its kind it can take (name, the types of their openings, end caps: rooms of one opening that close a dead end). A room joins a free opening through one of its own openings of the same type.")]
    public Task<string> DungeonRoomList(
        [Description("The dungeon's id (list_dungeons).")] int id) => OnUi(() =>
    {
        var (tool, problem) = DungeonOf(id);
        if (tool?.Dungeon is not { } d)
        {
            return problem!;
        }
        return ToJson(new
        {
            id,
            kind = DungeonTool.KindName(d.Kind).English,
            rooms = d.Rooms.Select((r, i) => new { index = i, name = r.Name, x = MathF.Round(r.Position.X, 2), y = MathF.Round(r.Position.Y, 2), z = MathF.Round(r.Position.Z, 2), yaw = YawOf(r.Rotation) }),
            openEnds = tool.FreeEnds.Select((e, i) => new { index = i, type = TypeName(e.Type), room = e.Room, x = MathF.Round(e.Position.X, 2), y = MathF.Round(e.Position.Y, 2), z = MathF.Round(e.Position.Z, 2), entrance = e.Entrance }),
            doorJoints = tool.DoorJoints.Select((j, i) => new { index = i, rooms = new[] { j.First, j.Second }, type = TypeName(j.Type), door = tool.DoorsAt(j).Count > 0 }),
            overlappingPairs = tool.OverlappingPairs,
            canTake = tool.Palette.Select(r => new { name = r.Name, openings = r.Openings.Select(o => TypeName(o.Type)).Distinct(), endCap = r.EndCap, entrance = r.Entrance }),
        });
    });
}
