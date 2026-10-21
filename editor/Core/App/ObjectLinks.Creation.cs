using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Making the links of new objects: a link named by the data an object holds, the link of a placed group member, and a new id.
/// </summary>
public static partial class ObjectLinks
{
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
