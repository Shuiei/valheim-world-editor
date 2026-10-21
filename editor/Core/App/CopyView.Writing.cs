using System.Globalization;
using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// The values of <see cref="CopyView"/> set from typed values (ICopy.Set*).
/// </summary>
public sealed partial class CopyView
{
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
