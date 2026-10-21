using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Reading one manifest into the notes: its version, its data and prefab notes, its extension and its links.
/// </summary>
public static partial class ModManifests
{
    /// <summary>
    /// Adds what a manifest says to <paramref name="notes"/>; false (nothing added) when it is not a manifest of a
    /// version this editor reads. Entries not in the expected form are left out.
    /// </summary>
    /// <param name="json">The manifest's text.</param>
    /// <param name="notes">What is added to.</param>
    /// <param name="from">
    /// The manifest's folder and its mod (null for one given in code or lying in plugins itself, which offers no extension).
    /// </param>
    /// <exception cref="JsonException">Not JSON.</exception>
    private static bool Read(string json, Notes notes, (string Folder, string Mod)? from)
    {
        if (ParseManifest(json) is not { } root)
        {
            return false;
        }
        Read(root, notes, from, addOn: false);
        return true;
    }

    /// <summary>Adds what a manifest read says to <paramref name="notes"/> (its extension an add-on's for <paramref name="addOn"/>).</summary>
    private static void Read(JsonObject root, Notes notes, (string Folder, string Mod)? from, bool addOn)
    {
        ReadData(root["data"], notes);
        ReadPrefabs(root["prefabs"], notes);
        ReadExtension(root, from, notes, addOn);
        ReadLinks(root["links"], notes);
    }

    /// <summary>The manifest's root object, when the text is a JSON object of a version this editor reads; else null.</summary>
    /// <exception cref="JsonException">Not JSON.</exception>
    private static JsonObject? ParseManifest(string json)
    {
        if (JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is not JsonObject root
            || !IsSupportedVersion(root["version"]))
        {
            return null;
        }
        return root;
    }

    /// <summary>Whether a manifest's "version" is a whole number from 1 to the version this editor reads.</summary>
    private static bool IsSupportedVersion(JsonNode? node) => node is JsonValue v && v.TryGetValue(out int version) && version >= 1 && version <= Version;
}
