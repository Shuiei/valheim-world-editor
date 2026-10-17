using System.Globalization;
using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.App;

/// <summary>
/// Remapping a copied part's ids and references to the copy's new ids, and mirroring its placement.
/// </summary>
public static partial class ObjectLinks
{
    /// <summary>
    /// Gives each id the copy's objects hold a new one: the first object holding it is the one it is taken from (<c>at</c>),
    /// and each id is new in the world (drawn from <paramref name="taken"/>, which gets it).
    /// </summary>
    private static (Dictionary<int, int> At, Dictionary<int, int> Ids) NewCopyIds(List<ObjectLink?> links, ISet<int> taken, Random random)
    {
        var at = new Dictionary<int, int>();
        var ids = new Dictionary<int, int>();
        for (int i = 0; i < links.Count; i++)
        {
            if (links[i] is { Id: not 0 } l && at.TryAdd(l.Id, i))
            {
                ids[l.Id] = NewId(taken, random);
            }
        }
        return (at, ids);
    }

    /// <summary>
    /// Points each reference of a part at the copy's new id for what it names; a reference to something not copied goes,
    /// and so does a part's parent when the part was not copied under one.
    /// </summary>
    private static void RemapReferences(List<ObjectField> data, LinkRule rule, bool attached, Dictionary<int, int> ids)
    {
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
    }

    /// <summary>A part pasted mirrored: its offset and turns, in its parent's frame, are mirrored too.</summary>
    private static void MirrorPlacement(List<ObjectField> data, LinkRule rule, ObjectLink link)
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
