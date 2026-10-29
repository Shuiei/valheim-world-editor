using System.Text.Json.Nodes;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Reading the tie rules of a manifest: objects tied to a root that names them (ObjectTies).
/// </summary>
public static partial class ModManifests
{
    /// <summary>The most bytes one id of a byte array may be.</summary>
    private const int MostIdBytes = 64;

    /// <summary>The tie rules the manifests give (ObjectTies), in the order first read.</summary>
    public static IReadOnlyList<TieRule> Ties => _notes.TieList;

    /// <summary>Adds the tie rules of a manifest's "ties" array to the notes (those in the expected form).</summary>
    private static void ReadTies(JsonNode? node, Notes notes)
    {
        if (node is not JsonArray ties)
        {
            return;
        }
        foreach (var e in ties)
        {
            if (e is JsonObject o && Tie(o) is { } rule)
            {
                // In place of one of the same keys, where it stood: a newer file's word wins.
                notes.Ties[(rule.RootKey, rule.MemberKey)] = rule;
            }
        }
    }

    /// <summary>
    /// A tie rule; null when it is not in the expected form: "root" and "member" given; "idBytes" (1 to 64) and "name"
    /// optional.
    /// </summary>
    private static TieRule? Tie(JsonObject o)
    {
        if (!Text(o["root"], out string? root) || root == null || !Text(o["member"], out string? member) || member == null
            || !Text(o["name"], out string? name))
        {
            return null;
        }
        int size = 0;
        if (o["idBytes"] is { } n && (n is not JsonValue v || !v.TryGetValue(out size) || size is < 1 or > MostIdBytes))
        {
            return null;
        }
        return new TieRule(name ?? LinkRule.DefaultName, StableHash.Of(root), StableHash.Of(member), size);
    }
}
