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
        bool small = SmallPosition && Position.Y == 0f && Fits(Position.X) && Fits(Position.Z);
        ushort flags = SerializeFlags(small, rotated);
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
        WriteParts(w);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Whether a coordinate can be stored in the short position form (a whole number within a short).
    /// </summary>
    private static bool Fits(float v) => v == MathF.Round(v) && v >= short.MinValue && v <= short.MaxValue;
}
