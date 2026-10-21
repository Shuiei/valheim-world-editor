using System.IO.Compression;
using System.Numerics;
using TerrainEditor.Tunnelheim;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A column's record as Tunnelheim keeps it: written in the layout the mod writes (5, or 6 with a sized hoe edit) and
/// read back the same; the older layouts read as the mod reads them; one the mod cannot read refused.
/// </summary>
public class ColumnRecordTests
{
    private static readonly HoeSettings Sized = new(0.5f, true, 2.5f, false, true, 3f, 0.2f, -1f, true, 3.5f, 2f, true, 2, 2.75f);

    private static byte[] Gz(Action<BinaryWriter> write)
    {
        using var raw = new MemoryStream();
        using (var w = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            write(w);
        }
        using var packed = new MemoryStream();
        using (var gz = new GZipStream(packed, CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write(raw.ToArray());
        }
        return packed.ToArray();
    }

    [Fact]
    public void ARecordReadsBackAsWritten()
    {
        float[] seeds = [.. Enumerable.Range(0, ColumnRecord.SeedCount).Select(i => 30f + i * 0.01f)];
        DigOp[] ops =
        [
            new(DigKind.Chip, new Vector3(1.5f, 33f, -2.25f), 1.25f, -45000, 135f, 76561198000000000),
            new(DigKind.Fill, new Vector3(4f, 32f, 4f), 1f),
            new(DigKind.Hoe, new Vector3(2f, 31f, 2f), 30f, 954701736),
        ];
        var back = ColumnRecord.Parse(new ColumnRecord(ColumnRecord.Plain, seeds, 77, ops).Encode());
        Assert.Equal(ColumnRecord.Plain, back.Version);
        Assert.Equal(seeds, back.Seeds);
        Assert.Equal(77, back.CheckpointId);
        Assert.Equal(ops, back.Ops);
    }

    [Fact]
    public void ASizedHoeEditIsWrittenInTheNewestLayoutWithItsSettings()
    {
        var op = new DigOp(DigKind.SizedHoe, new Vector3(1f, 2f, 3f), 15f, 126758991, Settings: Sized);
        var back = ColumnRecord.Parse(new ColumnRecord(5, null, 0, [op]).Encode());
        Assert.Equal(ColumnRecord.Newest, back.Version);
        Assert.Null(back.Seeds);
        Assert.Equal(Sized, Assert.Single(back.Ops).Settings);
    }

    [Fact]
    public void TheOldestLayoutHoldsSpheresOnly()
    {
        var back = ColumnRecord.Parse(Gz(w =>
        {
            w.Write(1);
            w.Write(1);
            w.Write(10f);
            w.Write(30f);
            w.Write(20f);
            w.Write(1.5f);
        }));
        Assert.Equal(new DigOp(DigKind.Sphere, new Vector3(10f, 30f, 20f), 1.5f), Assert.Single(back.Ops));
        Assert.Equal(0, back.CheckpointId);
    }

    [Theory]
    [InlineData(2, false, false)]
    [InlineData(3, true, false)]
    [InlineData(4, true, true)]
    public void OlderLayoutsLeaveOutWhatTheyHadNot(int version, bool extra, bool author)
    {
        var back = ColumnRecord.Parse(Gz(w =>
        {
            w.Write(version);
            w.Write(0);
            w.Write(1);
            w.Write((int)DigKind.Box);
            w.Write(1f);
            w.Write(2f);
            w.Write(3f);
            w.Write(1.5f);
            w.Write(0);
            if (extra)
            {
                w.Write(45f);
            }
            if (author)
            {
                w.Write(99L);
            }
        }));
        Assert.Equal(new DigOp(DigKind.Box, new Vector3(1f, 2f, 3f), 1.5f, 0, extra ? 45f : 0f, author ? 99 : 0), Assert.Single(back.Ops));
    }

    [Fact]
    public void ARecordTheModCannotReadIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => ColumnRecord.Parse(Gz(w => w.Write(7))));
        Assert.Throws<InvalidDataException>(() => ColumnRecord.Parse(Gz(w =>
        {
            w.Write(5);
            w.Write(1025);
        })));
        Assert.Throws<InvalidDataException>(() => ColumnRecord.Parse(Gz(w =>
        {
            w.Write(5);
            w.Write(1024);
        })));
        Assert.Throws<InvalidDataException>(() => ColumnRecord.Parse([1, 2, 3]));
    }
}
