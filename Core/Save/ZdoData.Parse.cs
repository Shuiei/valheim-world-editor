
namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>Reads one object's save bytes.</summary>
    /// <param name="bytes">The object as saved, from its flags to its last value.</param>
    /// <param name="worldVersion">
    /// The world version it was written in (older ones carry a sector and a full rotation).
    /// </param>
    public static ZdoData Parse(byte[] bytes, int worldVersion = 41) => Parse(bytes, 0, bytes.Length, worldVersion);

    /// <summary>Reads the object saved in part of a buffer (a chunk file's bytes, say).</summary>
    /// <param name="buffer">The bytes the object is in.</param>
    /// <param name="start">Where the object starts (at its flags).</param>
    /// <param name="length">Its length in bytes; nothing past it is read.</param>
    /// <param name="worldVersion">The world version it was written in.</param>
    public static ZdoData Parse(byte[] buffer, int start, int length, int worldVersion)
    {
        using ValheimReader r = new(new MemoryStream(buffer, start, length));
        ZdoReader read = new(r, worldVersion);
        ZdoData z = new()
        {
            BaseFlags = (ushort)(read.Flags & KeptFlags),
            Position = read.Position,
            SmallPosition = read.SmallPosition,
            Prefab = read.Prefab,
            Rotation = read.Rotation,
        };
        while (read.Next())
        {
            int k = read.Key;
            switch (read.Section)
            {
                case Connections: z.Connection = read.Connection; break;
                case Floats: z.FloatList.Add((k, read.FloatValue)); break;
                case Vec3s: z.Vec3List.Add((k, read.Vec3Value)); break;
                case Quats: z.QuatList.Add((k, read.QuatValue)); break;
                case Ints: z.IntList.Add((k, read.IntValue)); break;
                case Longs: z.LongList.Add((k, read.LongValue)); break;
                case Strings: z.StringList.Add((k, read.StringValue!)); break;
                default: z.ByteList.Add((k, read.BytesValue!)); break;
            }
        }
        return z;
    }
}
