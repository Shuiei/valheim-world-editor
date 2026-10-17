using System.Globalization;
using TerrainEditor.Editing;

namespace TerrainEditor.App;

/// <summary>
/// Remapping each part of a copied group: its new id, where it stands, and the values the rules leave out of copies.
/// </summary>
public static partial class ObjectLinks
{
    /// <summary>
    /// The top of a part's chain in the copy (its root, or the part it hangs on when that one's parent is not copied).
    /// </summary>
    private static int TopOfPart(List<ObjectLink?> links, Dictionary<int, int> at, int i)
    {
        var seen = new HashSet<int> { i };
        while (links[i] is { Holder: not 0 } l && at.TryGetValue(l.Holder, out int up) && seen.Add(up))
        {
            i = up;
        }
        return i;
    }

    /// <summary>The object at <paramref name="i"/> of a copy with a link, remapped (its new id, references, place and values).</summary>
    private static NewObject RemapPart(IReadOnlyList<NewObject> copy, List<ObjectLink?> links, int i, ObjectLink link, Dictionary<int, int> at,
        Dictionary<int, int> ids, ISet<int> taken, Random random, bool mirror, IReadOnlySet<int> rewritten)
    {
        var o = copy[i];
        var rule = link.Rule;
        var data = o.Data!.ToList();
        if (link.Id != 0)
        {
            // Another object holding the same id (damaged data) gets one of its own: the parts follow the first.
            int id = at[link.Id] == i ? ids[link.Id] : NewId(taken, random);
            Put(data, "ints", rule.IdKey, id.ToString(CultureInfo.InvariantCulture));
        }
        bool attached = link.Holder != 0 && at.ContainsKey(link.Holder);
        RemapReferences(data, rule, attached, ids);
        var position = o.Position;
        if (attached)
        {
            if (rule.StoredAtRoot)
            {
                position = copy[TopOfPart(links, at, i)].Position;
            }
            if (mirror)
            {
                MirrorPlacement(data, rule, link);
            }
        }
        else
        {
            ClearPlacement(data, rule);
        }
        DropUnkept(data, rule, rewritten);
        return o with { Position = position, Data = data };
    }

    /// <summary>A part whose parent was not copied is no part any more: its parent and placement values are taken away.</summary>
    private static void ClearPlacement(List<ObjectField> data, LinkRule rule)
    {
        foreach (int key in rule.ParentKeys)
        {
            Put(data, "ints", key, null);
        }
        Put(data, "vec3", rule.OffsetKey, null);
        Put(data, "vec3", rule.EulerKey, null);
        Put(data, "quats", rule.QuatKey, null);
    }

    /// <summary>Takes away the values the rule leaves out of copies (unless an extension rewrites them).</summary>
    private static void DropUnkept(List<ObjectField> data, LinkRule rule, IReadOnlySet<int> rewritten)
    {
        for (int k = 0; k < data.Count; k++)
        {
            if (rule.DropKeys.Contains(data[k].Key) && !rewritten.Contains(data[k].Key))
            {
                data[k] = data[k] with { Value = null };
            }
        }
    }
}
