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
        var own = new ParentPlacement(Key(offset), Key(euler), Key(quat), stored != "own", false);
        if (Placements(o["placements"], parents, own) is not { } placements)
        {
            return null;
        }
        return (id, new LinkRule(name ?? LinkRule.DefaultName, Key(id), [.. parents.Select(Key)], own.OffsetKey, own.EulerKey, own.QuatKey,
            [.. references.Select(Key)], [.. drops.Select(Key)], own.StoredAtRoot, placements.Any(p => p != null) ? placements : null));
    }

    /// <summary>A data key's hash; 0 for none.</summary>
    private static int Key(string? n) => n == null ? 0 : StableHash.Of(n);

    /// <summary>
    /// A rule's "placements" by its parent keys' order (null entries: the rule's own): each a parent key of the rule
    /// with the offset, rotation and rotationQuaternion keys and storedAt of a part hanging on it (each one not given:
    /// the rule's), or carried (true: the part keeps no offset; the group carries it where the game saved it). Null when
    /// it is not in that form.
    /// </summary>
    private static ParentPlacement?[]? Placements(JsonNode? node, string[] parents, ParentPlacement own)
    {
        var placements = new ParentPlacement?[parents.Length];
        if (node is null)
        {
            return placements;
        }
        if (node is not JsonObject o)
        {
            return null;
        }
        foreach (var (key, value) in o)
        {
            int i = Array.IndexOf(parents, key);
            if (i < 0 || value is not JsonObject p || !Flag(p["carried"], out bool carried) || !Text(p["offset"], out string? offset)
                || !Text(p["rotation"], out string? euler) || !Text(p["rotationQuaternion"], out string? quat)
                || !Text(p["storedAt"], out string? stored) || stored is not (null or "root" or "own")
                || carried && (offset ?? euler ?? quat ?? stored) != null)
            {
                return null;
            }
            placements[i] = carried
                ? new ParentPlacement(0, 0, 0, false, true)
                : new ParentPlacement(offset != null ? Key(offset) : own.OffsetKey, euler != null ? Key(euler) : own.EulerKey,
                    quat != null ? Key(quat) : own.QuatKey, stored == null ? own.StoredAtRoot : stored == "root", false);
        }
        return placements;
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
