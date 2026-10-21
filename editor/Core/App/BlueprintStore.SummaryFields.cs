using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// The values of a blueprint file that <see cref="BlueprintStore"/> reads for its summaries.
/// </summary>
public sealed partial class BlueprintStore
{
    /// <summary>
    /// The copy has ground when any point of its relative heights is set (-32768 marks "no ground").
    /// </summary>
    private static bool HasGround(JsonElement clip) =>
        clip.TryGetProperty("rel", out JsonElement rel) && rel.ValueKind == JsonValueKind.Array
        && rel.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.Null && v.GetInt32() != -32768);

    /// <summary>A text value of an object; null when missing or null (InvalidOperationException when not text).</summary>
    private static string? Text(JsonElement o, string key) =>
        o.TryGetProperty(key, out JsonElement v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;

    /// <summary>A whole number of an object; 0 when missing or null (FormatException or InvalidOperationException when not one).</summary>
    private static int Number(JsonElement o, string key) =>
        o.TryGetProperty(key, out JsonElement v) && v.ValueKind != JsonValueKind.Null ? v.GetInt32() : 0;
}
