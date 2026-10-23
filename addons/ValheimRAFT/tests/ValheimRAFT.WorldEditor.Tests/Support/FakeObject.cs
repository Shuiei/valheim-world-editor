using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor.Tests;

/// <summary>
/// An object as the editor hands it to the add-on, its values kept by key; changed in place as a pasted copy is.
/// </summary>
internal sealed class FakeObject(string prefab, Vector3 position = default, Quaternion? orientation = null, float scale = 1) : ICopy
{
    public Dictionary<string, int> Ints { get; } = [];

    public Dictionary<string, Vector3> Vectors { get; } = [];

    public Dictionary<string, float> Floats { get; } = [];

    public Dictionary<string, byte[]> Bytes { get; } = [];

    public string Prefab => prefab;

    public Vector3 Position => position;

    public Vector3 Rotation => Vector3.Zero;

    public Quaternion Orientation => orientation ?? Quaternion.Identity;

    public float Scale => scale;

    public bool TryGetInt(string key, out int value) => Ints.TryGetValue(key, out value);

    public bool TryGetLong(string key, out long value)
    {
        value = 0;
        return false;
    }

    public bool TryGetFloat(string key, out float value) => Floats.TryGetValue(key, out value);

    public bool TryGetVector3(string key, out Vector3 value) => Vectors.TryGetValue(key, out value);

    public bool TryGetQuaternion(string key, out Quaternion value)
    {
        value = default;
        return false;
    }

    public bool TryGetString(string key, out string value)
    {
        value = "";
        return false;
    }

    public bool TryGetBytes(string key, out byte[] value) => Bytes.TryGetValue(key, out value!);

    public void SetInt(string key, int? value) => Set(Ints, key, value);

    public void SetLong(string key, long? value) => throw new NotSupportedException();

    public void SetFloat(string key, float? value) => throw new NotSupportedException();

    public void SetVector3(string key, Vector3? value) => Set(Vectors, key, value);

    public void SetString(string key, string? value) => throw new NotSupportedException();

    public void SetBytes(string key, byte[]? value)
    {
        if (value == null)
        {
            Bytes.Remove(key);
        }
        else
        {
            Bytes[key] = value;
        }
    }

    private static void Set<T>(Dictionary<string, T> values, string key, T? value)
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
}

/// <summary>A world of the objects given, found by the id they hold under <see cref="Keys.Id"/>.</summary>
internal sealed class FakeWorld(params FakeObject[] objects) : IWorldView
{
    public bool TryFind(string idKey, int id, out IObjectView obj)
    {
        obj = objects.FirstOrDefault(o => o.TryGetInt(idKey, out int held) && held == id)!;
        return obj != null;
    }
}

/// <summary>A world of no object whose ground stands at one height (none: the editor has no ground there).</summary>
internal sealed class FakeGround(float? height) : IWorldView, IGroundView
{
    public bool TryFind(string idKey, int id, out IObjectView obj)
    {
        obj = null!;
        return false;
    }

    public bool TryGroundHeight(float x, float z, out float h)
    {
        h = height ?? 0;
        return height != null;
    }
}

/// <summary>A paste's new ids for the originals' it copied.</summary>
internal sealed class FakeIds(Dictionary<int, int> map) : IIdMap
{
    public bool TryGetNew(int oldId, out int newId) => map.TryGetValue(oldId, out newId);
}
