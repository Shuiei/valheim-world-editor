using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Reading the values of a manifest: the optional texts, flags and name lists.
/// </summary>
public static partial class ModManifests
{
    /// <summary>An optional list of names: absent (empty) or an array of at most 32 texts.</summary>
    private static bool Names(JsonNode? n, out string[] names)
    {
        names = [];
        if (n is null)
        {
            return true;
        }
        if (n is not JsonArray a || a.Count > 32)
        {
            return false;
        }
        var list = new List<string>();
        foreach (var e in a)
        {
            if (!Text(e, out string? s) || s == null)
            {
                return false;
            }
            list.Add(s);
        }
        names = [.. list];
        return true;
    }

    /// <summary>An optional string: absent (null) or a non-empty string of at most 200 characters.</summary>
    private static bool Text(JsonNode? n, out string? text)
    {
        text = null;
        if (n is null)
        {
            return true;
        }
        if (n is JsonValue v && v.TryGetValue(out string? s) && s.Trim().Length is > 0 and <= 200)
        {
            text = s.Trim();
            return true;
        }
        return false;
    }

    /// <summary>An optional true or false: absent (false) or a boolean.</summary>
    private static bool Flag(JsonNode? n, out bool flag)
    {
        flag = false;
        return n is null || n is JsonValue v && v.TryGetValue(out flag);
    }
}
