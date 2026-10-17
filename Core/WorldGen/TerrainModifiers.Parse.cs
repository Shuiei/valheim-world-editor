using System.Numerics;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

public sealed partial class TerrainModifiers
{
    /// <summary>
    /// Reads terrain-modifiers.json into modifiers by prefab name hash.
    /// </summary>
    private static Dictionary<int, (string, bool, List<Template>)> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        Dictionary<int, (string, bool, List<Template>)> result = [];
        foreach (JsonProperty p in doc.RootElement.EnumerateObject())
        {
            List<Template> mods = [];
            foreach (JsonElement m in p.Value.GetProperty("modifiers").EnumerateArray())
            {
                JsonElement pos = m.GetProperty("pos");
                bool B(string k) => m.GetProperty(k).GetInt32() != 0;
                float F(string k) => m.GetProperty(k).GetSingle();
                var paint = (Paint)m.GetProperty("paintType").GetInt32();
                if (!Enum.IsDefined(paint))
                {
                    throw new InvalidDataException($"{p.Name}: unknown terrain paint type {(int)paint}");
                }
                mods.Add(new Template(new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle()), B("level"), F("levelOffset"), F("levelRadius"), B("square"),
                    B("smooth"), F("smoothRadius"), F("smoothPower"), B("paintCleared"), F("paintRadius"), paint, F("paintStrength"), B("paintHeightCheck"),
                    m.GetProperty("sortOrder").GetInt32(), B("player")));
            }
            result[StableHash.Of(p.Name)] = (p.Name, p.Value.GetProperty("location").GetBoolean(), mods);
        }
        return result;
    }
}
