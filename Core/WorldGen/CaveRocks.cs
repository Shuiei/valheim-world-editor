using System.Numerics;
using System.Text.Json;

namespace TerrainEditor.Terrain;

/// <summary>
/// The cave roof rocks' meshes (each prefab's, in its own frame), for the Path tool's Cave (CaveRoof),
/// read from the player's own Valheim (GameData; the tests use tests/fixtures/cave-rocks.json).
/// </summary>
public static class CaveRocks
{
    /// <summary>
    /// One rock prefab's mesh in the prefab's own frame.
    /// </summary>
    /// <param name="V">The vertices.</param>
    /// <param name="T">The triangles: three indices into <paramref name="V"/> each.</param>
    public sealed record Shape(Vector3[] V, int[] T);

    /// <summary>
    /// None until Use: no world opens before then (GameData).
    /// </summary>
    private static volatile Dictionary<string, Shape> _shapes = [];

    /// <summary>
    /// Takes the shapes by prefab, as GameCatalogues.CaveRocksJson writes them (vertices "v", triangles "t"),
    /// replacing any read before.
    /// </summary>
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

    /// <summary>
    /// The mesh of a rock prefab; throws when the prefab is not one of the cave rocks read by Use.
    /// </summary>
    public static Shape Of(string prefab) => _shapes[prefab];
}
