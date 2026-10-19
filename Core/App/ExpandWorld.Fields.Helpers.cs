namespace TerrainEditor.App;

// Small conversions the files' entries need again and again: a flag as the rules keep one, a fraction of the radius,
// a setting two mods both patch.
public static partial class ExpandWorld
{
    /// <summary>A true-or-false field as the rules keep one (1 or 0), with its default when absent.</summary>
    private static int Bit(Fields f, string key, bool fallback) => f.B(key, fallback) ? 1 : 0;

    /// <summary>A fraction of the world's radius, in metres (the mod's conversion, in double, rounded once).</summary>
    private static float Dist(float fraction, float radius) => (float)((double)fraction * radius);

    /// <summary>A fraction of the radius in metres when it is positive; a zero or negative value as it is (the mod's sentinels).</summary>
    private static float DistWhenPositive(float fraction, float radius) => fraction > 0f ? Dist(fraction, radius) : fraction;

    /// <summary>Half a size given as a fraction of the radius, metres; no bound (infinity) when the size is not given.</summary>
    private static float HalfOf(float? size, float radius) => size is { } s ? (float)((double)Dist(s, radius) / 2.0) : float.PositiveInfinity;

    /// <summary>A wiggle's width: the entry's own, else the default when the entry's switch for it is on, else none.</summary>
    private static float WiggleWidth(Fields f, string widthKey, string switchKey, float width) => f.FOpt(widthKey) ?? (f.B(switchKey, true) ? width : 0f);

    /// <summary>True as a flag kept only when set (null otherwise), as the rules keep their rare flags.</summary>
    private static bool? OrNull(bool flag) => flag ? true : null;

    /// <summary>
    /// A setting both Expand World Size and Data patch: Data's when it is away from the game's value, else Size's. The
    /// comparison is exact on purpose: the mod decides the same way, and a value a hair from the game's is a change.
    /// </summary>
    private static float Overriding(float data, float game, float size) => data != game ? data : size;

    /// <summary>A switch both Expand World Size and Data patch: on only when neither turns it off.</summary>
    private static bool BothOn(string? size, string? data, bool fallback) => Bool(size, fallback) && Bool(data, fallback);
}
