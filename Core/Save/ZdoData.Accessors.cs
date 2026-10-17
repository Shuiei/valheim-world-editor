using System.Numerics;

namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>Sets a byte array value, replacing one with the same key.</summary>
    public void SetBytes(int key, byte[] value) => Put(ByteList, key, value, v => v);

    /// <summary>Sets a vector value, replacing one with the same key.</summary>
    public void SetVec3(int key, Vector3 value) => Put(Vec3List, key, value, v => v);

    /// <summary>The byte array value with this key, or null when there is none.</summary>
    public byte[]? GetBytes(int key) => ByteList.FirstOrDefault(i => i.Key == key).Value;

    /// <summary>The int value with this key, or null when there is none.</summary>
    public int? GetInt(int key) => TryGet(IntList, key, out int v) ? v : null;
}
