using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// An object as the editor hands it to the add-on, read or pasted (ICopy): its values by type and key name, set in
/// place (null takes one away).
/// </summary>
internal sealed class FakeObject(string prefab) : ICopy
{
    public string Prefab => prefab;

    public Vector3 Position { get; init; }

    public Vector3 Rotation { get; init; }

    public Quaternion Orientation { get; init; } = Quaternion.Identity;

    public float Scale => 1;

    public Dictionary<string, int> Ints { get; } = [];

    public Dictionary<string, long> Longs { get; } = [];

    public Dictionary<string, float> Floats { get; } = [];

    public Dictionary<string, Vector3> Vectors { get; } = [];

    public Dictionary<string, Quaternion> Quaternions { get; } = [];

    public Dictionary<string, string> Strings { get; } = [];

    public Dictionary<string, byte[]> Bytes { get; } = [];

    /// <summary>Every key it holds a value under, whatever its type.</summary>
    public IEnumerable<string> Keys =>
        Ints.Keys.Concat(Longs.Keys).Concat(Floats.Keys).Concat(Vectors.Keys).Concat(Quaternions.Keys).Concat(Strings.Keys).Concat(Bytes.Keys);

    public bool TryGetInt(string key, out int value) => Ints.TryGetValue(key, out value);

    public bool TryGetLong(string key, out long value) => Longs.TryGetValue(key, out value);

    public bool TryGetFloat(string key, out float value) => Floats.TryGetValue(key, out value);

    public bool TryGetVector3(string key, out Vector3 value) => Vectors.TryGetValue(key, out value);

    public bool TryGetQuaternion(string key, out Quaternion value) => Quaternions.TryGetValue(key, out value);

    public bool TryGetString(string key, out string value) => Strings.TryGetValue(key, out value!);

    public bool TryGetBytes(string key, out byte[] value) => Bytes.TryGetValue(key, out value!);

    public void SetInt(string key, int? value) => Put(Ints, key, value);

    public void SetLong(string key, long? value) => Put(Longs, key, value);

    public void SetFloat(string key, float? value) => Put(Floats, key, value);

    public void SetVector3(string key, Vector3? value) => Put(Vectors, key, value);

    public void SetQuaternion(string key, Quaternion? value) => Put(Quaternions, key, value);

    public void SetString(string key, string? value) => PutRef(Strings, key, value);

    public void SetBytes(string key, byte[]? value) => PutRef(Bytes, key, value);

    private static void Put<T>(Dictionary<string, T> values, string key, T? value)
        where T : struct
    {
        if (value is { } v)
        {
            values[key] = v;
        }
        else
        {
            values.Remove(key);
        }
    }

    private static void PutRef<T>(Dictionary<string, T> values, string key, T? value)
        where T : class
    {
        if (value != null)
        {
            values[key] = value;
        }
        else
        {
            values.Remove(key);
        }
    }
}
