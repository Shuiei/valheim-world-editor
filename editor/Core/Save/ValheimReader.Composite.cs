using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>
/// The read side of Valheim's ZPackage for composite values: vectors, quaternions, small rotations and item counts.
/// </summary>
public sealed partial class ValheimReader
{
    /// <summary>Three floats: x, y, z.</summary>
    public Vector3 ReadVector3() => new(ReadSingle(), ReadSingle(), ReadSingle());

    /// <summary>A quaternion: x, y, z, then w.</summary>
    public Quaternion ReadQuaternion() => new(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

    /// <summary>A Vector2s: two shorts, such as a sector's x and y.</summary>
    public (short X, short Y) ReadVector2s() => (_reader.ReadInt16(), _reader.ReadInt16());

    /// <summary>ZPackage.ReadSmallRotation: Euler angles in half degrees; 2 bytes for a pure Y rotation.</summary>
    /// <returns>The Euler angles in degrees.</returns>
    public Vector3 ReadSmallRotation()
    {
        uint num = _reader.ReadUInt16();
        if ((num & 0x8000) != 0)
        {
            return new Vector3(0f, (num & 0x7FFF) * 0.5f, 0f);
        }
        num = (num << 16) | _reader.ReadUInt16();
        return new Vector3(num & 0x3FF, (num >> 10) & 0x3FF, (num >> 20) & 0x3FF) * 0.5f;
    }

    /// <summary>ZPackage.ReadNumItems: 1 byte, or 2 bytes when the high bit is set (world version &gt;= 33).</summary>
    public int ReadNumItems(int worldVersion) => worldVersion < WorldVersion.NumItems ? _reader.ReadByte() : _reader.ReadNumItems();
}
