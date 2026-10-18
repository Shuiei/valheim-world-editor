using System.Runtime.CompilerServices;

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
public static partial class ObjectLinks
{
    /// <summary>How many parents a part's chain may have (a deeper one is taken for damaged data).</summary>
    public const int MaxDepth = 16;

    /// <summary>The links read from added objects' bytes, by those bytes, with the rules they were read by.</summary>
    private static readonly ConditionalWeakTable<byte[], Tuple<LinkSet, ObjectLink?>> RawLinks = [];

    /// <summary>The rules in use: the mods' manifests' (ModManifests).</summary>
    public static LinkSet Current => ModManifests.Links;

}
