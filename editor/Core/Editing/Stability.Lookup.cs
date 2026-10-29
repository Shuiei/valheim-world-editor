using System.Numerics;
using System.Text.Json;

namespace TerrainEditor.Editing;

public static partial class Stability
{
    /// <summary>Takes the pieces by name, as GameCatalogues' SupportScan writes them, in place of any before.</summary>
    public static void Use(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, Data>();
        foreach (JsonProperty p in doc.RootElement.EnumerateObject())
        {
            var c = p.Value.GetProperty("c");
            var boxes = p.Value.GetProperty("b").EnumerateArray().Select(b =>
            {
                float F(int i) => b[i].GetSingle();
                return new Box(new Vector3(F(0), F(1), F(2)), new Vector3(F(3), F(4), F(5)), new Quaternion(F(6), F(7), F(8), F(9)), b[10].GetInt32() != 0, SupportLayers.Contains(b[11].GetInt32()), b[12].GetInt32() != 0);
            }).ToArray();
            result[p.Name] = new Data(p.Value.GetProperty("m").GetInt32(), p.Value.GetProperty("s").GetInt32() != 0,
                new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle()), boxes, p.Value.TryGetProperty("f", out var f) && f.GetInt32() != 0);
        }
        _pieces = result;
    }

    /// <summary>
    /// Whether the support data has this piece: a building piece with a WearNTear (a wall, a beam, a
    /// workbench too), so its support can be worked out. Whether other pieces can rest on it is its
    /// <c>Data.Supports</c>.
    /// </summary>
    public static bool Known(string prefab) => _pieces.ContainsKey(prefab);

    /// <summary>
    /// A piece's material (WearNTear: 0 wood, 1 stone, 2 iron… -1 unknown) and its colliders as boxes in
    /// its own frame (middle, half size, rotation): its rough shape, for pictures without its model.
    /// </summary>
    public static (int Material, List<(Vector3 C, Vector3 H, Quaternion Q)> Boxes) Shape(string prefab) =>
        _pieces.TryGetValue(prefab, out var d) ? (d.Material, d.Boxes.Select(b => (b.C, b.H, b.Q)).ToList()) : (-1, []);

    /// <summary>WearNTear.GetMaterialProperties: maximum and minimum support, losses per metre (all 0 for an unknown material).</summary>
    public static (float Max, float Min, float Horizontal, float Vertical) Material(int m) => m switch
    {
        0 => (100f, 10f, 0.2f, 0.125f),          // wood
        1 => (1000f, 100f, 1f, 0.125f),          // stone
        2 => (1500f, 20f, 1f / 13f, 1f / 13f),   // iron
        3 => (140f, 10f, 1f / 6f, 0.1f),         // hardwood (core wood)
        4 => (1500f, 100f, 0.5f, 0.125f),        // marble (black marble)
        5 => (2000f, 100f, 1f / 3f, 0.1f),       // ashstone (grausten)
        6 => (5000f, 100f, 0.25f, 1f / 15f),     // ancient
        7 => (1000f, 100f, 1f / 3f, 0.125f),     // ice
        8 => (200f, 10f, 0.2f, 1f / 13f),        // timberwood
        _ => (0f, 0f, 0f, 0f),
    };
}
