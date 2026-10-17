
namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// Whether two objects are the same but for where they stand and how they are turned: prefab, flags, connection
    /// and every value (in any order).
    /// </summary>
    private static bool SameButPlace(ZdoData a, ZdoData b)
    {
        static bool Same<T>(List<(int Key, T Value)> x, List<(int Key, T Value)> y, Func<T, T, bool> eq)
        {
            if (x.Count != y.Count)
            {
                return false;
            }
            var byKey = new Dictionary<int, T>(x.Count);
            foreach (var (k, v) in x)
            {
                byKey[k] = v;
            }
            return y.All(e => byKey.TryGetValue(e.Key, out T? v) && eq(v, e.Value));
        }
        return a.Prefab == b.Prefab && a.BaseFlags == b.BaseFlags
            && (a.Connection ?? []).AsSpan().SequenceEqual(b.Connection ?? [])
            && Same(a.FloatList, b.FloatList, (p, q) => p.Equals(q))
            && Same(a.Vec3List, b.Vec3List, (p, q) => p.Equals(q))
            && Same(a.QuatList, b.QuatList, (p, q) => p.Equals(q))
            && Same(a.IntList, b.IntList, (p, q) => p == q)
            && Same(a.LongList, b.LongList, (p, q) => p == q)
            && Same(a.StringList, b.StringList, string.Equals)
            && Same(a.ByteList, b.ByteList, (p, q) => p.AsSpan().SequenceEqual(q));
    }
}
