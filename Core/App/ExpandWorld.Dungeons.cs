using ValheimGen;

namespace TerrainEditor.App;

// The room and dungeon files' entries (expand_rooms.yaml, expand_dungeons.yaml).
public static partial class ExpandWorld
{
    /// <summary>
    /// The room and dungeon files' entries as the Dungeon tool takes them; null when both switches are off (the rooms
    /// the game's when the room file has none). A room that is no game room nor a copy of one (a blueprint room: the editor does not build those) is left out and
    /// noted, as are an added object that is no prefab of the game and a swap or data that cannot be written ahead.
    /// </summary>
    /// <param name="data">Expand World Data's files.</param>
    internal static DungeonRules? DungeonsOf(DataFiles data)
    {
        if (data.Rooms == null && data.Dungeons == null)
        {
            return null;
        }
        var table = new DataTable(data.DataEntries);
        return new DungeonRules
        {
            // No file: the game's rooms (the mod writes them as its file).
            Rooms = data.Rooms is { Count: > 0 } rooms ? [.. rooms.Select(e => RoomOf(new Fields(e, "rooms", data.Notes), table, data.Notes)).OfType<RoomEntry>()] : null,
            Dungeons = data.Dungeons is { } dungeons ? [.. dungeons.Select(e => DungeonOf(new Fields(e, "dungeons", data.Notes), table, data.Notes))] : [],
        };
    }

    /// <summary>One room of the room file, with the mod's defaults (RoomYaml); null for a blueprint room, noted.</summary>
    private static RoomEntry? RoomOf(Fields f, DataTable table, List<string> notes)
    {
        string name = f.Str("name");
        string game = name.Split(':')[0];
        if (Editing.Dungeons.RoomOf(game) == null)
        {
            notes.Add($"expand_rooms: {name} is a blueprint room (no game room of that name): the Dungeon tool does not build it");
            return null;
        }
        List<string> why = [];
        var objects = Objects(f.Rows("objects"), table, why);
        var swaps = Swaps(f.Rows("objectSwap"), why);
        var objectData = DataChoices(f.Rows("objectData"), table, why);
        notes.AddRange(why.Select(w => $"expand_rooms: {name}: {w} is left out"));
        List<RoomConnection> connections = [];
        if (f.Raw("connections") is List<object?> items)
        {
            foreach (var c in items.OfType<Dictionary<string, object?>>().Select(f.Of))
            {
                connections.Add(new RoomConnection(c.Str("position"), c.Str("type"), c.B("entrance", false), c.B("exitOnly", false), c.Str("door", "true")));
            }
        }
        return new RoomEntry(name, f.Str("theme"), f.B("enabled", true), f.B("entrance", false), f.B("endCap", false), f.B("divider", false),
            f.I("endCapPriority", 0), f.F("weight", 1f), f.B("perimeter", false), f.Str("size"), connections, objects)
        { Swaps = swaps, Data = objectData, MinPlaceOrder = f.I("minPlaceOrder", 0) };
    }

    /// <summary>One generator of the dungeon file, with the mod's defaults (DungeonYaml); a swap or data it cannot write left out, noted.</summary>
    private static DungeonEntry DungeonOf(Fields f, DataTable table, List<string> notes)
    {
        List<string> why = [];
        var swaps = Swaps(f.Rows("objectSwap"), why);
        var objectData = DataChoices(f.Rows("objectData"), table, why);
        notes.AddRange(why.Select(w => $"expand_dungeons: {f.Str("name")}: {w} is left out"));
        List<DoorEntry> doors = [];
        if (f.Raw("doorTypes") is List<object?> items)
        {
            foreach (var d in items.OfType<Dictionary<string, object?>>().Select(f.Of))
            {
                doors.Add(new DoorEntry(d.Str("prefab"), d.Str("connectionType"), d.F("chance", 0f)));
            }
        }
        List<RoomLimit> limits = [];
        if (f.Raw("roomLimits") is Dictionary<string, object?> map)
        {
            limits.AddRange(map.Select(kv => LimitOf(kv.Key, kv.Value as string ?? "")));
        }
        return new DungeonEntry(f.Str("name"), f.Str("algorithm"), f.Str("bounds"), f.Str("themes"), f.I("minRooms", 1), f.I("maxRooms", 1),
            NameList(f.Str("requiredRooms")), NameList(f.Str("excludedRooms")), limits, doors, f.F("doorChance", 0f), f.B("interiorTransform", false),
            f.B("addBaseSeedToRandomSpawn", false))
        { Swaps = swaps, Data = objectData };
    }

    /// <summary>
    /// A room's limits as the mod reads them (ParseRoomLimit): "min n", "max n" and "required n" separated by ",", the
    /// required count at least the min; an item it cannot read passed over.
    /// </summary>
    /// <param name="room">The room's name.</param>
    /// <param name="text">The limits.</param>
    internal static RoomLimit LimitOf(string room, string text)
    {
        int min = 0, max = 0, required = 0;
        foreach (string[] parts in NameList(text).Select(i => i.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length >= 2))
        {
            int n = Int(parts[1]) ?? 0;
            switch (parts[0].ToLowerInvariant())
            {
                case "min":
                    min = n;
                    break;
                case "max":
                    max = n;
                    break;
                case "required":
                    required = n;
                    break;
                default:
                    // Another word: the mod passes it over.
                    break;
            }
            required = Math.Max(required, min);
        }
        return new RoomLimit(room, min, max, required);
    }

    /// <summary>A list as the mod reads one (DataManager.ToList): split at ",", each item trimmed, blank ones left out.</summary>
    private static List<string> NameList(string text) => [.. text.Split(',').Select(s => s.Trim()).Where(s => s != "")];
}
