using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>A blueprint as a copy (the clipboard format), its name and the world it came from, or null.</summary>
    /// <param name="id">The blueprint's id ("hs:" and its file name, or its store id).</param>
    /// <param name="problem">Why a blueprint file could not be made a copy, when that is why (null otherwise).</param>
    private (JsonObject Clip, string Name, string? World, bool Dungeon)? Load(string id, out Said? problem)
    {
        problem = null;
        if (id.StartsWith(HomesteadId, StringComparison.Ordinal))
        {
            string path = Path.Combine(Status.Folder, id[HomesteadId.Length..]);
            if (Path.GetFileName(path) != id[HomesteadId.Length..] || !File.Exists(path))
            {
                return null;
            }
            string text = File.ReadAllText(path);
            var parsed = BlueprintFormats.Parse(path, text);
            var world = _scene()?.World;
            JsonObject clip;
            try
            {
                clip = BlueprintFormats.ToClip(parsed, n => world?.CanCreate(StableHash.OfName(n)) ?? Terrain.PrefabCatalog.Get(StableHash.OfName(n)) != null, out _);
            }
            catch (InvalidDataException ex)
            {
                Log.CaughtBrief(ex, "turning a blueprint file into a copy to paste");
                problem = Said.Of("blueprints.msg.unreadableWhy", ex.Message);
                return null;
            }
            // A dungeon by its tags: pasted, it goes up behind an entrance.
            return (clip, parsed.Name, null, new Homestead.Details(parsed.Name, "", Homestead.Details.ParseTags(TagsOf(text))).Dungeon);
        }
        if (Store.Read(id) is not string json || JsonNode.Parse(json) is not JsonObject doc || doc["clip"] is not JsonObject c)
        {
            return null;
        }
        return (c, (string?)doc["name"] ?? id, (string?)doc["world"], false);
    }
}
