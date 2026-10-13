using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// ValheimRAFT's vehicles (ships, land vehicles): a root object holding a PersistentID, and its pieces, each naming
/// that id as its parent and keeping its place and turn in the root's frame. The game saves every piece at the root's
/// position; the mod builds each again from its offset when the vehicle loads. Pieces on a swivel name the swivel
/// (itself a piece of the vehicle) and keep their place in its frame. Read from the objects' data: names do not matter.
/// </summary>
public static class Vehicles
{
    /// <summary>Int data key of an object's own id among the mod's objects (vehicle roots, swivels, masts, rope anchors).</summary>
    public static readonly int PersistentIdKey = StableHash.Of("PersistentID");

    /// <summary>Int data key of a piece's vehicle: the root's PersistentID.</summary>
    public static readonly int ParentKey = StableHash.Of("MBParentId");

    /// <summary>Vec3 data key of a piece's place in its parent's frame.</summary>
    public static readonly int OffsetKey = StableHash.Of("MBPosition");

    /// <summary>Vec3 data key of a piece's turn in its parent's frame, Unity Euler degrees.</summary>
    public static readonly int EulerKey = StableHash.Of("MBRotationVec");

    /// <summary>Quaternion data key of a piece's turn as older versions of the mod kept it (used when the Euler one is missing).</summary>
    public static readonly int OldTurnKey = StableHash.Of("MBRotation");

    /// <summary>Int data key of a piece on a swivel: the swivel's PersistentID (its offset is then in the swivel's frame).</summary>
    public static readonly int SwivelKey = StableHash.Of("SwivelParentId");

    /// <summary>Int data key of a sail's mast (its PersistentID).</summary>
    public static readonly int SailParentKey = StableHash.Of("SailParentId");

    /// <summary>Int data key of a plant's planter on a vehicle (its PersistentID).</summary>
    public static readonly int CultivatableParentKey = StableHash.Of("MBCultivatableParentId");

    /// <summary>Int data key of the swivel a switch drives (its PersistentID).</summary>
    public static readonly int SwivelTargetKey = StableHash.Of("Mechanism_swivelTargetId");

    /// <summary>Byte array data key of a rope anchor's ropes: the anchors they go to.</summary>
    public static readonly int RopesKey = StableHash.Of("MBRopeAnchor_Ropes");

    /// <summary>Int data key of what is only aboard for now (characters, carts): the vehicle's PersistentID.</summary>
    public static readonly int AboardKey = StableHash.Of("VehicleTempPieceParentId");

    /// <summary>The int keys naming another object's PersistentID, given the new ids when a copy is pasted.</summary>
    private static readonly int[] References = [ParentKey, SwivelKey, SailParentKey, CultivatableParentKey, SwivelTargetKey];

    /// <summary>The links read from added objects' bytes, by those bytes.</summary>
    private static readonly ConditionalWeakTable<byte[], StrongBox<VehicleLink?>> RawLinks = [];

    /// <summary>The keys a link is read from.</summary>
    private static readonly HashSet<int> LinkKeys = [PersistentIdKey, ParentKey, OffsetKey, EulerKey, OldTurnKey, SwivelKey, AboardKey];

    /// <summary>The rope list's version that names anchors by PersistentID (older ones name them by ZDOID).</summary>
    private const byte RopesVersion = 2;

    /// <summary>How many parents a piece's chain may have (a deeper one is taken for damaged data).</summary>
    public const int MaxDepth = 16;

    /// <summary>Whether a data key is one of those a link is read from.</summary>
    public static bool IsLinkKey(int key) => LinkKeys.Contains(key);

    /// <summary>An object's link from all its data; null when it has none.</summary>
    public static VehicleLink? Read(ZdoData z)
    {
        int Int(int key) => z.IntList.Find(e => e.Key == key).Value;
        int offset = z.Vec3List.FindIndex(e => e.Key == OffsetKey), euler = z.Vec3List.FindIndex(e => e.Key == EulerKey), old = z.QuatList.FindIndex(e => e.Key == OldTurnKey);
        var link = new VehicleLink(Int(PersistentIdKey), Int(ParentKey), Int(SwivelKey), offset >= 0 ? z.Vec3List[offset].Value : Vector3.Zero,
            euler >= 0 ? z.Vec3List[euler].Value : null, old >= 0 ? z.QuatList[old].Value : null, Int(AboardKey));
        return link.IsEmpty ? null : link;
    }

    /// <summary>
    /// A link with values set over it (as a new object's data sets them over what it is made from): null when it ends
    /// up with none. Values that do not parse are left out.
    /// </summary>
    /// <param name="start">The link before (null: none).</param>
    /// <param name="data">The values set, if any.</param>
    public static VehicleLink? With(VehicleLink? start, IReadOnlyList<ObjectField>? data)
    {
        if (data == null || !data.Any(f => IsLinkKey(f.Key)))
        {
            return start;
        }
        var z = new ZdoData();
        if (start != null)
        {
            Write(start, z);
        }
        foreach (var f in data.Where(f => IsLinkKey(f.Key)))
        {
            try
            {
                z.Set(f.Section, f.Key, f.Value);
            }
            catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
            {
                // A damaged value counts as none.
            }
        }
        return Read(z);
    }

    /// <summary>Puts a link's values in an object's data.</summary>
    private static void Write(VehicleLink link, ZdoData z)
    {
        foreach (var (key, value) in new[] { (PersistentIdKey, link.Id), (ParentKey, link.Parent), (SwivelKey, link.Swivel), (AboardKey, link.Aboard) })
        {
            if (value != 0)
            {
                z.IntList.Add((key, value));
            }
        }
        z.Vec3List.Add((OffsetKey, link.Offset));
        if (link.Euler is { } e)
        {
            z.Vec3List.Add((EulerKey, e));
        }
        if (link.OldTurn is { } q)
        {
            z.QuatList.Add((OldTurnKey, q));
        }
    }

    /// <summary>
    /// The link of an object added in this session: from its own bytes (an inspector's edit), else from the saved
    /// object it was moved from (a move keeps all its data), with its data's values set over it.
    /// </summary>
    /// <param name="world">The world, holding the saved objects' links.</param>
    /// <param name="n">The added object.</param>
    public static VehicleLink? LinkOf(WorldSave world, NewObject n)
    {
        VehicleLink? link = null;
        if (n.Raw != null)
        {
            // Read once per object: the vehicles are looked at after every edit, and a reopened history's objects all
            // come with their bytes.
            link = RawLinks.GetValue(n.Raw, static raw => new StrongBox<VehicleLink?>(Read(ZdoData.Parse(raw)))).Value;
        }
        else if (!n.Fresh && n.SourceId is int s && world.VehicleLinks.TryGetValue(s, out var saved))
        {
            link = saved.Link;
        }
        return With(link, n.Data);
    }

    /// <summary>
    /// The PersistentIDs a rope anchor's ropes go to, as the mod writes them (a version byte, then each rope's anchor
    /// id and attachment point); null for another version or damaged bytes.
    /// </summary>
    public static List<(int Id, byte Point)>? ReadRopes(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[0] != RopesVersion || (bytes.Length - 1) % 5 != 0)
        {
            return null;
        }
        var ropes = new List<(int Id, byte Point)>();
        for (int i = 1; i < bytes.Length; i += 5)
        {
            ropes.Add((BitConverter.ToInt32(bytes, i), bytes[i + 4]));
        }
        return ropes;
    }

    /// <summary>A rope anchor's ropes as the mod reads them (see <see cref="ReadRopes"/>).</summary>
    public static byte[] WriteRopes(IReadOnlyList<(int Id, byte Point)> ropes)
    {
        byte[] bytes = new byte[1 + ropes.Count * 5];
        bytes[0] = RopesVersion;
        for (int k = 0; k < ropes.Count; k++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(1 + k * 5), ropes[k].Id);
            bytes[1 + k * 5 + 4] = ropes[k].Point;
        }
        return bytes;
    }

    /// <summary>
    /// One pasted copy's objects made a vehicle of their own. Each PersistentID gets a new one, unique in the world, and
    /// every reference to it follows (parents, swivels, masts, planters, switches, ropes): two vehicles under one id
    /// would share their pieces in the game. A piece whose parent is in the copy is stored at its vehicle's root, the
    /// way the game saves it; one whose parent was not copied is no longer a vehicle's piece (its links are taken
    /// away) and stays where it was pasted. What was only aboard (characters, carts) is left out. Objects that are no
    /// vehicle's come back as they were.
    /// </summary>
    /// <param name="copy">The copy's new objects, each with all its data (as the clipboard holds them).</param>
    /// <param name="taken">The PersistentIDs in use; the new ones are added to it.</param>
    /// <param name="mirror">The copy is mirrored east to west: the pieces' offsets and turns in their parents' frames too.</param>
    /// <param name="random">Draws the new ids (null: the shared one).</param>
    /// <returns>The objects as they are to be made, in the copy's order; null for those left out.</returns>
    public static NewObject?[] Remap(IReadOnlyList<NewObject> copy, ISet<int> taken, bool mirror = false, Random? random = null)
    {
        // What has no link but names another object (a sail on a mast standing on its own) is looked at too.
        var none = new VehicleLink(0, 0, 0, Vector3.Zero, null, null, 0);
        var links = copy.Select(o => With(null, o.Data) ?? (o.Data?.Any(f => f.Key == RopesKey || References.Contains(f.Key)) == true ? none : null)).ToList();
        if (links.All(l => l == null))
        {
            return [.. copy];
        }
        random ??= Random.Shared;
        // The copy's own ids: each object holding one, and its new id.
        var at = new Dictionary<int, int>();
        var ids = new Dictionary<int, int>();
        for (int i = 0; i < copy.Count; i++)
        {
            if (links[i] is { Id: not 0, Aboard: 0 } l && at.TryAdd(l.Id, i))
            {
                ids[l.Id] = NewId(taken, random);
            }
        }
        // The top of a piece's chain in the copy (its root, or the swivel or part it hangs on when that is not copied).
        int Top(int i)
        {
            var seen = new HashSet<int> { i };
            while (links[i] is { Holder: not 0 } l && at.TryGetValue(l.Holder, out int up) && seen.Add(up))
            {
                i = up;
            }
            return i;
        }
        var result = new NewObject?[copy.Count];
        for (int i = 0; i < copy.Count; i++)
        {
            var o = copy[i];
            if (links[i] is not { } link)
            {
                result[i] = o;
                continue;
            }
            if (link.Aboard != 0)
            {
                continue;
            }
            var data = o.Data!.ToList();
            if (link.Id != 0)
            {
                Put(data, "ints", PersistentIdKey, ids[link.Id].ToString(CultureInfo.InvariantCulture));
            }
            bool attached = link.Holder != 0 && at.ContainsKey(link.Holder);
            foreach (int key in References)
            {
                // A piece whose parent was not copied is let go below.
                if (IntIn(data, key) is not (not 0 and var was) || !attached && (key == ParentKey || key == SwivelKey))
                {
                    continue;
                }
                // Another reference to something not copied goes.
                Put(data, "ints", key, ids.TryGetValue(was, out int now) ? now.ToString(CultureInfo.InvariantCulture) : null);
            }
            var position = o.Position;
            if (attached)
            {
                position = copy[Top(i)].Position;
                if (mirror)
                {
                    Put(data, "vec3", OffsetKey, Vec(link.Offset with { X = -link.Offset.X }));
                    if (link.Euler is { } e)
                    {
                        Put(data, "vec3", EulerKey, Vec(new Vector3(e.X, -e.Y, -e.Z)));
                    }
                    if (link.OldTurn is { } q)
                    {
                        Put(data, "quats", OldTurnKey, string.Create(CultureInfo.InvariantCulture, $"{q.X:R} {-q.Y:R} {-q.Z:R} {q.W:R}"));
                    }
                }
            }
            else
            {
                foreach (var (section, key) in new[] { ("ints", ParentKey), ("ints", SwivelKey), ("vec3", OffsetKey), ("vec3", EulerKey), ("quats", OldTurnKey) })
                {
                    Put(data, section, key, null);
                }
            }
            int ropes = data.FindIndex(f => f.Key == RopesKey && f.Section == "bytes" && f.Value != null);
            if (ropes >= 0)
            {
                // Ropes to anchors not copied go; a list naming anchors by ZDOID would tie the copy to the originals.
                var list = ReadRopes(Convert.FromBase64String(data[ropes].Value!));
                data[ropes] = data[ropes] with
                {
                    Value = list == null ? null : Convert.ToBase64String(WriteRopes([.. list.Where(r => ids.ContainsKey(r.Id)).Select(r => (ids[r.Id], r.Point))])),
                };
            }
            result[i] = o with { Position = position, Data = data };
        }
        return result;
    }

    /// <summary>A vector as data text.</summary>
    private static string Vec(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:R} {v.Y:R} {v.Z:R}");

    /// <summary>An int value of data (0 when it has none or it does not parse).</summary>
    private static int IntIn(List<ObjectField> data, int key) =>
        data.Find(f => f.Key == key && f.Section == "ints") is { Value: { } v } && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    /// <summary>Sets a value of data (null: takes it away), in place of the one it had.</summary>
    private static void Put(List<ObjectField> data, string section, int key, string? value)
    {
        int i = data.FindIndex(f => f.Key == key && f.Section == section);
        if (i >= 0)
        {
            data[i] = data[i] with { Value = value };
        }
        else if (value != null)
        {
            data.Add(new ObjectField(section, key, value));
        }
    }

    /// <summary>A PersistentID not in use (it is added to those in use).</summary>
    private static int NewId(ISet<int> taken, Random random)
    {
        int id;
        do
        {
            id = random.Next(1, int.MaxValue);
        }
        while (!taken.Add(id));
        return id;
    }
}

/// <summary>What an object's data says of its place in a vehicle (see <see cref="Vehicles"/>).</summary>
/// <param name="Id">Its own PersistentID (0: none).</param>
/// <param name="Parent">Its vehicle's PersistentID (MBParentId; 0: none).</param>
/// <param name="Swivel">The PersistentID of the swivel it is on (0: none).</param>
/// <param name="Offset">Its place in its parent's frame (the swivel's when it is on one), in metres.</param>
/// <param name="Euler">Its turn in that frame, Unity Euler degrees; null when not stored.</param>
/// <param name="OldTurn">Its turn as older versions of the mod stored it; null when not stored.</param>
/// <param name="Aboard">The vehicle it is only aboard for now (characters, carts; 0: none).</param>
public sealed record VehicleLink(int Id, int Parent, int Swivel, Vector3 Offset, Vector3? Euler, Quaternion? OldTurn, int Aboard)
{
    /// <summary>
    /// The PersistentID of what it hangs on: the swivel when it is on one, else its vehicle; 0 for a root and for what is
    /// only aboard (the mod keeps those where they are).
    /// </summary>
    public int Holder => Aboard != 0 ? 0 : Swivel != 0 ? Swivel : Parent;

    /// <summary>Its turn in its parent's frame: the Euler one, else the older quaternion, else none.</summary>
    public Quaternion Turn => Euler is { } e ? BlueprintFormats.FromEuler(e)
        : OldTurn is { } q && q.LengthSquared() > 1e-8f ? Quaternion.Normalize(q) : Quaternion.Identity;

    /// <summary>It names no vehicle and has no id of its own.</summary>
    public bool IsEmpty => Id == 0 && Parent == 0 && Swivel == 0 && Aboard == 0;
}

/// <summary>
/// The vehicles of a world as they are now (its objects and those added in this session, deleted ones left out): which
/// object holds which PersistentID, where each piece is drawn, and which objects make one vehicle.
/// </summary>
public sealed class VehicleIndex
{
    /// <summary>An object with a link.</summary>
    /// <param name="Id">Its object id (negative for one added in this session).</param>
    /// <param name="Position">Its position as stored, in metres.</param>
    /// <param name="Rotation">Its turn as stored, Unity Euler degrees.</param>
    /// <param name="Link">What its data says.</param>
    public sealed record Member(int Id, Vector3 Position, Vector3 Rotation, VehicleLink Link);

    /// <summary>The objects with a link, by id.</summary>
    private readonly Dictionary<int, Member> _members = [];

    /// <summary>The object holding each PersistentID (the first one read when two hold it).</summary>
    private readonly Dictionary<int, int> _holders = [];

    /// <summary>The objects hanging on each object, by its id.</summary>
    private readonly Dictionary<int, List<int>> _children = [];

    /// <summary>Every PersistentID in use, deleted objects' too (undo brings them back): a new one must be none of these.</summary>
    public HashSet<int> Ids { get; } = [];

    /// <summary>No object has a link.</summary>
    public bool IsEmpty => _members.Count == 0;

    /// <summary>The vehicles of a world, as they are with the edits. The caller holds the world's lock.</summary>
    /// <param name="world">The world.</param>
    /// <param name="deleted">Saved objects left out.</param>
    /// <param name="added">Objects added in this session.</param>
    public static VehicleIndex Of(WorldSave world, ICollection<int> deleted, IEnumerable<NewObject> added)
    {
        var ix = new VehicleIndex();
        foreach (var (id, (link, rotation)) in world.VehicleLinks)
        {
            if (link.Id != 0)
            {
                ix.Ids.Add(link.Id);
            }
            if (!deleted.Contains(id))
            {
                ix.Add(new Member(id, world.ObjectRefs[id].Position, rotation, link));
            }
        }
        foreach (var n in added)
        {
            if (Vehicles.LinkOf(world, n) is { } link)
            {
                if (link.Id != 0)
                {
                    ix.Ids.Add(link.Id);
                }
                ix.Add(new Member(n.Id, n.Position, n.Rotation, link));
            }
        }
        ix.Join();
        return ix;
    }

    /// <summary>The vehicles of these objects alone (tests, and objects that are no world's).</summary>
    public static VehicleIndex Of(IEnumerable<Member> members)
    {
        var ix = new VehicleIndex();
        foreach (var m in members)
        {
            if (m.Link.Id != 0)
            {
                ix.Ids.Add(m.Link.Id);
            }
            ix.Add(m);
        }
        ix.Join();
        return ix;
    }

    /// <summary>Takes an object in.</summary>
    private void Add(Member m)
    {
        _members[m.Id] = m;
        if (m.Link.Id != 0)
        {
            _holders.TryAdd(m.Link.Id, m.Id);
        }
    }

    /// <summary>Hangs each object on what it names, once all are in.</summary>
    private void Join()
    {
        foreach (var m in _members.Values)
        {
            if (m.Link.Holder != 0 && _holders.TryGetValue(m.Link.Holder, out int up) && up != m.Id)
            {
                (_children.TryGetValue(up, out var list) ? list : _children[up] = []).Add(m.Id);
            }
        }
    }

    /// <summary>An object with a link, by id; null when it has none.</summary>
    public Member? Get(int id) => _members.GetValueOrDefault(id);

    /// <summary>
    /// The objects from one up to the top of its chain (what it hangs on, what that hangs on...); null when the chain
    /// loops or goes deeper than <see cref="Vehicles.MaxDepth"/> (damaged data).
    /// </summary>
    private List<Member>? Chain(Member m)
    {
        var chain = new List<Member> { m };
        var seen = new HashSet<int> { m.Id };
        while (chain[^1].Link.Holder != 0 && _holders.TryGetValue(chain[^1].Link.Holder, out int up))
        {
            if (!seen.Add(up) || chain.Count > Vehicles.MaxDepth)
            {
                return null;
            }
            chain.Add(_members[up]);
        }
        return chain;
    }

    /// <summary>
    /// Where the mod puts an object (position in metres, turn as Unity Euler degrees): a piece at its offset in its
    /// parent's frame, the parent where its own parent puts it, and so on up to the root, which is where it is stored.
    /// An object whose parent is missing (or whose chain loops) stays where it is stored. Null for an object with no
    /// link.
    /// </summary>
    public (Vector3 Position, Vector3 Rotation)? Pose(int id)
    {
        if (Get(id) is not { } m)
        {
            return null;
        }
        if (Chain(m) is not { Count: > 1 } chain)
        {
            return (m.Position, m.Rotation);
        }
        var top = chain[^1];
        Vector3 p = top.Position;
        Quaternion q = BlueprintFormats.FromEuler(top.Rotation);
        for (int k = chain.Count - 2; k >= 0; k--)
        {
            var link = chain[k].Link;
            p += Vector3.Transform(link.Offset, q);
            q = Quaternion.Normalize(q * link.Turn);
        }
        return (p, BlueprintFormats.ToEuler(q));
    }

    /// <summary>The root of an object's vehicle (the top of its chain; itself when it hangs on nothing found).</summary>
    public int RootOf(int id)
    {
        if (Get(id) is not { } m)
        {
            return id;
        }
        var seen = new HashSet<int> { id };
        while (m.Link.Holder != 0 && _holders.TryGetValue(m.Link.Holder, out int up) && seen.Add(up))
        {
            m = _members[up];
        }
        return m.Id;
    }

    /// <summary>
    /// Every object of the vehicle an object belongs to: its root first, then every object whose chain leads there. Just
    /// the object for one with no link.
    /// </summary>
    public List<int> Group(int id)
    {
        int root = RootOf(id);
        var group = new List<int> { root };
        var seen = new HashSet<int> { root };
        for (int k = 0; k < group.Count; k++)
        {
            foreach (int child in _children.GetValueOrDefault(group[k]) ?? [])
            {
                if (seen.Add(child))
                {
                    group.Add(child);
                }
            }
        }
        return group;
    }
}
