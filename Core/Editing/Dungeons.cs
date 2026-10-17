using System.Numerics;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>
/// The game's dungeons (Frost Caves, crypts, Dvergr mines...) as the editor's Dungeon tool edits them.
/// A dungeon is one object (DG_Cave, DG_SunkenCrypt...) whose byte array "roomData" lists its rooms: the
/// stable hash of each room prefab's name, its position and rotation. The game builds the rooms from
/// that list when the dungeon loads (DungeonGenerator.Load/Spawn) and never checks it against its own
/// generator, so a list made here is built as it is. Rooms and their openings come from the player's
/// Valheim (GameData; the tests use tests/fixtures/dungeon-rooms.json). All in Unity's world space.
/// </summary>
public static partial class Dungeons
{
    /// <summary>The object data key of a dungeon's room list ("roomData").</summary>
    public static readonly int RoomDataKey = StableHash.Of("roomData");

    /// <summary>
    /// One opening of a room, in the room's own frame. Two rooms join where openings of the same type
    /// meet at one point, facing each other.
    /// </summary>
    /// <param name="Type">Its connection type; only openings of the same type join.</param>
    /// <param name="Entrance">The entrance room's way in, from outside the dungeon.</param>
    /// <param name="AllowDoor">A door can stand there.</param>
    /// <param name="Position">Where it is in the room's frame.</param>
    /// <param name="Rotation">Which way it faces in the room's frame.</param>
    /// <param name="OnlyIfOther">A door only when the other room's opening allows one too.</param>
    public sealed record Opening(string Type, bool Entrance, bool AllowDoor, Vector3 Position, Quaternion Rotation, bool OnlyIfOther = false);

    /// <summary>A networked object the game makes with a room when it first generates it.</summary>
    /// <param name="Prefab">The object's prefab name.</param>
    /// <param name="Position">In the room's frame.</param>
    /// <param name="Rotation">In the room's frame.</param>
    /// <param name="Node">Where it sits in the room's tree of random parts (-1: on the room itself).</param>
    public sealed record Content(string Prefab, Vector3 Position, Quaternion Rotation, int Node);

    /// <summary>
    /// RandomSpawn: the node is there with this chance (%) when the dungeon has the theme (0: any) and it
    /// is within the heights; when not, Off (a node, -1 none) is shown instead.
    /// </summary>
    /// <param name="Node">The part it shows or hides.</param>
    /// <param name="Chance">Its chance (%).</param>
    /// <param name="Theme">The dungeon theme flags it needs (0: any).</param>
    /// <param name="Off">The node shown when it is not (-1 none).</param>
    /// <param name="MinY">Lowest room height it is there at (metres).</param>
    /// <param name="MaxY">Highest room height it is there at (metres).</param>
    public sealed record Spawn(int Node, float Chance, int Theme, int Off, int MinY, int MaxY);

    /// <summary>RandomObject: one of the choices (node, weight) is kept, the others are not.</summary>
    /// <param name="Node">Its own node, hidden when no choice is kept (wrong theme or height).</param>
    /// <param name="Theme">The dungeon theme flags it needs (0: any).</param>
    /// <param name="Choices">The nodes to choose from and their weights.</param>
    /// <param name="MinY">Lowest room height a choice is kept at (metres).</param>
    /// <param name="MaxY">Highest room height a choice is kept at (metres).</param>
    public sealed record Pick(int Node, int Theme, (int Node, float Weight)[] Choices, int MinY, int MaxY);

    /// <summary>A room prefab of the game's dungeons, as GameData's dungeon scan describes it.</summary>
    /// <param name="Name">Its prefab name.</param>
    /// <param name="Hash">The stable hash of its name, as in a room list.</param>
    /// <param name="Theme">Its theme flags; it can go in dungeons sharing one.</param>
    /// <param name="Size">Its box (metres, Room.m_size), centred on its position.</param>
    /// <param name="EndCap">Closes an open end.</param>
    /// <param name="Entrance">An entrance room, the first of a dungeon.</param>
    /// <param name="Divider">The game's m_divider; not joined by the generator here.</param>
    /// <param name="EndCapPrio">The game's m_endCapPrio; the Dungeon tool's Close open ends picks the highest that fits.</param>
    /// <param name="Weight">How often it is picked against the others (Room.m_weight).</param>
    /// <param name="Perimeter">The game's m_perimeter (kept, not used here).</param>
    /// <param name="Enabled">The game uses it; disabled rooms are left out of RoomsFor.</param>
    /// <param name="Openings">Where other rooms join it.</param>
    /// <param name="Contents">The networked objects it makes.</param>
    /// <param name="Spawns">Its RandomSpawn parts, rolled in order.</param>
    /// <param name="Picks">Its RandomObject parts, rolled after the spawns.</param>
    /// <param name="Nodes">Each random part's parent node (-1: the room itself).</param>
    public sealed record Room(string Name, int Hash, int Theme, Vector3 Size, bool EndCap, bool Entrance, bool Divider, int EndCapPrio,
        float Weight, bool Perimeter, bool Enabled, Opening[] Openings, Content[] Contents, Spawn[] Spawns, Pick[] Picks, int[] Nodes);

    /// <summary>A kind of dungeon (the DG_ prefab).</summary>
    /// <param name="Name">The DG_ prefab's name.</param>
    /// <param name="Hash">The stable hash of its name (the object's prefab).</param>
    /// <param name="Themes">Its room themes (flags).</param>
    /// <param name="Interior">
    /// Built in the sky above its entrance (DungeonGenerator.Algorithm.Dungeon), not laid out on the ground
    /// (camps, villages).
    /// </param>
    /// <param name="ZoneSize">The box its rooms stay in, around the zone's centre at the dungeon's height (metres).</param>
    /// <param name="CustomInterior">Rooms are rolled by their place relative to the dungeon (m_useCustomInteriorTransform).</param>
    /// <param name="BaseSeed">The dungeon's seed is added to each room's roll (m_addBaseSeedToRandomSpawn).</param>
    public sealed record Kind(string Name, int Hash, int Themes, bool Interior, Vector3 ZoneSize, bool CustomInterior = false, bool BaseSeed = false)
    {
        /// <summary>Its door types (prefab, opening type, chance; 0 takes DoorChance).</summary>
        public (string Prefab, string Type, float Chance)[] Doors { get; init; } = [];

        /// <summary>The chance (0 to 1) of a door whose type gives none.</summary>
        public float DoorChance { get; init; } = 0.5f;
    }

    /// <summary>The rooms and dungeon kinds read, by name hash.</summary>
    private sealed record Catalog(Dictionary<int, Room> Rooms, Dictionary<int, Kind> Kinds);

    /// <summary>None until Use: no world opens before then (GameData).</summary>
    private static volatile Catalog _all = new([], []);

    /// <summary>Takes the dungeons and rooms as GameCatalogues' DungeonScan writes them, in place of any before.</summary>
    public static void Use(string json) => _all = Load(json);

    /// <summary>The room of this name hash, or null when it is not known.</summary>
    public static Room? RoomOf(int hash) => _all.Rooms.GetValueOrDefault(hash);

    /// <summary>The room of this prefab name, or null when it is not known.</summary>
    public static Room? RoomOf(string name) => RoomOf(StableHash.Of(name));

    /// <summary>The dungeon kind of an object's prefab hash, or null when it is not a dungeon.</summary>
    public static Kind? KindOf(int prefab) => _all.Kinds.GetValueOrDefault(prefab);

    /// <summary>Every dungeon kind, by name.</summary>
    public static IEnumerable<Kind> Kinds => _all.Kinds.Values.OrderBy(k => k.Name, StringComparer.Ordinal);

    /// <summary>Every room, by name.</summary>
    public static IEnumerable<Room> AllRooms => _all.Rooms.Values.OrderBy(r => r.Name, StringComparer.Ordinal);

    /// <summary>
    /// The rooms a dungeon of this kind can hold: those of its themes, the game's way
    /// (DungeonGenerator.SetupAvailableRooms: enabled rooms whose theme shares a flag with the dungeon's).
    /// </summary>
    public static IEnumerable<Room> RoomsFor(Kind kind) => AllRooms.Where(r => r.Enabled && (r.Theme & kind.Themes) != 0);

    /// <summary>Three numbers of a scan's JSON array from <paramref name="i"/> on, as a point.</summary>
    internal static Vector3 V(JsonElement a, int i) => new(a[i].GetSingle(), a[i + 1].GetSingle(), a[i + 2].GetSingle());

    /// <summary>Four numbers of a scan's JSON array from <paramref name="i"/> on, as a rotation (x, y, z, w).</summary>
    internal static Quaternion Q(JsonElement a, int i) => new(a[i].GetSingle(), a[i + 1].GetSingle(), a[i + 2].GetSingle(), a[i + 3].GetSingle());
}
