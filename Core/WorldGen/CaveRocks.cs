using System.Numerics;
using System.Text.Json;

namespace TerrainEditor.Terrain;

// The cave roof rocks' meshes (each prefab's, in its own frame), for the Path tool's Cave (CaveRoof),
// read from the player's own Valheim (GameData; the tests use tests/fixtures/cave-rocks.json).
public static class CaveRocks
{
    public sealed record Shape(Vector3[] V, int[] T);

    // None until Use: no world opens before then (GameData).
    private static volatile Dictionary<string, Shape> _shapes = [];

    // The shapes by prefab, as GameCatalogues.CaveRocksJson writes them (vertices "v", triangles "t").
    public static void Use(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var shapes = new Dictionary<string, Shape>();
        foreach (JsonProperty p in doc.RootElement.EnumerateObject())
        {
            var v = p.Value.GetProperty("v").EnumerateArray().Select(a => new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle())).ToArray();
            int[] t = [.. p.Value.GetProperty("t").EnumerateArray().Select(a => a.GetInt32())];
            shapes[p.Name] = new Shape(v, t);
        }
        _shapes = shapes;
    }

    public static Shape Of(string prefab) => _shapes[prefab];
}
