using System.Globalization;
using NVector3 = System.Numerics.Vector3;

namespace ValheimGen;

/// <summary>
/// The icons the game's map shows for a world's locations (ZoneSystem.GetLocationIcons, with Expand World Data's
/// replacement of it): a kind's icon wherever it is laid out, or once its zone is made; named after the kind, or as
/// Expand World Data's location file names it.
/// </summary>
public static class LocationIcons
{
    /// <summary>One icon on the map.</summary>
    /// <param name="X">World x.</param>
    /// <param name="Z">World z.</param>
    /// <param name="Name">The icon's name (the minimap's location icons are named after their location).</param>
    /// <param name="Size">Its size: times the game's under 5, else metres wide on the map.</param>
    public sealed record Icon(float X, float Z, string Name, float Size);

    /// <summary>The icons of these location instances, by their kinds' rules (the world's own).</summary>
    /// <param name="instances">The world's location instances: the kind's hash, where, whether its zone is made.</param>
    /// <param name="kinds">The world's location kinds.</param>
    public static List<Icon> Of(IEnumerable<(int Hash, NVector3 Position, bool Placed)> instances, IReadOnlyList<LocationRule> kinds)
    {
        var byHash = new Dictionary<int, LocationRule>();
        foreach (var k in kinds)
        {
            byHash.TryAdd(TerrainEditor.Save.StableHash.Of(k.Prefab), k);
        }
        List<Icon> icons = [];
        foreach (var (hash, at, placed) in instances)
        {
            if (byHash.TryGetValue(hash, out var kind) && NameOf(kind, placed) is { } text)
            {
                icons.Add(Parsed(at, text));
            }
        }
        return icons;
    }

    /// <summary>
    /// The icon text of one instance: with Expand World Data's names, the placed one once its zone is made (when it
    /// has one), else the always one; without them, the kind's name when it always shows, or shows once placed.
    /// </summary>
    private static string? NameOf(LocationRule kind, bool placed)
    {
        if (kind.IconAlwaysName == null && kind.IconPlacedName == null)
        {
            return kind.IconAlways != 0 || (kind.IconPlaced != 0 && placed) ? kind.Prefab : null;
        }
        string placedName = Named(kind.IconPlacedName, kind.Prefab), always = Named(kind.IconAlwaysName, kind.Prefab);
        return placed && placedName != "" ? placedName : always != "" ? always : null;
    }

    /// <summary>A field's icon text: "true" the kind's name, "false" or nothing none, anything else as it is.</summary>
    private static string Named(string? field, string prefab) => field switch
    {
        "true" => prefab,
        null or "false" => "",
        _ => field,
    };

    /// <summary>
    /// An icon from its text "name[, size[, animate]]" (the mod's MinimapIcon: a size of 0 or less is the game's). Any
    /// third field makes the game's icon pulse (its pin's m_animate); the editor's map is a still picture, so it is
    /// drawn at its size without it.
    /// </summary>
    private static Icon Parsed(NVector3 at, string text)
    {
        string[] parts = [.. text.Split(',').Select(p => p.Trim()).Where(p => p != "")];
        float size = parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0f ? s : 1f;
        return new Icon(at.X, at.Z, parts.Length > 0 ? parts[0] : text, size);
    }
}
