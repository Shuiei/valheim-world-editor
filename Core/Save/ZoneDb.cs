using System.IO.Compression;
using System.Numerics;

namespace TerrainEditor.Save;

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
