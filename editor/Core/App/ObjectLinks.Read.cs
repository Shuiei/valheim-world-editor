using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Reading a link from an object's saved values: by the rules in use, from all its data or from the values given.
/// </summary>
public static partial class ObjectLinks
{
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
        int Int(int key) => ZdoData.TryGet(ints, key, out int v) ? v : 0;
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
}
