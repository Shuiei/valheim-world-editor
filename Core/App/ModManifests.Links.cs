using System.Text.Json.Nodes;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Reading the link rules of a manifest: how objects link into one (ObjectLinks), and their options.
/// </summary>
public static partial class ModManifests
{
    /// <summary>Adds the link rules of a manifest's "links" array to the notes (those in the expected form).</summary>
    private static void ReadLinks(JsonNode? node, Notes notes)
    {
        if (node is not JsonArray links)
        {
            return;
        }
        foreach (var e in links)
        {
            if (e is JsonObject o && Link(o) is var (id, rule))
            {
                // In place of one of the same id, where it stood: the first rule an object matches wins.
                notes.Links[id] = rule;
            }
        }
    }

    /// <summary>
    /// A link rule (ObjectLinks) and the name of its id key; null when it is not in the expected form: "id", "parents"
    /// (at least one) and "offset" given; "rotation", "rotationQuaternion", "references", "dropOnCopy", "name" and
    /// "storedAt" ("root", the default, or "own") optional.
    /// </summary>
    private static (string Id, LinkRule Rule)? Link(JsonObject o)
    {
        if (LinkPlacement(o) is not { } placement || LinkOptions(o) is not { } options)
        {
            return null;
        }
        var (id, parents, offset, euler, quat) = placement;
        var (references, drops, name, stored) = options;
        static int Key(string? n) => n == null ? 0 : StableHash.Of(n);
        return (id, new LinkRule(name ?? LinkRule.DefaultName, Key(id), [.. parents.Select(Key)], Key(offset), Key(euler), Key(quat),
            [.. references.Select(Key)], [.. drops.Select(Key)], stored != "own"));
    }

    /// <summary>The option fields of a link rule: its references, the keys dropped on copy, its name and where it is stored.</summary>
    private static (string[] References, string[] Drops, string? Name, string? Stored)? LinkOptions(JsonObject o)
    {
        if (!Names(o["references"], out string[] references) || !Names(o["dropOnCopy"], out string[] drops)
            || !Text(o["name"], out string? name) || !Text(o["storedAt"], out string? stored) || stored is not (null or "root" or "own"))
        {
            return null;
        }
        return (references, drops, name, stored);
    }
}
