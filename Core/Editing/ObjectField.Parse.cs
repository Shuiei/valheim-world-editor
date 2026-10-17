using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial record ObjectField
{
    /// <summary>
    /// Sets a value in a list of fields, replacing the one of the same section and key; null takes it away (kept with a
    /// null value when the list had it, as copies keep a value taken away).
    /// </summary>
    public static void Put(List<ObjectField> data, string section, int key, string? value)
    {
        int i = data.FindIndex(f => f.Key == key && f.Section == section);
        if (i >= 0)
        {
            data[i] = data[i] with { Value = value };
        }
        else if (value != null)
        {
            data.Add(new ObjectField(section, key, value));
        }
    }

    /// <summary>A field from its text form; null when it is malformed or its value does not fit its section.</summary>
    public static ObjectField? Parse(string text)
    {
        string[] p = text.Split(';');
        if (p.Length != 3 || p[0] is not ("floats" or "vec3" or "quats" or "ints" or "longs" or "strings" or "bytes")
            || !int.TryParse(p[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int key))
        {
            return null;
        }
        try
        {
            string? value = p[2] == "-" ? null : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[2]));
            // A value the save cannot take (a damaged or hand-edited line) is left out here, not when the world is written.
            return ZdoData.Valid(p[0], value) ? new ObjectField(p[0], key, value) : null;
        }
        catch (FormatException e)
        {
            Log.CaughtBrief(e, "reading an object field line of the kept edits (not base64: left out)");
            return null;
        }
    }
}
