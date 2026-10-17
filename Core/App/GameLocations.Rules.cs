// The rule of one ZoneLocation and the list fields it is read from (see GameLocations).

using AssetsTools.NET;
using ValheimGen;

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// A list field's items (its Array's, when it has one).
    /// </summary>
    private static List<AssetTypeValueField> Items(AssetTypeValueField list) => list["Array"].IsDummy ? list.Children : list["Array"].Children;

    /// <summary>
    /// How many items a list field has (0 when it is missing).
    /// </summary>
    private static int Count(AssetTypeValueField list) => list.IsDummy ? 0 : Items(list).Count;

    /// <summary>
    /// One ZoneLocation: each rule property from the field of the same name (m_ before it). <c>altBiome</c>: the alt
    /// biome adding it, null for the main lists. The prefab is named by the manifest from its asset ID, else by
    /// m_prefabName.
    /// </summary>
    private static SeedLocations.Rule Location(AssetTypeValueField l, Manifest manifest, string? altBiome)
    {
        var rule = new SeedLocations.Rule { altBiome = altBiome };
        foreach (var prop in typeof(SeedLocations.Rule).GetProperties())
        {
            var f = l["m_" + prop.Name];
            if (f.IsDummy || f.Value == null)
            {
                continue;
            }
            if (prop.PropertyType == typeof(int))
            {
                prop.SetValue(rule, f.Value.ValueType == AssetValueType.Bool ? (f.AsBool ? 1 : 0) : f.AsInt);
            }
            else if (prop.PropertyType == typeof(float))
            {
                prop.SetValue(rule, f.AsFloat);
            }
            else if (prop.PropertyType == typeof(string) && prop.Name != nameof(SeedLocations.Rule.altBiome))
            {
                prop.SetValue(rule, f.AsString);
            }
        }
        var id = l["m_prefab"]["m_assetID"];
        string hex = $"{id["v3"].AsUInt:x8}{id["v2"].AsUInt:x8}{id["v1"].AsUInt:x8}{id["v0"].AsUInt:x8}";
        rule.prefab = manifest.Names.GetValueOrDefault(hex) ?? l["m_prefabName"].AsString;
        return rule;
    }
}
