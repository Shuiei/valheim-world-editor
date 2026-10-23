using System.Numerics;

namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>Sets a byte array value, replacing one with the same key.</summary>
    public void SetBytes(int key, byte[] value) => Put(ByteList, key, value, v => v);

    /// <summary>Sets a vector value, replacing one with the same key.</summary>
    public void SetVec3(int key, Vector3 value) => Put(Vec3List, key, value, v => v);

    /// <summary>Sets an int value, replacing one with the same key; null takes it away.</summary>
    public void SetInt(int key, int? value) => Put(IntList, key, value, v => v!.Value);

    /// <summary>Sets a long value, replacing one with the same key; null takes it away.</summary>
    public void SetLong(int key, long? value) => Put(LongList, key, value, v => v!.Value);

    /// <summary>Sets a float value, replacing one with the same key; null takes it away.</summary>
    public void SetFloat(int key, float? value) => Put(FloatList, key, value, v => v!.Value);

    /// <summary>Sets a string value, replacing one with the same key; null takes it away.</summary>
    public void SetString(int key, string? value) => Put(StringList, key, value, v => v);

    /// <summary>Sets a vector value, replacing one with the same key; null takes it away.</summary>
    public void SetVec3(int key, Vector3? value) => Put(Vec3List, key, value, v => v!.Value);

    /// <summary>Sets a quaternion value, replacing one with the same key; null takes it away.</summary>
    public void SetQuat(int key, Quaternion? value) => Put(QuatList, key, value, v => v!.Value);

    /// <summary>Sets a byte array value, replacing one with the same key; null takes it away.</summary>
    public void SetBytesOrNone(int key, byte[]? value) => Put(ByteList, key, value, v => v);

    /// <summary>The byte array value with this key, or null when there is none.</summary>
    public byte[]? GetBytes(int key) => ByteList.FirstOrDefault(i => i.Key == key).Value;

    /// <summary>The int value with this key, or null when there is none.</summary>
    public int? GetInt(int key) => TryGet(IntList, key, out int v) ? v : null;
}
