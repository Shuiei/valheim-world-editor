using System.Globalization;
using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// The values of <see cref="CopyView"/> read as typed (ICopy.TryGet*).
/// </summary>
public sealed partial class CopyView
{
    /// <inheritdoc/>
    public bool TryGetInt(string key, out int value) => int.TryParse(Text("ints", key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryGetLong(string key, out long value) => long.TryParse(Text("longs", key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryGetFloat(string key, out float value) => float.TryParse(Text("floats", key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryGetVector3(string key, out Vector3 value)
    {
        float[]? v = Floats("vec3", key, 3);
        value = v == null ? default : new Vector3(v[0], v[1], v[2]);
        return v != null;
    }

    /// <inheritdoc/>
    public bool TryGetQuaternion(string key, out Quaternion value)
    {
        float[]? v = Floats("quats", key, 4);
        value = v == null ? default : new Quaternion(v[0], v[1], v[2], v[3]);
        return v != null;
    }

    /// <inheritdoc/>
    public bool TryGetString(string key, out string value)
    {
        value = Text("strings", key)!;
        return value != null;
    }

    /// <inheritdoc/>
    public bool TryGetBytes(string key, out byte[] value)
    {
        value = [];
        if (Text("bytes", key) is not { } text)
        {
            return false;
        }
        try
        {
            value = Convert.FromBase64String(text);
            return true;
        }
        catch (FormatException e)
        {
            Log.CaughtBrief(e, "reading copied bytes (not base64: left out)");
            return false;
        }
    }
}
