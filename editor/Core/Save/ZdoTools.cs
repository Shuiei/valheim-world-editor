using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>One object (ZDO) of the save: where its bytes are, and what the editor needs to know about it.</summary>
public sealed class ObjectRef
{
    /// <summary>The chunk file (or live snapshot file) its bytes are in.</summary>
    public required ChunkFile File { get; init; }

    /// <summary>Where the object starts in that file.</summary>
    public required long Start { get; init; }

    /// <summary>End of the object.</summary>
    public long End { get; set; }

    /// <summary>Where its data sections start (after position, prefab and rotation).</summary>
    public long DataStart { get; set; }

    /// <summary>The ZDO flags as saved (which sections follow, rotation and position form, type bits).</summary>
    public ushort Flags { get; set; }

    /// <summary>The prefab name hash.</summary>
    public int Prefab { get; set; }

    /// <summary>World position in metres.</summary>
    public Vector3 Position { get; set; }

    /// <summary>Player-built piece (has a creator).</summary>
    public bool IsPiece { get; set; }

    /// <summary>Terrain modification object (it carries TCData).</summary>
    public bool IsTerrain { get; set; }

    /// <summary>The player who built it (its "creator"; 0: none).</summary>
    public long Creator { get; set; }

    /// <summary>The object's ZDOID in the running game (live snapshots only).</summary>
    public (long User, uint Id) LiveId { get; set; }

    /// <summary>The 64 m zone the object is in.</summary>
    public (int X, int Z) Zone => ChunkMath.SectorOf(Position.X, Position.Z);
}

/// <summary>
/// Builds new objects by copying an existing object of the same prefab (so all its data, such as health or a creator,
/// stays valid) with a new position, rotation and optionally scale.
/// </summary>
public static partial class ZdoBuilder
{
    /// <summary>A copy of the template object at a new place, in the save format of world versions 41 and 42.</summary>
    /// <param name="source">The chunk file bytes the template lives in.</param>
    /// <param name="template">The object to copy.</param>
    /// <param name="worldVersion">The world version the source was written in.</param>
    /// <param name="position">World position in metres (always written in the full form).</param>
    /// <param name="euler">Unity Euler angles in degrees.</param>
    /// <param name="scale">The new uniform scale; &lt;= 0 keeps the template's scale data.</param>
    /// <param name="fresh">
    /// A new, independent object of the same kind: only the builder (creator) and scale are kept, none of what made the
    /// source unique (chest contents, sign text, health, portal tag...). Otherwise (moving an object) all of its data
    /// is kept.
    /// </param>
    public static byte[] Build(byte[] source, ObjectRef template, int worldVersion, Vector3 position, Vector3 euler, float scale, bool fresh = false)
    {
        var z = ZdoData.Parse(source, (int)template.Start, (int)(template.End - template.Start), worldVersion);
        z.Position = position;
        z.SmallPosition = false;
        z.Rotation = euler;
        if (fresh)
        {
            z.Connection = null;
            z.FloatList.RemoveAll(i => i.Key != ZdoData.ScaleScalarKey);
            z.Vec3List.RemoveAll(i => i.Key != ZdoData.ScaleKey);
            z.QuatList.Clear();
            z.IntList.Clear();
            z.LongList.RemoveAll(i => i.Key != Editing.ObjectField.CreatorKey);
            z.StringList.Clear();
            z.ByteList.Clear();
        }
        if (scale > 0f)
        {
            // The game stores a non-default scale as a Vector3 "scale" (ZNetView.SetLocalScale); scale 1 is no value,
            // as Blank writes it.
            z.FloatList.RemoveAll(i => i.Key == ZdoData.ScaleScalarKey);
            if (MathF.Abs(scale - 1f) > 1e-4f)
            {
                z.SetVec3(ZdoData.ScaleKey, new Vector3(scale));
            }
            else
            {
                z.Vec3List.RemoveAll(i => i.Key == ZdoData.ScaleKey);
            }
        }
        return z.Serialize();
    }

    /// <summary>An angle in degrees brought into 0..360.</summary>
    internal static float Wrap(float a)
    {
        a %= 360f;
        return a < 0f ? a + 360f : a;
    }

    /// <summary>Mirrors ZPackage.WriteSmallRotation / ReadSmallRotation (half degrees, 2 bytes for pure Y).</summary>
    internal static void WriteSmallRotation(BinaryWriter w, Vector3 e)
    {
        uint x = (uint)MathF.Round(e.X * 2f) % 720, y = (uint)MathF.Round(e.Y * 2f) % 720, z = (uint)MathF.Round(e.Z * 2f) % 720;
        if (x == 0 && z == 0)
        {
            w.Write((ushort)(0x8000 | y));
            return;
        }
        uint v = x | (y << 10) | (z << 20);
        w.Write((ushort)(v >> 16));
        w.Write((ushort)(v & 0xFFFF));
    }

    /// <summary>Mirrors ZPackage.WriteNumItems: one byte below 128, else two bytes with the high bit set.</summary>
    internal static void WriteNumItems(BinaryWriter w, int n)
    {
        if (n < 128)
        {
            w.Write((byte)n);
            return;
        }
        w.Write((byte)((n >> 8) | 0x80));
        w.Write((byte)n);
    }
}
