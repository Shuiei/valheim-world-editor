using System.Numerics;
using System.Text.Json;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>Takes the pieces by name, as GameCatalogues' PlaceScan writes them, in place of any before.</summary>
    public static void Use(string json) => _all = Read(json);

    /// <summary>The pieces by name, as GameCatalogues' PlaceScan writes them; a collider of a kind not known is left out.</summary>
    internal static Dictionary<string, Data> Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, Data>();
        foreach (JsonProperty p in doc.RootElement.EnumerateObject())
        {
            var f = p.Value.GetProperty("f");
            bool Flag(string k) => f.TryGetProperty(k, out var v) && v.GetInt32() != 0;
            var snaps = p.Value.GetProperty("s").EnumerateArray().Select(a => new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle())).ToArray();
            var shapes = new List<Shape>();
            foreach (var c in p.Value.GetProperty("c").EnumerateArray())
            {
                float F(int i) => c[i].GetSingle();
                switch (c[0].GetString())
                {
                    case "b":
                        shapes.Add(new Box(new(F(1), F(2), F(3)), new(F(4), F(5), F(6)), new(F(7), F(8), F(9), F(10))));
                        break;
                    case "s":
                        shapes.Add(new Sphere(new(F(1), F(2), F(3)), F(4)));
                        break;
                    case "c":
                        shapes.Add(new Capsule(new(F(1), F(2), F(3)), new(F(4), F(5), F(6)), F(7)));
                        break;
                    case "m":
                        float[] v = [.. c[2].EnumerateArray().Select(x => x.GetSingle())];
                        shapes.Add(new Mesh(c[1].GetInt32() != 0, [.. Enumerable.Range(0, v.Length / 3).Select(i => new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]))],
                            [.. c[3].EnumerateArray().Select(x => x.GetInt32())]));
                        break;
                    default:
                        break;
                }
            }
            result[p.Name] = new Data(Flag("g"), Flag("cg"), Flag("ce"), Flag("ro"), snaps, [.. shapes]);
        }
        return result;
    }

    /// <summary>A piece's data, or null when the scan has no such piece.</summary>
    public static Data? Get(string prefab) => _all.GetValueOrDefault(prefab);

    /// <summary>The longest reach (<see cref="Data.Radius"/>) of any piece the scan has (0 for none).</summary>
    public static float MaxRadius() => _all.Values.Select(d => d.Radius).DefaultIfEmpty(0).Max();
}
