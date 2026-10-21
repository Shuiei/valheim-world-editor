using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>What an edit's data setter writes (an ICopy keeping only byte arrays), to read a record the tool made back.</summary>
internal sealed class RecordCopy(string prefab = "TunnelColumnData") : ICopy
{
    public Dictionary<string, byte[]> Bytes { get; } = [];

    public string Prefab => prefab;

    public Vector3 Position => Vector3.Zero;

    public Vector3 Rotation => Vector3.Zero;

    public Quaternion Orientation => Quaternion.Identity;

    public float Scale => 1;

    /// <summary>The record written under the mod's key.</summary>
    public ColumnRecord Record => ColumnRecord.Parse(Bytes[TunnelheimMod.RecordKeyName]);

    /// <summary>What a data setter writes.</summary>
    public static RecordCopy Of(Action<ICopy> set)
    {
        var copy = new RecordCopy();
        set(copy);
        return copy;
    }

    public void SetBytes(string key, byte[]? value)
    {
        if (value != null)
        {
            Bytes[key] = value;
        }
    }

    public void SetInt(string key, int? value)
    {
    }

    public void SetLong(string key, long? value)
    {
    }

    public void SetFloat(string key, float? value)
    {
    }

    public void SetVector3(string key, Vector3? value)
    {
    }

    public void SetString(string key, string? value)
    {
    }

    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        return false;
    }

    public bool TryGetLong(string key, out long value)
    {
        value = 0;
        return false;
    }

    public bool TryGetFloat(string key, out float value)
    {
        value = 0;
        return false;
    }

    public bool TryGetVector3(string key, out Vector3 value)
    {
        value = default;
        return false;
    }

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
}
