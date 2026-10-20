namespace ValheimGen;

/// <summary>
/// Expand World Data's room and dungeon files (expand_rooms.yaml, expand_dungeons.yaml) as the Dungeon tool takes
/// them: the rooms in place of the game's list, and the dungeon generators' settings in place of their prefabs'.
/// </summary>
public sealed record DungeonRules
{
    /// <summary>The room file's rooms, in its order; null for the game's (its switch off).</summary>
    public IReadOnlyList<RoomEntry>? Rooms { get; init; }

    /// <summary>The dungeon file's generators (the game's others stay as they are).</summary>
    public IReadOnlyList<DungeonEntry> Dungeons { get; init; } = [];
}

/// <summary>
/// One room of the room file (the mod's RoomYaml): a room of the game by its name, or a copy of one ("name:suffix"),
/// with every value the file gives (a value left out is the mod's default).
/// </summary>
/// <param name="Name">The room's name; a copy's is its game room's name, ":" and a suffix.</param>
/// <param name="Theme">Its themes, names or numbers separated by ",".</param>
/// <param name="Enabled">Whether dungeons may use it.</param>
/// <param name="Entrance">Used only as the first room.</param>
/// <param name="EndCap">Used to close open ends.</param>
/// <param name="Divider">Used to close openings that do not match.</param>
/// <param name="EndCapPriority">End caps of a higher priority are tried first.</param>
/// <param name="Weight">How often it is picked.</param>
/// <param name="Perimeter">Placed on a camp's edge.</param>
/// <param name="Size">Its size as the file writes it ("x, z, y" metres; empty: none).</param>
/// <param name="Connections">Its openings, by index over the game room's.</param>
/// <param name="Objects">The objects added with it.</param>
public sealed record RoomEntry(string Name, string Theme, bool Enabled, bool Entrance, bool EndCap, bool Divider, int EndCapPriority, float Weight, bool Perimeter,
    string Size, IReadOnlyList<RoomConnection> Connections, IReadOnlyList<ContentObject> Objects)
{
    /// <summary>How many rooms from the entrance it may join at the fewest ("minPlaceOrder").</summary>
    public int MinPlaceOrder { get; init; }

    /// <summary>The blueprint it is built of, when it is a blueprint room (no game room of its name); else null.</summary>
    public RoomBlueprint? Blueprint { get; init; }

    /// <summary>Its objects' swaps and data ("objectSwap", "objectData").</summary>
    public ObjectChanges Changes { get; init; } = ObjectChanges.None;
}

/// <summary>One opening of a room of the room file (the mod's RoomConnectionYaml).</summary>
/// <param name="Position">Its place and turn, "x, z, y, turn y, turn x, turn z" in the room's frame; empty: the game room's.</param>
/// <param name="Type">Its connection type.</param>
/// <param name="Entrance">The entrance room's way in.</param>
/// <param name="ExitOnly">Only for joining rooms to it once it is placed, never for joining it to another.</param>
/// <param name="Door">"true": a door may stand there; "other": only when the other opening allows one too; else none.</param>
public sealed record RoomConnection(string Position, string Type, bool Entrance, bool ExitOnly, string Door);

/// <summary>One dungeon generator of the dungeon file (the mod's DungeonYaml), as the Dungeon tool uses it.</summary>
/// <param name="Name">The generator's prefab (DG_...).</param>
/// <param name="Algorithm">"Dungeon", "CampGrid" or "CampRadial".</param>
/// <param name="Bounds">Its box, "x, z, y" or one size (empty: 64 wide, 256 high).</param>
/// <param name="Themes">Its room themes, names separated by ",".</param>
/// <param name="MinRooms">The fewest rooms.</param>
/// <param name="MaxRooms">The most rooms.</param>
/// <param name="Required">Rooms it must have.</param>
/// <param name="Excluded">Rooms it never takes.</param>
/// <param name="Limits">Per room: the fewest, the most and how many it must have.</param>
/// <param name="Doors">Its doors.</param>
/// <param name="DoorChance">The chance of a door whose type gives none.</param>
/// <param name="InteriorTransform">Rooms rolled by their place from the dungeon's (m_useCustomInteriorTransform).</param>
/// <param name="AddBaseSeed">The dungeon's seed added to its rooms' rolls.</param>
public sealed record DungeonEntry(string Name, string Algorithm, string Bounds, string Themes, int MinRooms, int MaxRooms, IReadOnlyList<string> Required,
    IReadOnlyList<string> Excluded, IReadOnlyList<RoomLimit> Limits, IReadOnlyList<DoorEntry> Doors, float DoorChance, bool InteriorTransform, bool AddBaseSeed)
{
    /// <summary>Its objects' swaps and data ("objectSwap", "objectData").</summary>
    public ObjectChanges Changes { get; init; } = ObjectChanges.None;
}

/// <summary>A dungeon's limits on one room (the mod's DungeonRoomLimit).</summary>
/// <param name="Room">The room's name.</param>
/// <param name="Min">The fewest (0: none).</param>
/// <param name="Max">The most (0: none).</param>
/// <param name="Required">How many it must have (0: none).</param>
public sealed record RoomLimit(string Room, int Min, int Max, int Required);

/// <summary>A door of a dungeon (the mod's DungeonDoorYaml).</summary>
/// <param name="Prefab">The door's prefab.</param>
/// <param name="Type">The connection type it stands in.</param>
/// <param name="Chance">Its chance (0: the dungeon's door chance).</param>
public sealed record DoorEntry(string Prefab, string Type, float Chance);

/// <summary>How Expand World Data's file changes the objects of a room or a dungeon ("objectSwap", "objectData").</summary>
public sealed record ObjectChanges
{
    /// <summary>No change.</summary>
    public static readonly ObjectChanges None = new();

    /// <summary>The swaps, by prefab: what each may become, with its weight; an empty name removes it.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Weighted<string>>> Swaps { get; init; } = new Dictionary<string, IReadOnlyList<Weighted<string>>>();

    /// <summary>The data, by prefab or "all": the data each may get, with its weight.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>> Data { get; init; } = new Dictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>>();

    /// <summary>Whether it changes anything.</summary>
    public bool Any => Swaps.Count > 0 || Data.Count > 0;
}

/// <summary>
/// The blueprint a blueprint room of the room file is built of (a room whose name is no game room's), as the mod loads it
/// for the room.
/// </summary>
/// <param name="Name">The blueprint's name.</param>
/// <param name="Blueprints">It and the blueprints it nests, by name, each as its centred parts.</param>
/// <param name="SnapPoints">Where the room's openings are, in its frame, by index (the file's, or the room's connections').</param>
/// <param name="Size">Its pieces' bounds (the origin among them): the room's size when the file gives none.</param>
public sealed record RoomBlueprint(string Name, IReadOnlyDictionary<string, IReadOnlyList<BlueprintPart>> Blueprints,
    IReadOnlyList<(System.Numerics.Vector3 Position, System.Numerics.Quaternion Rotation)> SnapPoints, System.Numerics.Vector3 Size);
