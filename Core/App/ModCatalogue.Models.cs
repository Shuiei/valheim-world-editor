using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// The models of the mods' prefabs: taking the models a game reports, reading one model's JSON and naming its parts.
/// </summary>
public static partial class ModCatalogue
{
    /// <summary>A model's JSON (checked by IsModel) as a record.</summary>
    private static Model ToModel(JsonObject o)
    {
        static float[] Floats(JsonNode? n) => [.. n!.AsArray().Select(x => (float)x!.GetValue<double>())];
        var parts = o["p"]!.AsArray().Select(p => new ModelPart(
            Plain(p!["mesh"]!.GetValue<string>()), p["v"]!.GetValue<int>(), p["sub"]!.GetValue<int>(),
            p["mat"] is { } m ? Plain(m.GetValue<string>()) : null, Floats(p["m"]), p["b"] is { } b ? Floats(b) : null)).ToList();
        return new Model(Floats(o["s"]), parts);
    }

    /// <summary>
    /// A name without what Unity adds to the copies made while the game runs (" (Instance)", " Instance").
    /// </summary>
    internal static string Plain(string name)
    {
        foreach (string end in (string[])[" (Instance)", " Instance"])
        {
            while (name.EndsWith(end, StringComparison.Ordinal))
            {
                name = name[..^end.Length];
            }
        }
        return name;
    }

    /// <summary>
    /// Takes a game's models (POST /models) of the mod prefabs known: each replaces the one there was, and the whole is
    /// kept. Models not in the expected form, and of prefabs that are not known mods' ones, are left out.
    /// </summary>
    /// <returns>How many models were taken.</returns>
    /// <exception cref="JsonException">The answer is not a JSON object.</exception>
    public static int TakeModels(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("the models are not a JSON object");
        lock (Writing)
        {
            var now = _data;
            var models = new Dictionary<string, JsonNode>(now.Models);
            int taken = 0;
            bool changed = false;
            foreach (var (name, e) in Entries(root))
            {
                if (now.Prefabs.ContainsKey(name) && IsModel(e))
                {
                    taken++;
                    changed |= Put(models, name, e.DeepClone());
                }
            }
            if (changed)
            {
                var next = now with { Models = models };
                // Kept first, as Take does.
                SafeFile.WriteAllText(FilePath, Write(next));
                Use(next);
            }
            return taken;
        }
    }
}
