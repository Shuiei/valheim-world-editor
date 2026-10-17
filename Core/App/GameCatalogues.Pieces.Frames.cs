using AssetsTools.NET;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// A child's frame from its parent's: (position, rotation, scale), all in the prefab's frame.
    /// </summary>
    private static (V3 P, Quat Q, V3 S) ChildFrame(AssetTypeValueField tr, V3 pos, Quat rot, V3 scale)
    {
        var p = Add(rot.Rotate(Scale(Vec(tr["m_LocalPosition"]), scale)), pos);
        return (p, rot.Mul(Quat.Of(tr["m_LocalRotation"])), Scale(scale, Vec(tr["m_LocalScale"])));
    }

    /// <summary>
    /// A prefab root's position as the files have it: whole numbers.
    /// </summary>
    private static List<object?> Origin() => [0L, 0L, 0L];

    /// <summary>
    /// The first smallest (as Python's min).
    /// </summary>
    private static double Min(IEnumerable<double> xs) => xs.Aggregate((a, x) => x < a ? x : a);

    /// <summary>
    /// The first largest (as Python's max).
    /// </summary>
    private static double Max(IEnumerable<double> xs) => xs.Aggregate((a, x) => x > a ? x : a);
}
