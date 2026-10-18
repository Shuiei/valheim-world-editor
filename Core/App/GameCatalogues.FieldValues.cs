using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The game data's values as JSON has them: a field read as a value, the rounding of floats, and the kinds of MonoBehaviour.
/// </summary>
public static partial class GameCatalogues
{
    // ---- Values, as the game data's JSON has them.

    /// <summary>
    /// Unity's class id of a GameObject.
    /// </summary>
    private const int GameObjectType = (int)AssetClassID.GameObject;
    /// <summary>
    /// Unity's class id of a Transform.
    /// </summary>
    private const int TransformType = (int)AssetClassID.Transform;
    /// <summary>
    /// Unity's class id of a MonoBehaviour.
    /// </summary>
    private const int MonoBehaviourType = (int)AssetClassID.MonoBehaviour;

    /// <summary>
    /// The "snappoint" tag (the first custom tag).
    /// </summary>
    private const int SnapTag = 20000;

    /// <summary>
    /// Whether a field is present (AssetsTools gives a dummy for a missing one).
    /// </summary>
    private static bool Has(AssetTypeValueField f, string name) => !f[name].IsDummy;

    /// <summary>
    /// A field's value: ints (the game's bools are bytes: 0 or 1), floats (as doubles), strings; lists
    /// and structures as lists and dictionaries; null when there is none.
    /// </summary>
    private static object? Py(AssetTypeValueField f)
    {
        if (f.IsDummy)
        {
            return null;
        }
        if (f.Value != null && f.Value.ValueType != AssetValueType.Array)
        {
            return f.Value.ValueType switch
            {
                AssetValueType.Bool => f.AsBool,
                AssetValueType.Int8 => (long)f.AsSByte,
                AssetValueType.UInt8 => (long)f.AsByte,
                AssetValueType.Int16 => (long)f.AsShort,
                AssetValueType.UInt16 => (long)f.AsUShort,
                AssetValueType.Int32 => (long)f.AsInt,
                AssetValueType.UInt32 => (long)f.AsUInt,
                AssetValueType.Int64 => f.AsLong,
                AssetValueType.UInt64 => (long)f.AsULong,
                AssetValueType.Float => (double)f.AsFloat,
                AssetValueType.Double => f.AsDouble,
                AssetValueType.String => f.AsString,
                _ => throw new NotSupportedException($"field {f.TemplateField.Name}: {f.Value.ValueType}"),
            };
        }
        if (f.TemplateField.IsArray)
        {
            return f.Children.Select(Py).ToList();
        }
        if (f.Children.Count == 1 && f.Children[0].TemplateField.IsArray)
        {
            return Py(f.Children[0]);
        }
        var d = new CatalogueJson.Dict();
        foreach (var c in f.Children)
        {
            d[c.TemplateField.Name] = Py(c);
        }
        return d;
    }

    /// <summary>
    /// A field's value, or the fallback when there is no such field.
    /// </summary>
    private static object? Get(AssetTypeValueField f, string name, object? fallback) => Has(f, name) ? Py(f[name]) : fallback;

    /// <summary>
    /// Rounded to n decimals: floats only (an int stays one).
    /// </summary>
    internal static object? Round(object? v, int n) => v is double d ? CatalogueJson.Round(d, n) : v;

    /// <summary>
    /// Rounded half to even at n decimals, as Python's round.
    /// </summary>
    internal static double R(double v, int n) => CatalogueJson.Round(v, n);

    /// <summary>
    /// The larger, the first when they are equal (as Python's max: -0.0 before 0 stays -0.0).
    /// </summary>
    internal static double Max(double a, double b) => b > a ? b : a;

    /// <summary>
    /// Whether a MonoBehaviour's fields are a Piece's (m_category, m_resources, m_craftingStation).
    /// </summary>
    private static bool IsPiece(AssetTypeValueField f) => Has(f, "m_category") && Has(f, "m_resources") && Has(f, "m_craftingStation");
}
