using ValheimGen;

namespace TerrainEditor.App;

// A blueprint location's radius, and the game's kinds as Expand World Size changes them.
public static partial class ExpandWorld
{
    /// <summary>
    /// The radius of a blueprint location: half the diagonal of its pieces' bounds on the ground, as the mod measures
    /// it, from the blueprint file named by the prefab (with .blueprint or .vbuild) in the blueprint folder; null when
    /// there is no such file, or it cannot be read (noted).
    /// </summary>
    internal static float? BlueprintRadius(string? folder, string prefab, List<string> notes)
    {
        if (folder == null || !Directory.Exists(folder))
        {
            return null;
        }
        string? file = BlueprintExtensions.Select(ext => Path.Combine(folder, prefab + ext)).FirstOrDefault(File.Exists);
        if (file == null)
        {
            return null;
        }
        try
        {
            var parsed = BlueprintFormats.Parse(Path.GetFileName(file), File.ReadAllText(file));
            if (parsed.Pieces.Count == 0)
            {
                return 0f;
            }
            float minX = parsed.Pieces.Min(p => p.Position.X), maxX = parsed.Pieces.Max(p => p.Position.X);
            float minZ = parsed.Pieces.Min(p => p.Position.Z), maxZ = parsed.Pieces.Max(p => p.Position.Z);
            float ex = (maxX - minX) / 2f, ez = (maxZ - minZ) / 2f;
            return (float)Math.Sqrt((double)ex * ex + (double)ez * ez);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            Log.Caught(ex, $"reading the blueprint {file} for its radius (the location is left out)");
            notes.Add($"expand_locations: the blueprint {Path.GetFileName(file)} could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>The file name extensions of the blueprint files the mod reads.</summary>
    private static readonly string[] BlueprintExtensions = [".blueprint", ".vbuild"];

    /// <summary>
    /// The game's own kinds as Expand World Size changes them, when it does: each enabled kind's quantity (but the
    /// start's) times its locations multiplier, rounded, at least 1 (0 for a multiplier of 0); and, without Expand
    /// World Data, their distances from the middle scaled with the radius. The kinds as given when nothing changes.
    /// </summary>
    /// <param name="rules">The kinds.</param>
    /// <param name="r">The world's rules.</param>
    internal static List<SeedLocations.Rule> SizedLocations(IReadOnlyList<SeedLocations.Rule> rules, WorldRules r)
    {
        bool quantities = r.LocationsMultiplier != 1f, distances = !r.DataInstalled && r.Radius != 10000f;
        if (!quantities && !distances)
        {
            return [.. rules];
        }
        List<SeedLocations.Rule> sized = [];
        foreach (var rule in rules)
        {
            var copy = SeedLocations.RuleSet.FromJson("{\"locations\":[" + System.Text.Json.JsonSerializer.Serialize(rule) + "]}").Locations[0];
            // What the file changes about its contents is not part of the JSON.
            copy.Contents = rule.Contents;
            if (quantities && copy.Quantity != 0 && copy.Enable != 0 && copy.Prefab != StartLocation)
            {
                copy.Quantity = r.LocationsMultiplier == 0f ? 0 : Math.Max(1, (int)Math.Round((double)copy.Quantity * r.LocationsMultiplier, MidpointRounding.ToEven));
            }
            if (distances)
            {
                copy.MinDistance = (float)((double)copy.MinDistance * (r.Radius / 10000f));
                copy.MaxDistance = (float)((double)copy.MaxDistance * (r.Radius / 10000f));
            }
            sized.Add(copy);
        }
        return sized;
    }
}
