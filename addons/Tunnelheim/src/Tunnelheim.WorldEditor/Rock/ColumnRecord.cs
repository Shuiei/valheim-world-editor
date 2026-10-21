using System.IO.Compression;
using System.Numerics;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// One column's record, the data of a <see cref="TunnelheimMod.RecordPrefab"/> object: the ground heights the column's
/// rock started from (its seeds, frozen when the column was first dug), the checkpoint its edits start from, and the
/// edits, replayed in order. Stored gzipped in the object's <see cref="TunnelheimMod.RecordKey"/> bytes, laid out as the
/// game's packages (little-endian).
/// </summary>
/// <param name="Version">The layout it was read in (1 to 6); written as 6 when it holds a <see cref="DigKind.SizedHoe"/> edit, else 5.</param>
/// <param name="Seeds">The ground height at each of the column's 32 by 32 points (0.5 m apart, x fastest), or null when not kept.</param>
/// <param name="CheckpointId">The id of the checkpoint the edits start from (<see cref="TunnelheimMod.CheckpointIdKey"/>), 0 for none.</param>
/// <param name="Ops">The edits, in the order they were made.</param>
public sealed record ColumnRecord(int Version, float[]? Seeds, int CheckpointId, IReadOnlyList<DigOp> Ops)
{
    /// <summary>The newest layout Tunnelheim 1.0.0 reads; a newer one it keeps untouched and never builds.</summary>
    public const int Newest = 6;

    /// <summary>The layout written when no edit needs <see cref="Newest"/>.</summary>
    public const int Plain = 5;

    /// <summary>How many ground heights a column keeps: 32 by 32.</summary>
    public const int SeedCount = RockGrid.Samples * RockGrid.Samples;

    /// <summary>
    /// Reads a record from the object's bytes. Throws <see cref="InvalidDataException"/> when the bytes are no record
    /// Tunnelheim 1.0.0 reads (the mod then never builds the column): a newer layout, too many heights, a cut end.
    /// </summary>
    /// <param name="bytes">The gzipped record.</param>
    public static ColumnRecord Parse(byte[] bytes)
    {
        try
        {
            using var r = new PackageReader(new MemoryStream(Gunzip(bytes)));
            return Read(r);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("Tunnelheim record cut short", e);
        }
    }

    private static ColumnRecord Read(PackageReader r)
    {
        int version = r.ReadInt();
        var ops = new List<DigOp>();
        if (version == 1)
        {
            int count = r.ReadCount(16);
            for (int i = 0; i < count; i++)
            {
                var centre = ReadVector(r);
                ops.Add(new DigOp(DigKind.Sphere, centre, r.ReadSingle()));
            }
            return new ColumnRecord(version, null, 0, ops);
        }
        if (version is < 2 or > Newest)
        {
            throw new InvalidDataException($"Tunnelheim record layout {version}, newer than {Newest}");
        }
        int seedCount = r.ReadInt();
        if (seedCount is < 0 or > SeedCount)
        {
            throw new InvalidDataException($"Tunnelheim record with {seedCount} ground heights");
        }
        float[]? seeds = null;
        if (seedCount > 0)
        {
            seeds = new float[seedCount];
            for (int i = 0; i < seedCount; i++)
            {
                seeds[i] = r.ReadSingle();
            }
        }
        int checkpoint = version >= 5 ? r.ReadInt() : 0;
        int opCount = r.ReadCount(20);
        for (int i = 0; i < opCount; i++)
        {
            ops.Add(ReadOp(r, version >= 3, version >= 4));
        }
        return new ColumnRecord(version, seeds, checkpoint, ops);
    }

    private static DigOp ReadOp(PackageReader r, bool extra, bool author)
    {
        var kind = (DigKind)(byte)r.ReadInt();
        var position = ReadVector(r);
        float value = r.ReadSingle();
        int code = r.ReadInt();
        float more = extra ? r.ReadSingle() : 0;
        long by = author ? r.ReadLong() : 0;
        HoeSettings? settings = null;
        if (kind == DigKind.SizedHoe)
        {
            settings = new HoeSettings(
                LevelOffset: r.ReadSingle(),
                Level: r.ReadBool(),
                LevelRadius: r.ReadSingle(),
                Square: r.ReadBool(),
                Raise: r.ReadBool(),
                RaiseRadius: r.ReadSingle(),
                RaisePower: r.ReadSingle(),
                RaiseDelta: r.ReadSingle(),
                Smooth: r.ReadBool(),
                SmoothRadius: r.ReadSingle(),
                SmoothPower: r.ReadSingle(),
                PaintCleared: r.ReadBool(),
                PaintType: r.ReadInt(),
                PaintRadius: r.ReadSingle());
        }
        return new DigOp(kind, position, value, code, more, by, settings);
    }

    private static Vector3 ReadVector(PackageReader r)
    {
        float x = r.ReadSingle();
        float y = r.ReadSingle();
        return new Vector3(x, y, r.ReadSingle());
    }

    /// <summary>
    /// The record's bytes as Tunnelheim writes them: in the newest layout its edits need (<see cref="Plain"/> or
    /// <see cref="Newest"/>), gzipped.
    /// </summary>
    public byte[] Encode()
    {
        using var raw = new MemoryStream();
        using (var w = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Ops.Any(o => o.Kind == DigKind.SizedHoe) ? Newest : Plain);
            w.Write(Seeds?.Length ?? 0);
            foreach (float s in Seeds ?? [])
            {
                w.Write(s);
            }
            w.Write(CheckpointId);
            w.Write(Ops.Count);
            foreach (var op in Ops)
            {
                WriteOp(w, op);
            }
        }
        return Gzip(raw.ToArray());
    }

    private static void WriteOp(BinaryWriter w, in DigOp op)
    {
        w.Write((int)op.Kind);
        w.Write(op.Position.X);
        w.Write(op.Position.Y);
        w.Write(op.Position.Z);
        w.Write(op.Value);
        w.Write(op.Code);
        w.Write(op.Extra);
        w.Write(op.Author);
        if (op.Kind != DigKind.SizedHoe)
        {
            return;
        }
        var s = op.Settings ?? throw new InvalidOperationException("A sized hoe edit needs its settings");
        w.Write(s.LevelOffset);
        w.Write(s.Level);
        w.Write(s.LevelRadius);
        w.Write(s.Square);
        w.Write(s.Raise);
        w.Write(s.RaiseRadius);
        w.Write(s.RaisePower);
        w.Write(s.RaiseDelta);
        w.Write(s.Smooth);
        w.Write(s.SmoothRadius);
        w.Write(s.SmoothPower);
        w.Write(s.PaintCleared);
        w.Write(s.PaintType);
        w.Write(s.PaintRadius);
    }

    /// <summary>Gzips bytes as the game's Utils.Compress does (fastest level).</summary>
    /// <param name="bytes">The bytes to pack.</param>
    internal static byte[] Gzip(byte[] bytes)
    {
        using var packed = new MemoryStream();
        using (var gz = new GZipStream(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(bytes, 0, bytes.Length);
        }
        return packed.ToArray();
    }

    /// <summary>Unpacks gzipped bytes; throws <see cref="InvalidDataException"/> when they are not gzip.</summary>
    /// <param name="bytes">The packed bytes.</param>
    internal static byte[] Gunzip(byte[] bytes)
    {
        using var inflated = new MemoryStream();
        using (var gz = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress))
        {
            gz.CopyTo(inflated);
        }
        return inflated.ToArray();
    }
}
