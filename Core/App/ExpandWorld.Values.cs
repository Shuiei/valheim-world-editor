using System.Globalization;

namespace TerrainEditor.App;

// A setting's text as a value, as the mods read it.
public static partial class ExpandWorld
{
    /// <summary>A setting's number, as the mod reads it (invariant culture); the default when missing or not a number.</summary>
    internal static float Float(string? value, float fallback) => Float(value) ?? fallback;

    /// <summary>A setting's number, or null when missing, empty or not a number (an unset optional setting).</summary>
    internal static float? Float(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f) ? f : null;

    /// <summary>A setting's whole number; the default when missing or not one.</summary>
    internal static int Int(string? value, int fallback) => Int(value) ?? fallback;

    /// <summary>A setting's whole number, or null when missing or not one.</summary>
    internal static int? Int(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;

    /// <summary>A text as the mod keeps a name it may leave blank: null when blank, else without spaces around.</summary>
    internal static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A setting's true or false; the default when missing or neither.</summary>
    internal static bool Bool(string? value, bool fallback) => bool.TryParse(value, out bool b) ? b : fallback;
}
