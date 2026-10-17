using System.Numerics;

namespace TerrainEditor.Save;

public static partial class ZdoBuilder
{
    /// <summary>
    /// A new object with no data, for a prefab the world has no example of; the game fills in the rest when it loads
    /// it.
    /// </summary>
    /// <param name="prefab">The prefab name hash.</param>
    /// <param name="baseFlags">The prefab's persistent / distant / type bits (PrefabCatalog).</param>
    /// <param name="position">World position in metres.</param>
    /// <param name="euler">Unity Euler angles in degrees.</param>
    /// <param name="scale">Uniform scale; stored only when above 0 and not 1.</param>
    /// <param name="ints">Int data to store (key hash, value), such as a location proxy's location and seed; none by default.</param>
    public static byte[] Blank(int prefab, ushort baseFlags, Vector3 position, Vector3 euler, float scale, IReadOnlyList<(int Key, int Value)>? ints = null)
    {
        euler = new Vector3(Wrap(euler.X), Wrap(euler.Y), Wrap(euler.Z));
        bool rotated = euler.LengthSquared() > 1e-6f;
        ushort flags = (ushort)(baseFlags & 0x0F00);
        if (rotated)
        {
            flags |= ZdoData.RotationFlag;
        }
        if (scale > 0f && MathF.Abs(scale - 1f) > 1e-4f)
        {
            flags |= ZdoData.Vec3s;
        }
        if (ints is { Count: > 0 })
        {
            flags |= ZdoData.Ints;
        }
        using MemoryStream ms = new();
        using BinaryWriter w = new(ms);
        w.Write(flags);
        w.Write(position.X); w.Write(position.Y); w.Write(position.Z);
        w.Write(prefab);
        if (rotated)
        {
            WriteSmallRotation(w, euler);
        }
        if ((flags & ZdoData.Vec3s) != 0)
        {
            WriteNumItems(w, 1);
            w.Write(ZdoData.ScaleKey);
            w.Write(scale); w.Write(scale); w.Write(scale);
        }
        if ((flags & ZdoData.Ints) != 0)
        {
            WriteNumItems(w, ints!.Count);
            foreach (var (key, value) in ints)
            {
                w.Write(key);
                w.Write(value);
            }
        }
        w.Flush();
        return ms.ToArray();
    }
}
