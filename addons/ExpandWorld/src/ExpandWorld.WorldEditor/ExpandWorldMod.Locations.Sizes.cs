using ValheimGen;

namespace ExpandWorld.WorldEditor;

// A blueprint location's radius, and the game's kinds as Expand World Size changes them.
public static partial class ExpandWorldMod
{
    /// <summary>
    /// The radius of a blueprint location as the mod measures it (Blueprint.Radius): half the diagonal on the ground of
    /// its pieces' bounds (holding the file's origin), or its terrain snapshots' reach when wider, from the blueprint file
    /// of that name in the blueprint folder (found as the mod finds it); null when there is no such file, or it cannot
    /// be read (noted).
    /// </summary>
    internal static float? BlueprintRadius(string? folder, string prefab, List<string> notes) =>
        ReadBlueprint(BlueprintFiles(folder), prefab, DataTable.Empty, [], notes)?.Radius;

    /// <summary>
    /// The game's own kinds as Expand World Size changes them, when it does: each enabled kind's quantity (but the
    /// start's) times its locations multiplier, rounded, at least 1 (0 for a multiplier of 0); and, without Expand
    /// World Data, their distances from the middle scaled with the radius. The kinds as given when nothing changes.
    /// </summary>
    /// <param name="rules">The kinds.</param>
    /// <param name="r">The world's rules.</param>
    internal static List<LocationRule> SizedLocations(IReadOnlyList<LocationRule> rules, WorldRules r)
    {
        bool quantities = r.LocationsMultiplier != 1f, distances = !r.DataInstalled && r.Radius != 10000f;
        if (!quantities && !distances)
        {
            return [.. rules];
        }
        List<LocationRule> sized = [];
        foreach (var rule in rules)
        {
            var copy = LocationRuleSet.FromJson("{\"locations\":[" + System.Text.Json.JsonSerializer.Serialize(rule) + "]}").Locations[0];
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
