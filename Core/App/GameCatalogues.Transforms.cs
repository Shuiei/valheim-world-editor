using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// The scene of the game data: the children of a Transform, and the positions and scales of objects.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// The children of a Transform, as GameObjects' path ids (children that cannot be read left out).
    /// </summary>
    private static IEnumerable<long> ChildObjects(Bundle b, AssetTypeValueField tr)
    {
        foreach (var ch in tr["m_Children.Array"].Children)
        {
            if (b.Field(ch["m_PathID"].AsLong) is { } ct)
            {
                yield return ct["m_GameObject.m_PathID"].AsLong;
            }
        }
    }

    /// <summary>
    /// A Vector3 field as doubles.
    /// </summary>
    private static (double X, double Y, double Z) Vec(AssetTypeValueField v) => (v["x"].AsFloat, v["y"].AsFloat, v["z"].AsFloat);

    /// <summary>
    /// The sum of two vectors.
    /// </summary>
    private static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>
    /// Two vectors multiplied part by part.
    /// </summary>
    private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, (double X, double Y, double Z) s) => (a.X * s.X, a.Y * s.Y, a.Z * s.Z);
}
