using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// The linked objects of a world as they are now (its objects and those added in this session, deleted ones left out):
/// which object holds which id, where each part is drawn, and which objects make one group.
/// </summary>
public sealed partial class LinkIndex
{
    /// <summary>An object with a link.</summary>
    /// <param name="Id">Its object id (negative for one added in this session).</param>
    /// <param name="Position">Its position as stored, in metres.</param>
    /// <param name="Rotation">Its turn as stored, Unity Euler degrees.</param>
    /// <param name="Link">What its data says.</param>
    public sealed record Member(int Id, Vector3 Position, Vector3 Rotation, ObjectLink Link)
    {
        /// <summary>Its prefab's hash (0: not known, an index of members alone): an extension may place it (ILinkPlacer).</summary>
        public int Prefab { get; init; }
    }

    /// <summary>The objects with a link, by id.</summary>
    private readonly Dictionary<int, Member> _members = [];

    /// <summary>
    /// The object holding each id, by its rule's id key and the id (the first one read when two hold it): an id under
    /// another rule's key (another mod's) is not the same one.
    /// </summary>
    private readonly Dictionary<(int IdKey, int Id), int> _holders = [];

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
        var made = new Dictionary<int, NewObject>();
        ix._read = id => ObjectData.Bytes(world, made.GetValueOrDefault, id, WorldSave.Builder) is { } bytes ? ZdoData.Parse(bytes) : null;
        foreach (var (id, (link, rotation)) in world.Links)
        {
            if (link.Id != 0)
            {
                ix.Ids.Add(link.Id);
            }
            if (!deleted.Contains(id))
            {
                ix.Add(new Member(id, world.ObjectRefs[id].Position, rotation, link) { Prefab = world.ObjectRefs[id].Prefab });
            }
        }
        foreach (var n in added)
        {
            made[n.Id] = n;
            if (ObjectLinks.LinkOf(world, n) is { } link)
            {
                if (link.Id != 0)
                {
                    ix.Ids.Add(link.Id);
                }
                ix.Add(new Member(n.Id, n.Position, n.Rotation, link) { Prefab = n.Prefab });
            }
        }
        ix.Join();
        return ix;
    }

    /// <summary>The links of these objects alone (tests, and objects that are no world's).</summary>
    /// <param name="members">The objects.</param>
    /// <param name="data">Each object's data by id, for the extensions' link placers (null: none is read).</param>
    public static LinkIndex Of(IEnumerable<Member> members, Func<int, ZdoData?>? data = null)
    {
        var ix = new LinkIndex { _read = data };
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
            _holders.TryAdd((m.Link.Rule.IdKey, m.Link.Id), m.Id);
        }
    }

    /// <summary>Hangs each object on what it names, once all are in.</summary>
    private void Join()
    {
        foreach (var m in _members.Values)
        {
            if (HolderOf(m) is int up && up != m.Id)
            {
                (_children.TryGetValue(up, out var list) ? list : _children[up] = []).Add(m.Id);
            }
        }
    }

    /// <summary>An object with a link, by id; null when it has none.</summary>
    public Member? Get(int id) => _members.GetValueOrDefault(id);

    /// <summary>The object holding an id of a rule's (the first read when two hold it); null when none does.</summary>
    /// <param name="idKey">The rule's id key (hash): an id under another key is not it.</param>
    /// <param name="id">The id.</param>
    public Member? Holder(int idKey, int id) => _holders.TryGetValue((idKey, id), out int o) ? _members[o] : null;

}
