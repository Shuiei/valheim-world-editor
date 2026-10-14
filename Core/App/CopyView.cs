using System.Globalization;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// One object of a pasted copy as extensions rewrite it (ICopy): its values as data text (ObjectField, as the
/// clipboard keeps them), read and changed in place. A value that does not parse counts as none.
/// </summary>
/// <param name="prefab">Its prefab's name ("" when not known).</param>
/// <param name="position">Where it is pasted.</param>
/// <param name="rotation">Its turn, Unity Euler degrees.</param>
/// <param name="data">Its values; changed in place (a value taken away is kept with a null value, as copies do).</param>
public sealed class CopyView(string prefab, Vector3 position, Vector3 rotation, List<ObjectField> data) : ICopy
{
    /// <inheritdoc/>
    public string Prefab => prefab;

    /// <inheritdoc/>
    public Vector3 Position => position;

    /// <inheritdoc/>
    public Vector3 Rotation => rotation;

    /// <summary>Its values, as changed.</summary>
    public List<ObjectField> Data => data;

    /// <summary>Whether it holds a value of a key (by hash).</summary>
    public bool Holds(int key) => data.Exists(f => f.Key == key && f.Value != null);

    /// <summary>A value's text; null when it has none.</summary>
    private string? Text(string section, string key)
    {
        int h = StableHash.Of(key);
        return data.Find(f => f.Key == h && f.Section == section)?.Value;
    }

    /// <summary>Sets a value's text (null: takes it away).</summary>
    private void Put(string section, string key, string? value)
    {
        int h = StableHash.Of(key);
        int i = data.FindIndex(f => f.Key == h && f.Section == section);
        if (i >= 0)
        {
            data[i] = data[i] with { Value = value };
        }
        else if (value != null)
        {
            data.Add(new ObjectField(section, h, value));
        }
    }

    /// <summary>Numbers of a value's text, exactly <paramref name="n"/> of them; null otherwise.</summary>
    private float[]? Floats(string section, string key, int n)
    {
        string[] parts = Text(section, key)?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries) ?? [];
        float[] v = new float[n];
        if (parts.Length != n)
        {
            return null;
        }
        for (int i = 0; i < n; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
            {
                return null;
            }
        }
        return v;
    }

    /// <summary>Numbers as a value's text.</summary>
    private static string Join(params float[] v) => string.Join(' ', v.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));

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
        catch (FormatException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public void SetInt(string key, int? value) => Put("ints", key, value?.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public void SetLong(string key, long? value) => Put("longs", key, value?.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public void SetFloat(string key, float? value) => Put("floats", key, value?.ToString("R", CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public void SetVector3(string key, Vector3? value) => Put("vec3", key, value is { } v ? Join(v.X, v.Y, v.Z) : null);

    /// <inheritdoc/>
    public void SetString(string key, string? value) => Put("strings", key, value);

    /// <inheritdoc/>
    public void SetBytes(string key, byte[]? value) => Put("bytes", key, value == null ? null : Convert.ToBase64String(value));
}
