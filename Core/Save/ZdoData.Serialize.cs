using System.Numerics;
using System.Text;

namespace TerrainEditor.Save;

public sealed partial class ZdoData
{
    /// <summary>
    /// The object in the save format of world versions 41 and 42; empty sections are left out and a near-zero rotation
    /// is not stored.
    /// </summary>
    public byte[] Serialize()
    {
        Vector3 euler = new(ZdoBuilder.Wrap(Rotation.X), ZdoBuilder.Wrap(Rotation.Y), ZdoBuilder.Wrap(Rotation.Z));
        bool rotated = euler.LengthSquared() > 1e-6f;
        ushort flags = (ushort)(BaseFlags & KeptFlags);
        bool small = SmallPosition && Position.Y == 0f && Fits(Position.X) && Fits(Position.Z);
        if (small)
        {
            flags |= SmallPositionFlag;
        }

        if (rotated)
        {
            flags |= RotationFlag;
        }

        if (Connection != null)
        {
            flags |= Connections;
        }

        if (FloatList.Count > 0)
        {
            flags |= Floats;
        }

        if (Vec3List.Count > 0)
        {
            flags |= Vec3s;
        }

        if (QuatList.Count > 0)
        {
            flags |= Quats;
        }

        if (IntList.Count > 0)
        {
            flags |= Ints;
        }

        if (LongList.Count > 0)
        {
            flags |= Longs;
        }

        if (StringList.Count > 0)
        {
            flags |= Strings;
        }

        if (ByteList.Count > 0)
        {
            flags |= ByteArrays;
        }

        using MemoryStream ms = new();
        using BinaryWriter w = new(ms, Encoding.UTF8);
        w.Write(flags);
        if (small)
        {
            w.Write((short)Position.X);
            w.Write((short)Position.Z);
        }
        else
        {
            w.Write(Position.X); w.Write(Position.Y); w.Write(Position.Z);
        }
        w.Write(Prefab);
        if (rotated)
        {
            ZdoBuilder.WriteSmallRotation(w, euler);
        }
        if (Connection != null)
        {
            w.Write(Connection);
        }
        void Each<T>(List<(int Key, T Value)> list, Action<T> write)
        {
            if (list.Count == 0)
            {
                return;
            }
            ZdoBuilder.WriteNumItems(w, list.Count);
            foreach (var (k, v) in list)
            {
                w.Write(k);
                write(v);
            }
        }
        Each(FloatList, w.Write);
        Each(Vec3List, v => { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); });
        Each(QuatList, v => { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); w.Write(v.W); });
        Each(IntList, w.Write);
        Each(LongList, w.Write);
        Each(StringList, w.Write);
        Each(ByteList, v => { w.Write(v.Length); w.Write(v); });
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Whether a coordinate can be stored in the short position form (a whole number within a short).
    /// </summary>
    private static bool Fits(float v) => v == MathF.Round(v) && v >= short.MinValue && v <= short.MaxValue;
}
