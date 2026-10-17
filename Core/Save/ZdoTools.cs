using System.IO.Compression;
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
public static class ZdoBuilder
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

/// <summary>
/// The ZoneSystem part of _main.&lt;n&gt;.db2 (ZoneSystem.Save / Load): which zones the game has already generated, and
/// every location instance with whether it has been placed. Everything else in the file is kept byte for byte.
/// </summary>
public sealed class ZoneDb
{
    /// <summary>The save format version at the head of the file.</summary>
    public int FileVersion { get; private set; }

    /// <summary>The game's network time when it saved, in seconds.</summary>
    public double NetTime { get; private set; }

    /// <summary>The zones the game has generated.</summary>
    public HashSet<(short X, short Z)> Generated { get; } = [];

    /// <summary>ZoneSystem.m_locationVersion of the location list.</summary>
    public int LocationVersion { get; set; }

    /// <summary>The world's global keys (bosses beaten and such).</summary>
    public List<string> GlobalKeys { get; } = [];

    /// <summary>Whether the game has laid out the location instances yet.</summary>
    public bool LocationsGenerated { get; set; }

    /// <summary>Every location instance: its name hash, position and whether it has been placed.</summary>
    public List<(int Hash, Vector3 Position, bool Placed)> Locations { get; } = [];

    /// <summary>
    /// The rest of the file after the ZoneSystem package (the random events' state...), kept byte for byte.
    /// </summary>
    private byte[] _tail = [];

    // The largest zone package read: the biggest world has about 110,000 zones (4 bytes each) and some
    // ten thousand locations (17 bytes each), well under a megabyte.
    private const int MaxPackage = 64 << 20;

    /// <summary>Reads a .db2 file.</summary>
    public static ZoneDb Load(string path)
    {
        using FileStream file = File.OpenRead(path);
        return Load(file);
    }

    /// <summary>A .db2 file's content, from a stream (the fuzz tests read it from bytes).</summary>
    public static ZoneDb Load(Stream stream)
    {
        ZoneDb db = new();
        using BinaryReader r = new(stream);
        db.FileVersion = r.ReadInt32();
        db.NetTime = r.ReadDouble();
        byte[] packed = r.ReadExactBytes(r.ReadInt32());
        // The rest as it comes: a stream that cannot seek does not tell how much is left.
        using (MemoryStream rest = new())
        {
            r.BaseStream.CopyTo(rest);
            db._tail = rest.ToArray();
        }
        db.ReadPackage(CheckedReads.Gunzip(packed, MaxPackage));
        return db;
    }

    /// <summary>
    /// A new world's: no zone generated, no location laid out yet (the game does it on first load), and the rest of the
    /// file (the random events' state...) as given.
    /// </summary>
    public static ZoneDb Empty(int fileVersion, int locationVersion, byte[] tail) =>
        new() { FileVersion = fileVersion, NetTime = 0, LocationVersion = locationVersion, LocationsGenerated = false, _tail = tail };

    /// <summary>The uncompressed ZoneSystem package, as the live bridge sends it.</summary>
    public static ZoneDb FromPackage(byte[] package, double netTime)
    {
        ZoneDb db = new() { FileVersion = 41, NetTime = netTime };
        db.ReadPackage(package);
        return db;
    }

    /// <summary>
    /// Reads the ZoneSystem package: generated zones, location version, global keys, then the locations.
    /// </summary>
    private void ReadPackage(byte[] package)
    {
        ZoneDb db = this;
        using BinaryReader p = new(new MemoryStream(package));
        int n = p.CheckCount(p.ReadInt32(), 4);
        for (int i = 0; i < n; i++)
        {
            db.Generated.Add((p.ReadInt16(), p.ReadInt16()));
        }
        db.LocationVersion = p.ReadInt32();
        int keys = p.CheckCount(p.ReadInt32(), 1);
        for (int i = 0; i < keys; i++)
        {
            db.GlobalKeys.Add(p.ReadCheckedString());
        }
        db.LocationsGenerated = p.ReadBoolean();
        int locs = p.CheckCount(p.ReadInt32(), 17);
        for (int i = 0; i < locs; i++)
        {
            db.Locations.Add((p.ReadInt32(), new Vector3(p.ReadSingle(), p.ReadSingle(), p.ReadSingle()), p.ReadBoolean()));
        }
    }

    /// <summary>Writes the .db2 file: version, net time, the gzip-compressed package, then the kept tail.</summary>
    public void Save(string path)
    {
        using MemoryStream raw = new();
        using (BinaryWriter p = new(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            p.Write(Generated.Count);
            foreach (var (x, z) in Generated)
            {
                p.Write(x);
                p.Write(z);
            }
            p.Write(LocationVersion);
            p.Write(GlobalKeys.Count);
            foreach (string k in GlobalKeys)
            {
                p.Write(k);
            }
            p.Write(LocationsGenerated);
            p.Write(Locations.Count);
            foreach (var (hash, pos, placed) in Locations)
            {
                p.Write(hash);
                p.Write(pos.X); p.Write(pos.Y); p.Write(pos.Z);
                p.Write(placed);
            }
        }
        using MemoryStream packed = new();
        using (GZipStream gz = new(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(raw.ToArray());
        }
        using FileStream fs = new(path, FileMode.Create, FileAccess.Write);
        using BinaryWriter w = new(fs);
        w.Write(FileVersion);
        w.Write(NetTime);
        w.Write((int)packed.Length);
        w.Write(packed.ToArray());
        w.Write(_tail);
        w.Flush();
        fs.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Forget that a zone was generated: the game builds it again (vegetation, and every location in it that is marked
    /// unplaced) the next time a player comes near.
    /// </summary>
    public void ResetZone(int zx, int zz)
    {
        Generated.Remove(((short)zx, (short)zz));
        for (int i = 0; i < Locations.Count; i++)
        {
            var (hash, pos, placed) = Locations[i];
            if (ChunkMath.SectorOf(pos.X, pos.Z) == (zx, zz) && placed)
            {
                Locations[i] = (hash, pos, false);
            }
        }
    }
}
