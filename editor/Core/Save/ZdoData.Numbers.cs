using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>
    /// Exactly <paramref name="n"/> numbers from a value's text ("x y z"; commas and semicolons part them too), as
    /// <see cref="Set"/> reads vec3 and quats; null when it holds another count, or something that is no number.
    /// </summary>
    public static float[]? Numbers(string? text, int n)
    {
        string[] parts = text?.Split(NumberSeparators, StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length != n)
        {
            return null;
        }
        float[] v = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (!float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i]))
            {
                return null;
            }
        }
        return v;
    }

    /// <summary>Whether Set takes this value for that section (the numbers it needs, base64 for bytes).</summary>
    public static bool Valid(string section, string? value)
    {
        try
        {
            new ZdoData().Set(section, 0, value);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            Log.CaughtBrief(ex, $"checking a value for the {section} section (not taken)");
            return false;
        }
    }
}
