using System.Numerics;
using System.Text.Json;
using TerrainEditor.App;
using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Editing;

/// <summary>
/// The game's dungeons (Frost Caves, crypts, Dvergr mines...) as the editor's Dungeon tool edits them.
/// A dungeon is one object (DG_Cave, DG_SunkenCrypt...) whose byte array "roomData" lists its rooms: the
/// stable hash of each room prefab's name, its position and rotation. The game builds the rooms from
/// that list when the dungeon loads (DungeonGenerator.Load/Spawn) and never checks it against its own
/// generator, so a list made here is built as it is. Rooms and their openings come from the player's
/// Valheim (GameData; the tests use tests/fixtures/dungeon-rooms.json). All in Unity's world space.
/// </summary>
public static class Dungeons
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

    /// <summary>Reads the scan's JSON ("rooms" and "dungeons", each by prefab name).</summary>
    private static Catalog Load(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var rooms = new Dictionary<int, Room>();
        foreach (JsonProperty p in doc.RootElement.GetProperty("rooms").EnumerateObject())
        {
            JsonElement r = p.Value;
            bool B(string k) => r.GetProperty(k).GetInt32() != 0;
            var (contents, spawns, picks, nodes) = Parts(r);
            rooms[StableHash.Of(p.Name)] = new Room(p.Name, StableHash.Of(p.Name), r.GetProperty("theme").GetInt32(), V(r.GetProperty("size"), 0),
                B("endCap"), B("entrance"), B("divider"), r.GetProperty("endCapPrio").GetInt32(), r.GetProperty("weight").GetSingle(), B("perimeter"), B("enabled"),
                [.. r.GetProperty("openings").EnumerateArray().Select(o => new Opening(o[0].GetString() ?? "", o[1].GetInt32() != 0, o[2].GetInt32() != 0, V(o, 4), Q(o, 7), o[3].GetInt32() != 0))],
                contents, spawns, picks, nodes);
        }
        var kinds = new Dictionary<int, Kind>();
        foreach (JsonProperty p in doc.RootElement.GetProperty("dungeons").EnumerateObject())
        {
            JsonElement d = p.Value;
            kinds[StableHash.Of(p.Name)] = new Kind(p.Name, StableHash.Of(p.Name), d.GetProperty("themes").GetInt32(), d.GetProperty("algorithm").GetInt32() == 0, V(d.GetProperty("zoneSize"), 0),
                d.GetProperty("customInterior").GetInt32() != 0, d.GetProperty("baseSeed").GetInt32() != 0)
            {
                Doors = [.. d.GetProperty("doors").EnumerateArray().Select(x => (x[0].GetString() ?? "", x[1].GetString() ?? "", x[2].GetSingle()))],
                DoorChance = d.GetProperty("doorChance").GetSingle(),
            };
        }
        return new Catalog(rooms, kinds);
    }

    /// <summary>
    /// The contents, spawns, picks and nodes of a room's or a location's JSON entry (what the scan writes of a
    /// prefab's tree).
    /// </summary>
    internal static (Content[] Contents, Spawn[] Spawns, Pick[] Picks, int[] Nodes) Parts(JsonElement r)
    {
        return ([.. r.GetProperty("contents").EnumerateArray().Select(c => new Content(c[0].GetString() ?? "", V(c, 1), Q(c, 4), c[8].GetInt32()))],
            [.. r.GetProperty("spawns").EnumerateArray().Select(x => new Spawn(x[0].GetInt32(), x[1].GetSingle(), x[2].GetInt32(), x[3].GetInt32(), x[4].GetInt32(), x[5].GetInt32()))],
            [.. r.GetProperty("picks").EnumerateArray().Select(x => new Pick(x[0].GetInt32(), x[1].GetInt32(),
                [.. x[2].EnumerateArray().Select(c => (c[0].GetInt32(), c[1].GetSingle()))], x[3].GetInt32(), x[4].GetInt32()))],
            [.. r.GetProperty("nodes").EnumerateArray().Select(n => n.GetInt32())]);
    }

    // ---- The room list.
    /// <summary>One entry of a dungeon's room list: a room where it stands.</summary>
    /// <param name="Hash">The stable hash of the room prefab's name.</param>
    /// <param name="Position">World space.</param>
    /// <param name="Rotation">World space.</param>
    public sealed record Placed(int Hash, Vector3 Position, Quaternion Rotation)
    {
        /// <summary>The room, or null when the hash is not a known room.</summary>
        public Room? Room => RoomOf(Hash);

        /// <summary>The room's name, or its hash when it is not known.</summary>
        public string Name => Room?.Name ?? Hash.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>An opening in world space: where it is and which way it faces.</summary>
        public (Vector3 Position, Quaternion Rotation) At(Opening o) => (Position + Vector3.Transform(o.Position, Rotation), Rotation * o.Rotation);

        /// <summary>A point of the room's own frame in world space.</summary>
        public Vector3 ToWorld(Vector3 local) => Position + Vector3.Transform(local, Rotation);
    }

    /// <summary>
    /// Reads a room list as DungeonGenerator.Save writes it: an int count, then per room its hash (int),
    /// position (3 floats) and rotation (Euler angles, 3 floats).
    /// </summary>
    /// <exception cref="InvalidDataException">The count does not fit the bytes.</exception>
    public static List<Placed> Read(byte[] data)
    {
        using var r = new BinaryReader(new MemoryStream(data));
        int n = r.ReadInt32();
        if (n < 0 || n > (data.Length - 4) / 28)
        {
            throw new InvalidDataException($"The dungeon's room list is damaged ({n} rooms in {data.Length} bytes).");
        }
        var list = new List<Placed>(n);
        for (int i = 0; i < n; i++)
        {
            int hash = r.ReadInt32();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var euler = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            list.Add(new Placed(hash, pos, BlueprintFormats.FromEuler(euler)));
        }
        return list;
    }

    /// <summary>Writes a room list the way the game saves it (see <see cref="Read"/>).</summary>
    public static byte[] Write(IReadOnlyList<Placed> rooms)
    {
        var ms = new MemoryStream(4 + rooms.Count * 28);
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(rooms.Count);
            foreach (Placed p in rooms)
            {
                Vector3 e = BlueprintFormats.ToEuler(p.Rotation);
                w.Write(p.Hash);
                w.Write(p.Position.X);
                w.Write(p.Position.Y);
                w.Write(p.Position.Z);
                w.Write(e.X);
                w.Write(e.Y);
                w.Write(e.Z);
            }
        }
        return ms.ToArray();
    }

    // ---- Joining rooms.
    /// <summary>
    /// Joins a room the game's way (DungeonGenerator.PlaceRoom + CalculateRoomPosRot): the new room is
    /// turned so that its opening <paramref name="mine"/> faces the other way from the open one, and moved
    /// so the two meet.
    /// </summary>
    public static Placed Attach(Room room, Opening mine, Vector3 openPosition, Quaternion openRotation)
    {
        Quaternion exit = openRotation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        Quaternion rot = exit * Quaternion.Inverse(mine.Rotation);
        Vector3 pos = openPosition - Vector3.Transform(mine.Position, rot);
        return new Placed(room.Hash, pos, Quaternion.Normalize(rot));
    }

    /// <summary>
    /// Places a dungeon's first room the game's way (DungeonGenerator.PlaceStartRoom): its way in (the entrance
    /// opening, else its first opening, else its own origin) at the dungeon object, facing the way the object does.
    /// </summary>
    /// <param name="room">The first room.</param>
    /// <param name="position">The dungeon object's place.</param>
    /// <param name="rotation">The dungeon object's turn.</param>
    public static Placed PlaceStart(Room room, Vector3 position, Quaternion rotation)
    {
        var way = room.Openings.FirstOrDefault(o => o.Entrance) ?? room.Openings.FirstOrDefault();
        if (way == null)
        {
            return new Placed(room.Hash, position, Quaternion.Normalize(rotation));
        }
        Quaternion rot = rotation * Quaternion.Inverse(way.Rotation);
        return new Placed(room.Hash, position - Vector3.Transform(way.Position, rot), Quaternion.Normalize(rot));
    }

    /// <summary>
    /// The openings a room can join an open one of this type with: those of the same type (an end cap's
    /// only opening too), never the entrance's way in.
    /// </summary>
    public static IEnumerable<Opening> Matching(Room room, string type) => room.Openings.Where(o => !o.Entrance && o.Type == type);

    /// <summary>An opening of a placed room, in world space.</summary>
    /// <param name="Room">The index of its room in the list.</param>
    /// <param name="Index">The index of the opening in its room's openings.</param>
    /// <param name="Position">Where it is.</param>
    /// <param name="Rotation">Which way it faces.</param>
    /// <param name="Type">Its connection type.</param>
    /// <param name="Entrance">The entrance room's way in.</param>
    public sealed record OpenEnd(int Room, int Index, Vector3 Position, Quaternion Rotation, string Type, bool Entrance);

    /// <summary>
    /// The openings of the placed rooms (unknown rooms have none); with <paramref name="freeOnly"/>, only
    /// those no other room meets (RoomConnection.TestContact: within 0.1 m), never the entrance's way in.
    /// </summary>
    public static List<OpenEnd> Openings(IReadOnlyList<Placed> rooms, bool freeOnly)
    {
        var all = new List<OpenEnd>();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room? room = rooms[i].Room;
            if (room == null)
            {
                continue;
            }
            for (int k = 0; k < room.Openings.Length; k++)
            {
                var (p, q) = rooms[i].At(room.Openings[k]);
                all.Add(new OpenEnd(i, k, p, q, room.Openings[k].Type, room.Openings[k].Entrance));
            }
        }
        if (!freeOnly)
        {
            return all;
        }
        // The entrance room's way in leads out of the dungeon: it never takes a room.
        return [.. all.Where(a => !a.Entrance && !all.Any(b => b.Room != a.Room && Vector3.DistanceSquared(a.Position, b.Position) < 0.01f))];
    }

    // ---- Doors.
    /// <summary>
    /// Where two rooms meet, the game may put a door (DungeonGenerator.PlaceDoors): at the opening of the
    /// room that was there first, facing as it does, when that opening allows a door (and, OnlyIfOther, the
    /// new room's does too), and not where an end cap closes it.
    /// </summary>
    /// <param name="First">The index of the room that was there first.</param>
    /// <param name="Second">The index of the room that joined it.</param>
    /// <param name="Position">The first room's opening, in world space.</param>
    /// <param name="Rotation">Which way the first room's opening faces.</param>
    /// <param name="Type">The openings' connection type.</param>
    /// <param name="DoorAllowed">Whether a door may stand there.</param>
    public sealed record Joint(int First, int Second, Vector3 Position, Quaternion Rotation, string Type, bool DoorAllowed);

    /// <summary>Every place where two placed rooms' openings meet (within 0.1 m).</summary>
    public static List<Joint> Joints(IReadOnlyList<Placed> rooms)
    {
        var joints = new List<Joint>();
        var ends = Openings(rooms, freeOnly: false);
        foreach (var a in ends)
        {
            foreach (var b in ends)
            {
                if (b.Room <= a.Room || Vector3.DistanceSquared(a.Position, b.Position) >= 0.01f)
                {
                    continue;
                }
                var oa = rooms[a.Room].Room!.Openings[a.Index];
                var ob = rooms[b.Room].Room!.Openings[b.Index];
                bool allowed = !rooms[b.Room].Room!.EndCap && oa.AllowDoor && (!oa.OnlyIfOther || ob.AllowDoor);
                joints.Add(new Joint(a.Room, b.Room, a.Position, a.Rotation, a.Type, allowed));
            }
        }
        return joints;
    }

    /// <summary>The door types a joint of this type can take in this kind of dungeon.</summary>
    public static IEnumerable<(string Prefab, string Type, float Chance)> DoorsFor(Kind kind, string type) => kind.Doors.Where(d => d.Type == type);

    /// <summary>The game's roll for a door at a joint (FindDoorType, then its chance).</summary>
    /// <param name="kind">The dungeon kind, for its door types.</param>
    /// <param name="joint">Where the rooms meet.</param>
    /// <param name="random">Numbers from 0 to 1; called twice when a door can stand there.</param>
    /// <returns>The door's prefab, or null.</returns>
    public static string? RollDoor(Kind kind, Joint joint, Func<float> random)
    {
        var types = DoorsFor(kind, joint.Type).ToList();
        if (!joint.DoorAllowed || types.Count == 0)
        {
            return null;
        }
        var door = types[Math.Min(types.Count - 1, (int)(random() * types.Count))];
        float chance = door.Chance > 0 ? door.Chance : kind.DoorChance;
        return random() <= chance ? door.Prefab : null;
    }

    // ---- Room boxes.
    /// <summary>
    /// A room's box (Room.m_size, centred on the room's position, turned with it), for picking and for
    /// the game's overlap test (DungeonGenerator.TestCollision: the new room's box 0.1 m smaller). An
    /// unknown room gets a 4 m cube.
    /// </summary>
    public static (Vector3 Center, Vector3 Half, Quaternion Rotation) Box(Placed p)
    {
        Vector3 size = p.Room?.Size ?? new Vector3(4);
        return (p.Position, size / 2f, p.Rotation);
    }

    /// <summary>
    /// Indexes of the placed rooms the box of <paramref name="p"/> (at index <paramref name="skip"/>, -1 for
    /// a new one) overlaps; unknown rooms and rooms without a floor size never count.
    /// </summary>
    public static List<int> Overlaps(IReadOnlyList<Placed> rooms, Placed p, int skip = -1)
    {
        var hits = new List<int>();
        Room? room = p.Room;
        if (room == null || room.Size.X == 0 || room.Size.Z == 0)
        {
            return hits;
        }
        var a = (p.Position, (room.Size - new Vector3(0.1f)) / 2f, p.Rotation);
        for (int i = 0; i < rooms.Count; i++)
        {
            if (i == skip || rooms[i].Room is not { } other || other.Size.X == 0 || other.Size.Z == 0)
            {
                continue;
            }
            if (BoxesOverlap(a, (rooms[i].Position, other.Size / 2f, rooms[i].Rotation)))
            {
                hits.Add(i);
            }
        }
        return hits;
    }

    /// <summary>Separating axis test of two turned boxes (centre, half size, rotation); touching faces do not count.</summary>
    public static bool BoxesOverlap((Vector3 C, Vector3 H, Quaternion R) a, (Vector3 C, Vector3 H, Quaternion R) b)
    {
        Vector3[] ax = [Vector3.Transform(Vector3.UnitX, a.R), Vector3.Transform(Vector3.UnitY, a.R), Vector3.Transform(Vector3.UnitZ, a.R)];
        Vector3[] bx = [Vector3.Transform(Vector3.UnitX, b.R), Vector3.Transform(Vector3.UnitY, b.R), Vector3.Transform(Vector3.UnitZ, b.R)];
        Vector3 d = b.C - a.C;
        float[] ha = [a.H.X, a.H.Y, a.H.Z], hb = [b.H.X, b.H.Y, b.H.Z];
        bool Separated(Vector3 axis)
        {
            if (axis.LengthSquared() < 1e-8f)
            {
                return false;
            }
            float ra = 0, rb = 0;
            for (int i = 0; i < 3; i++)
            {
                ra += ha[i] * MathF.Abs(Vector3.Dot(ax[i], axis));
                rb += hb[i] * MathF.Abs(Vector3.Dot(bx[i], axis));
            }
            // Touching faces do not overlap (rooms that join share a wall plane).
            return MathF.Abs(Vector3.Dot(d, axis)) >= ra + rb - 1e-3f * axis.Length();
        }
        for (int i = 0; i < 3; i++)
        {
            if (Separated(ax[i]) || Separated(bx[i]))
            {
                return false;
            }
        }
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (Separated(Vector3.Cross(ax[i], bx[j])))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Whether a room stays inside the dungeon's box (DungeonGenerator.IsInsideDungeon: all eight corners
    /// of its box within m_zoneSize around the zone's centre, at the dungeon's height). An unknown room does.
    /// </summary>
    public static bool Inside(Kind kind, Vector3 dungeonPosition, Placed p)
    {
        if (p.Room is not { } room)
        {
            return true;
        }
        const float Zone = 64f;
        var center = new Vector3(MathF.Floor((dungeonPosition.X + Zone / 2) / Zone) * Zone, dungeonPosition.Y, MathF.Floor((dungeonPosition.Z + Zone / 2) / Zone) * Zone);
        Vector3 min = center - kind.ZoneSize / 2, max = center + kind.ZoneSize / 2;
        Vector3 h = room.Size / 2;
        foreach (float sx in new[] { -1f, 1f })
        {
            foreach (float sy in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 c = p.ToWorld(new Vector3(sx * h.X, sy * h.Y, sz * h.Z));
                    if (c.X < min.X || c.Y < min.Y || c.Z < min.Z || c.X > max.X || c.Y > max.Y || c.Z > max.Z)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    // ---- What the game makes with a room when it generates it.
    /// <summary>An object the game makes with a room, in world space.</summary>
    /// <param name="Prefab">The object's prefab name.</param>
    /// <param name="Position">World space.</param>
    /// <param name="Rotation">World space.</param>
    public sealed record Made(string Prefab, Vector3 Position, Quaternion Rotation);

    /// <summary>The dungeon's seed (DungeonGenerator.GetSeed): the world's seed and the dungeon's zone and place.</summary>
    public static int Seed(int worldSeed, Vector3 dungeonPosition)
    {
        var (zx, zz) = ChunkMath.SectorOf(dungeonPosition.X, dungeonPosition.Z);
        return unchecked(worldSeed + zx * 4271 + zz * -7187 + (int)dungeonPosition.X * -4271 + (int)dungeonPosition.Y * 9187 + (int)dungeonPosition.Z * -2134);
    }

    /// <summary>
    /// What the game makes with a room when it generates it (DungeonGenerator.PlaceRoom, SpawnMode Full): its
    /// networked objects, those its random parts keep, rolled from the room's place the game's way
    /// (Random.InitState, then each RandomSpawn's chance, then each RandomObject's pick, in order). The
    /// random parts' own heights are taken as the room's. The shared Unity random state is put back after.
    /// </summary>
    public static List<Made> Contents(Placed p, Kind kind, Vector3 dungeonPosition, int worldSeed)
    {
        if (p.Room is not { } room || room.Contents.Length == 0)
        {
            return [];
        }
        Vector3 v = kind.CustomInterior ? p.Position - dungeonPosition : p.Position;
        int seed = unchecked((int)v.X * 4271 + (int)v.Y * 9187 + (int)v.Z * 2134 + (kind.BaseSeed ? Seed(worldSeed, dungeonPosition) : 0));
        return Replay(room.Contents, room.Spawns, room.Picks, room.Nodes, kind.Themes, p.Position.Y, seed, p.Position, p.Rotation);
    }

    /// <summary>
    /// What the game makes with a room or a location it spawns: its networked objects, those its random parts keep,
    /// rolled the game's way (Random.InitState with the seed, then each RandomSpawn's chance, then each RandomObject's
    /// pick, in order), moved and turned with it. The shared Unity random state is put back after.
    /// </summary>
    /// <param name="contents">Its networked objects, in its frame.</param>
    /// <param name="spawns">Its RandomSpawn parts, in the order the game rolls them.</param>
    /// <param name="picks">Its RandomObject parts, rolled after the spawns.</param>
    /// <param name="nodes">Each random part's parent node (-1: the prefab itself).</param>
    /// <param name="themes">The dungeon's themes a part may require (every bit set: none is checked, as for a location).</param>
    /// <param name="y">The elevation the parts' limits are checked against.</param>
    /// <param name="seed">What the rolls start from.</param>
    /// <param name="position">Where it stands, world metres.</param>
    /// <param name="rotation">Its turn.</param>
    internal static List<Made> Replay(Content[] contents, Spawn[] spawns, Pick[] picks, int[] nodes, int themes, float y, int seed, Vector3 position, Quaternion rotation)
    {
        var made = new List<Made>();
        if (contents.Length == 0)
        {
            return made;
        }
        bool[] off = new bool[nodes.Length];
        var was = Rnd.state;
        try
        {
            Rnd.InitState(seed);
            foreach (var s in spawns)
            {
                bool spawned = Rnd.Range(0f, 100f) <= s.Chance;
                if (s.Theme != 0 && (themes & s.Theme) == 0 || y < s.MinY || y > s.MaxY)
                {
                    spawned = false;
                }
                Set(s.Node, !spawned);
                if (s.Off >= 0)
                {
                    Set(s.Off, spawned);
                }
            }
            foreach (var pick in picks)
            {
                float total = pick.Choices.Sum(c => c.Weight), roll = Rnd.Range(0f, total), sum = 0;
                int? chosen = null;
                foreach (var (node, weight) in pick.Choices)
                {
                    sum += weight;
                    if (roll <= sum)
                    {
                        chosen = node;
                        break;
                    }
                }
                if (pick.Theme != 0 && (themes & pick.Theme) == 0 || y < pick.MinY || y > pick.MaxY)
                {
                    chosen = null;
                }
                foreach (var (node, _) in pick.Choices)
                {
                    Set(node, node != chosen);
                }
                if (chosen == null)
                {
                    Set(pick.Node, true);
                }
            }
        }
        finally
        {
            Rnd.state = was;
        }
        foreach (var c in contents)
        {
            // Up its parents to the prefab: a node past the list (damaged scan data) ends the walk, and so does a walk
            // longer than the list (a loop).
            bool shown = true;
            for (int n = c.Node, steps = 0; n >= 0 && n < nodes.Length && shown && steps <= nodes.Length; n = nodes[n], steps++)
            {
                shown = !off[n];
            }
            if (shown)
            {
                made.Add(new Made(c.Prefab, position + Vector3.Transform(c.Position, rotation), Quaternion.Normalize(rotation * c.Rotation)));
            }
        }
        return made;

        void Set(int node, bool hidden)
        {
            if (node >= 0 && node < off.Length)
            {
                off[node] = hidden;
            }
        }
    }

    // ---- Dungeons of a save.
    /// <summary>A dungeon object found in a save.</summary>
    /// <param name="Id">The object's id in the save.</param>
    /// <param name="Kind">Its kind.</param>
    /// <param name="Position">Where it stands.</param>
    /// <param name="Rooms">How many rooms its list has (0: none, or not readable).</param>
    public sealed record Found(int Id, Kind Kind, Vector3 Position, int Rooms);

    /// <summary>Every dungeon object of the save, with its room count.</summary>
    public static List<Found> In(WorldSave world)
    {
        var found = new List<Found>();
        foreach (var (id, prefab, position, _, _) in world.Objects)
        {
            if (KindOf(prefab) is not { } kind)
            {
                continue;
            }
            int count = 0;
            try
            {
                byte[]? data = ZdoData.Parse(world.ObjectBytes(id), world.ObjectRefs[id].File.WorldVersion).GetBytes(RoomDataKey);
                count = data == null ? 0 : BitConverter.ToInt32(data, 0);
            }
            catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException or FormatException or OverflowException)
            {
                // Damaged or unreadable: found with no rooms, said in the log, and the others still listed.
                Console.WriteLine($"Dungeon object {id} ({kind}) at {position} (world version {world.ObjectRefs[id].File.WorldVersion}): room list not read ({e.GetType().Name}: {e.Message}); listed with no rooms.");
            }
            found.Add(new Found(id, kind, position, count));
        }
        return found;
    }

    /// <summary>The dungeon object's bytes with another room list.</summary>
    public static byte[] WithRooms(byte[] dungeonObject, IReadOnlyList<Placed> rooms)
    {
        var z = ZdoData.Parse(dungeonObject);
        z.SetBytes(RoomDataKey, Write(rooms));
        return z.Serialize();
    }
}
