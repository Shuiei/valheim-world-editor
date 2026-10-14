using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// How a mod links its objects into one (a ship and the pieces built on it...), as its worldeditor.json tells it
/// (ModManifests): an object holds an id of its own, its parts
/// name that id as what they hang on and keep their place and turn in its frame. Every key is a data key, as the
/// mod's code names it.
/// </summary>
/// <param name="Name">What one such group is called in messages ("vehicle"; <see cref="DefaultName"/> when not told).</param>
/// <param name="IdKey">Int data key of an object's own id (its parts name it).</param>
/// <param name="ParentKeys">
/// Int data keys naming what a part hangs on (another object's id), the first one set winning: a piece on a swivel names
/// the swivel first, then the vehicle.
/// </param>
/// <param name="OffsetKey">Vec3 data key of a part's place in the frame of what it hangs on, in metres.</param>
/// <param name="EulerKey">Vec3 data key of a part's turn in that frame, Unity Euler degrees (0: none).</param>
/// <param name="QuatKey">Quaternion data key of that turn, used when the Euler one is not stored (0: none).</param>
/// <param name="ReferenceKeys">
/// Int data keys naming another object's id that are no parent (a sail's mast, a switch's swivel): a pasted copy's
/// follow the new ids, and go when what they name was not copied.
/// </param>
/// <param name="DropKeys">
/// Data keys a pasted copy leaves out (any kind of value): ids packed in ways the editor cannot rewrite, ties that must
/// not carry over to a copy.
/// </param>
/// <param name="StoredAtRoot">
/// The game saves a part at its group's root (the mod builds it from its offset when the group loads), not where it is
/// drawn.
/// </param>
public sealed record LinkRule(string Name, int IdKey, int[] ParentKeys, int OffsetKey, int EulerKey, int QuatKey, int[] ReferenceKeys, int[] DropKeys,
    bool StoredAtRoot)
{
    /// <summary>The name used when a mod gives none.</summary>
    public const string DefaultName = "group";
}

/// <summary>The link rules in use and the data keys they read, swapped in whole (see ObjectLinks).</summary>
public sealed class LinkSet
{
    /// <summary>No rules.</summary>
    public static readonly LinkSet Empty = new([]);

    /// <summary>The rules, an object taking the first one whose id or parent keys it holds.</summary>
    public IReadOnlyList<LinkRule> Rules { get; }

    /// <summary>The int keys a link is read from (ids and parents).</summary>
    public HashSet<int> IntKeys { get; } = [];

    /// <summary>The vec3 keys a link is read from (offsets, Euler turns).</summary>
    public HashSet<int> Vec3Keys { get; } = [];

    /// <summary>The quaternion keys a link is read from.</summary>
    public HashSet<int> QuatKeys { get; } = [];

    /// <summary>The rules, and the keys they read.</summary>
    public LinkSet(IReadOnlyList<LinkRule> rules)
    {
        Rules = rules;
        foreach (var r in rules)
        {
            IntKeys.Add(r.IdKey);
            IntKeys.UnionWith(r.ParentKeys);
            Vec3Keys.Add(r.OffsetKey);
            if (r.EulerKey != 0)
            {
                Vec3Keys.Add(r.EulerKey);
            }
            if (r.QuatKey != 0)
            {
                QuatKeys.Add(r.QuatKey);
            }
        }
    }

    /// <summary>Whether a data key is one a link is read from.</summary>
    public bool IsLinkKey(int key) => IntKeys.Contains(key) || Vec3Keys.Contains(key) || QuatKeys.Contains(key);
}

/// <summary>
/// Objects mods link into one (LinkRule): a root holding an id, and its parts, each naming the id of what it hangs on
/// (the root, or a part of it) and keeping its place and turn in that one's frame. The mod builds each part there when
/// the group loads; the editor draws it there, and moves, copies and deletes the group whole. Read from the objects'
/// data: prefab names do not matter.
/// </summary>
public static class ObjectLinks
{
    /// <summary>How many parents a part's chain may have (a deeper one is taken for damaged data).</summary>
    public const int MaxDepth = 16;

    /// <summary>The links read from added objects' bytes, by those bytes, with the rules they were read by.</summary>
    private static readonly ConditionalWeakTable<byte[], Tuple<LinkSet, ObjectLink?>> RawLinks = [];

    /// <summary>The rules in use: the mods' manifests' (ModManifests).</summary>
    public static LinkSet Current => ModManifests.Links;

    /// <summary>Whether a data key is one a link is read from (by the rules in use).</summary>
    public static bool IsLinkKey(int key) => Current.IsLinkKey(key);

    /// <summary>An object's link from all its data, by the rules in use; null when it has none.</summary>
    public static ObjectLink? Read(ZdoData z) => Read(Current, z.IntList, z.Vec3List, z.QuatList);

    /// <summary>
    /// An object's link from its values: by the first rule whose id or parent keys it holds; null when none does.
    /// </summary>
    /// <param name="set">The rules.</param>
    /// <param name="ints">Its int values (others than the rules' are fine).</param>
    /// <param name="vec3s">Its vec3 values (null: none).</param>
    /// <param name="quats">Its quaternion values (null: none).</param>
    public static ObjectLink? Read(LinkSet set, IReadOnlyList<(int Key, int Value)> ints, IReadOnlyList<(int Key, Vector3 Value)>? vec3s,
        IReadOnlyList<(int Key, Quaternion Value)>? quats)
    {
        int Int(int key)
        {
            foreach (var (k, v) in ints)
            {
                if (k == key)
                {
                    return v;
                }
            }
            return 0;
        }
        foreach (var rule in set.Rules)
        {
            int id = Int(rule.IdKey);
            int[] parents = [.. rule.ParentKeys.Select(Int)];
            if (id == 0 && parents.All(p => p == 0))
            {
                continue;
            }
            Vector3? offset = null, euler = null;
            foreach (var (k, v) in vec3s ?? [])
            {
                if (k == rule.OffsetKey)
                {
                    offset ??= v;
                }
                else if (k == rule.EulerKey && k != 0)
                {
                    euler ??= v;
                }
            }
            Quaternion? quat = null;
            foreach (var (k, v) in quats ?? [])
            {
                if (k == rule.QuatKey && k != 0)
                {
                    quat ??= v;
                }
            }
            return new ObjectLink(rule, id, parents, offset ?? Vector3.Zero, euler, quat);
        }
        return null;
    }

    /// <summary>
    /// A link with values set over it (as a new object's data sets them over what it is made from): null when it ends
    /// up with none. Values that do not parse are left out.
    /// </summary>
    /// <param name="start">The link before (null: none).</param>
    /// <param name="data">The values set, if any.</param>
    public static ObjectLink? With(ObjectLink? start, IReadOnlyList<ObjectField>? data)
    {
        var set = Current;
        if (data == null || !data.Any(f => set.IsLinkKey(f.Key)))
        {
            return start;
        }
        var z = new ZdoData();
        if (start != null)
        {
            Write(start, z);
        }
        foreach (var f in data.Where(f => set.IsLinkKey(f.Key)))
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
        return Read(set, z.IntList, z.Vec3List, z.QuatList);
    }

    /// <summary>Puts a link's values in an object's data.</summary>
    private static void Write(ObjectLink link, ZdoData z)
    {
        var rule = link.Rule;
        if (link.Id != 0)
        {
            z.IntList.Add((rule.IdKey, link.Id));
        }
        for (int k = 0; k < rule.ParentKeys.Length; k++)
        {
            if (link.Parents[k] != 0)
            {
                z.IntList.Add((rule.ParentKeys[k], link.Parents[k]));
            }
        }
        z.Vec3List.Add((rule.OffsetKey, link.Offset));
        if (link.Euler is { } e)
        {
            z.Vec3List.Add((rule.EulerKey, e));
        }
        if (link.Quat is { } q)
        {
            z.QuatList.Add((rule.QuatKey, q));
        }
    }

    /// <summary>
    /// The link of an object added in this session: from its own bytes (an inspector's edit), else from the saved
    /// object it was moved from (a move keeps all its data), with its data's values set over it.
    /// </summary>
    /// <param name="world">The world, holding the saved objects' links.</param>
    /// <param name="n">The added object.</param>
    public static ObjectLink? LinkOf(WorldSave world, NewObject n)
    {
        ObjectLink? link = null;
        if (n.Raw != null)
        {
            // Read once per object and rules: the links are looked at after every edit, and a reopened history's
            // objects all come with their bytes.
            var set = Current;
            if (!RawLinks.TryGetValue(n.Raw, out var kept) || kept.Item1 != set)
            {
                var z = ZdoData.Parse(n.Raw);
                kept = Tuple.Create(set, Read(set, z.IntList, z.Vec3List, z.QuatList));
                RawLinks.AddOrUpdate(n.Raw, kept);
            }
            link = kept.Item2;
        }
        else if (!n.Fresh && n.SourceId is int s && world.Links.TryGetValue(s, out var saved))
        {
            link = saved.Link;
        }
        return With(link, n.Data);
    }

    /// <summary>
    /// One pasted copy's objects made groups of their own. Each id gets a new one, unique in the world, and every
    /// reference to it follows (parents and the rules' references): two groups under one id would share their parts in
    /// the game. A part whose parent is in the copy is stored at its group's root when the mod saves it so; one whose
    /// parent was not copied is no part any more (its links are taken away) and stays where it was pasted. Values the
    /// rules leave out of copies go. Objects that are no group's come back as they were.
    /// </summary>
    /// <param name="copy">The copy's new objects, each with all its data (as the clipboard holds them).</param>
    /// <param name="taken">The ids in use; the new ones are added to it.</param>
    /// <param name="mirror">The copy is mirrored east to west: the parts' offsets and turns in their parents' frames too.</param>
    /// <param name="random">Draws the new ids (null: the shared one).</param>
    /// <returns>The objects as they are to be made, in the copy's order.</returns>
    public static NewObject[] Remap(IReadOnlyList<NewObject> copy, ISet<int> taken, bool mirror = false, Random? random = null)
    {
        var set = Current;
        // What has no link but names another object, or holds what copies leave out (a sail on a mast standing on its
        // own), is looked at too.
        var links = copy.Select(o => With(null, o.Data) ?? Named(set, o.Data)).ToList();
        // Values extensions rewrite (ModExtensions): theirs to decide, not dropped.
        var rewritten = ModExtensions.RewrittenKeys;
        bool rewrite = rewritten.Count > 0 && copy.Any(o => o.Data?.Any(f => f.Value != null && rewritten.Contains(f.Key)) == true);
        if (links.All(l => l == null) && !rewrite)
        {
            return [.. copy];
        }
        random ??= Random.Shared;
        // The copy's own ids: each object holding one, and its new id.
        var at = new Dictionary<int, int>();
        var ids = new Dictionary<int, int>();
        for (int i = 0; i < copy.Count; i++)
        {
            if (links[i] is { Id: not 0 } l && at.TryAdd(l.Id, i))
            {
                ids[l.Id] = NewId(taken, random);
            }
        }
        // The top of a part's chain in the copy (its root, or the part it hangs on when that one's parent is not copied).
        int Top(int i)
        {
            var seen = new HashSet<int> { i };
            while (links[i] is { Holder: not 0 } l && at.TryGetValue(l.Holder, out int up) && seen.Add(up))
            {
                i = up;
            }
            return i;
        }
        var result = new NewObject[copy.Count];
        for (int i = 0; i < copy.Count; i++)
        {
            var o = copy[i];
            if (links[i] is not { } link)
            {
                result[i] = o;
                continue;
            }
            var rule = link.Rule;
            var data = o.Data!.ToList();
            if (link.Id != 0)
            {
                Put(data, "ints", rule.IdKey, ids[link.Id].ToString(CultureInfo.InvariantCulture));
            }
            bool attached = link.Holder != 0 && at.ContainsKey(link.Holder);
            foreach (int key in rule.ParentKeys.Concat(rule.ReferenceKeys))
            {
                // A part whose parent was not copied is let go below.
                if (IntIn(data, key) is not (not 0 and var was) || !attached && rule.ParentKeys.Contains(key))
                {
                    continue;
                }
                // Another reference to something not copied goes.
                Put(data, "ints", key, ids.TryGetValue(was, out int now) ? now.ToString(CultureInfo.InvariantCulture) : null);
            }
            var position = o.Position;
            if (attached)
            {
                if (rule.StoredAtRoot)
                {
                    position = copy[Top(i)].Position;
                }
                if (mirror)
                {
                    Put(data, "vec3", rule.OffsetKey, Vec(link.Offset with { X = -link.Offset.X }));
                    if (link.Euler is { } e)
                    {
                        Put(data, "vec3", rule.EulerKey, Vec(new Vector3(e.X, -e.Y, -e.Z)));
                    }
                    if (link.Quat is { } q)
                    {
                        Put(data, "quats", rule.QuatKey, string.Create(CultureInfo.InvariantCulture, $"{q.X:R} {-q.Y:R} {-q.Z:R} {q.W:R}"));
                    }
                }
            }
            else
            {
                foreach (int key in rule.ParentKeys)
                {
                    Put(data, "ints", key, null);
                }
                Put(data, "vec3", rule.OffsetKey, null);
                Put(data, "vec3", rule.EulerKey, null);
                Put(data, "quats", rule.QuatKey, null);
            }
            for (int k = 0; k < data.Count; k++)
            {
                if (rule.DropKeys.Contains(data[k].Key) && !rewritten.Contains(data[k].Key))
                {
                    data[k] = data[k] with { Value = null };
                }
            }
            result[i] = o with { Position = position, Data = data };
        }
        if (rewrite)
        {
            // Then the extensions, with the copy's new ids.
            var map = new IdMap(ids);
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] is { Data: { } data } o && data.Any(f => f.Value != null && rewritten.Contains(f.Key)))
                {
                    var view = new CopyView(PrefabCatalog.NameOf(o.Prefab) ?? "", o.Position, o.Rotation, [.. data]);
                    ModExtensions.Rewrite(view, map);
                    result[i] = o with { Data = view.Data };
                }
            }
        }
        return result;
    }

    /// <summary>A copy's new ids for the originals' (ICopyRewriter).</summary>
    private sealed class IdMap(Dictionary<int, int> ids) : ValheimWorldEditor.Sdk.IIdMap
    {
        /// <inheritdoc/>
        public bool TryGetNew(int oldId, out int newId) => ids.TryGetValue(oldId, out newId);
    }

    /// <summary>
    /// A link holding nothing, of the first rule whose references (or keys copies leave out) the data holds; null when
    /// none.
    /// </summary>
    private static ObjectLink? Named(LinkSet set, IReadOnlyList<ObjectField>? data)
    {
        if (data == null)
        {
            return null;
        }
        foreach (var rule in set.Rules)
        {
            if (data.Any(f => f.Value != null && (rule.DropKeys.Contains(f.Key) || f.Section == "ints" && rule.ReferenceKeys.Contains(f.Key))))
            {
                return new ObjectLink(rule, 0, new int[rule.ParentKeys.Length], Vector3.Zero, null, null);
            }
        }
        return null;
    }

    /// <summary>A vector as data text.</summary>
    private static string Vec(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:R} {v.Y:R} {v.Z:R}");

    /// <summary>An int value of data (0 when it has none or it does not parse).</summary>
    private static int IntIn(List<ObjectField> data, int key) =>
        data.Find(f => f.Key == key && f.Section == "ints") is { Value: { } v } && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    /// <summary>Sets a value of data (null: takes it away), in place of the one it had; nothing for key 0 (none).</summary>
    private static void Put(List<ObjectField> data, string section, int key, string? value)
    {
        if (key == 0)
        {
            return;
        }
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

    /// <summary>An id not in use (it is added to those in use).</summary>
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

/// <summary>What an object's data says of its place in a group of linked objects (see <see cref="ObjectLinks"/>).</summary>
/// <param name="Rule">The rule it was read by.</param>
/// <param name="Id">Its own id (0: none).</param>
/// <param name="Parents">The ids its rule's parent keys name, in their order (0: not set).</param>
/// <param name="Offset">Its place in the frame of what it hangs on, in metres.</param>
/// <param name="Euler">Its turn in that frame, Unity Euler degrees; null when not stored.</param>
/// <param name="Quat">Its turn in that frame as a quaternion; null when not stored.</param>
public sealed record ObjectLink(LinkRule Rule, int Id, int[] Parents, Vector3 Offset, Vector3? Euler, Quaternion? Quat)
{
    /// <summary>The id of what it hangs on: its first parent set; 0 for a root.</summary>
    public int Holder => Parents.FirstOrDefault(p => p != 0);

    /// <summary>Its turn in its parent's frame: the Euler one, else the quaternion, else none.</summary>
    public Quaternion Turn => Euler is { } e ? BlueprintFormats.FromEuler(e)
        : Quat is { } q && q.LengthSquared() > 1e-8f ? Quaternion.Normalize(q) : Quaternion.Identity;

    /// <summary>It hangs on nothing and has no id of its own.</summary>
    public bool IsEmpty => Id == 0 && Holder == 0;
}

/// <summary>
/// The linked objects of a world as they are now (its objects and those added in this session, deleted ones left out):
/// which object holds which id, where each part is drawn, and which objects make one group.
/// </summary>
public sealed class LinkIndex
{
    /// <summary>An object with a link.</summary>
    /// <param name="Id">Its object id (negative for one added in this session).</param>
    /// <param name="Position">Its position as stored, in metres.</param>
    /// <param name="Rotation">Its turn as stored, Unity Euler degrees.</param>
    /// <param name="Link">What its data says.</param>
    public sealed record Member(int Id, Vector3 Position, Vector3 Rotation, ObjectLink Link);

    /// <summary>The objects with a link, by id.</summary>
    private readonly Dictionary<int, Member> _members = [];

    /// <summary>The object holding each id (the first one read when two hold it).</summary>
    private readonly Dictionary<int, int> _holders = [];

    /// <summary>The objects hanging on each object, by its id.</summary>
    private readonly Dictionary<int, List<int>> _children = [];

    /// <summary>Every id in use, deleted objects' too (undo brings them back): a new one must be none of these.</summary>
    public HashSet<int> Ids { get; } = [];

    /// <summary>No object has a link.</summary>
    public bool IsEmpty => _members.Count == 0;

    /// <summary>The linked objects of a world, as they are with the edits. The caller holds the world's lock.</summary>
    /// <param name="world">The world.</param>
    /// <param name="deleted">Saved objects left out.</param>
    /// <param name="added">Objects added in this session.</param>
    public static LinkIndex Of(WorldSave world, ICollection<int> deleted, IEnumerable<NewObject> added)
    {
        var ix = new LinkIndex();
        foreach (var (id, (link, rotation)) in world.Links)
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
            if (ObjectLinks.LinkOf(world, n) is { } link)
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

    /// <summary>The links of these objects alone (tests, and objects that are no world's).</summary>
    public static LinkIndex Of(IEnumerable<Member> members)
    {
        var ix = new LinkIndex();
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

    /// <summary>What the group an object belongs to is called (its root's rule's name); null for one with no link.</summary>
    public string? NameOf(int id) => Get(RootOf(id))?.Link.Rule.Name;

    /// <summary>
    /// The objects from one up to the top of its chain (what it hangs on, what that hangs on...); null when the chain
    /// loops or goes deeper than <see cref="ObjectLinks.MaxDepth"/> (damaged data).
    /// </summary>
    private List<Member>? Chain(Member m)
    {
        var chain = new List<Member> { m };
        var seen = new HashSet<int> { m.Id };
        while (chain[^1].Link.Holder != 0 && _holders.TryGetValue(chain[^1].Link.Holder, out int up))
        {
            if (!seen.Add(up) || chain.Count > ObjectLinks.MaxDepth)
            {
                return null;
            }
            chain.Add(_members[up]);
        }
        return chain;
    }

    /// <summary>
    /// Where the mod puts an object (position in metres, turn as Unity Euler degrees): a part at its offset in its
    /// parent's frame, the parent where its own parent puts it, and so on up to the root, which is where it is stored.
    /// An object whose parent, or any parent up its chain, is missing (or whose chain loops) stays where it is stored.
    /// Null for an object with no link.
    /// </summary>
    public (Vector3 Position, Vector3 Rotation)? Pose(int id)
    {
        if (Get(id) is not { } m)
        {
            return null;
        }
        // The top hanging on something missing (a swivel whose vehicle is gone): the mod never places the chain.
        if (Chain(m) is not { Count: > 1 } chain || chain[^1].Link.Holder != 0)
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

    /// <summary>The root of an object's group (the top of its chain; itself when it hangs on nothing found).</summary>
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
    /// Every object of the group an object belongs to: its root first, then every object whose chain leads there. Just
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
