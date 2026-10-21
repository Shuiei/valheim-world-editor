using TerrainEditor.Tunnelheim;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>A column's checkpoint as Tunnelheim keeps it: written in its own format and read back; damaged ones refused.</summary>
public class ColumnCheckpointTests
{
    [Fact]
    public void ACheckpointReadsBackAsWritten()
    {
        short[] delta = new short[RockGrid.ChunkSamples];
        delta[5] = -300;
        delta[RockGrid.ChunkSamples - 1] = 12;
        byte[] paint = new byte[RockGrid.ChunkSamples];
        paint[7] = RockPaint.Paved;
        var back = ColumnCheckpoint.Parse(new ColumnCheckpoint(1, [new CheckpointChunk(2, delta, paint), new CheckpointChunk(3, new short[RockGrid.ChunkSamples], null)]).Encode());
        // The oldest format's changes are from its own seed ground: written back in it.
        Assert.Equal(1, back.Format);
        Assert.Equal(ColumnCheckpoint.Current, ColumnCheckpoint.Parse(new ColumnCheckpoint(ColumnCheckpoint.Current, back.Chunks).Encode()).Format);
        Assert.Equal(2, back.Chunks.Count);
        Assert.Equal((2, delta, paint), (back.Chunks[0].Y, back.Chunks[0].Delta, back.Chunks[0].Paint));
        Assert.Null(back.Chunks[1].Paint);
    }

    [Fact]
    public void ADamagedCheckpointIsRefused()
    {
        byte[] good = new ColumnCheckpoint(2, [new CheckpointChunk(0, new short[RockGrid.ChunkSamples], null)]).Encode();
        byte[] raw = ColumnRecordGunzip(good);
        raw[0] = 9;
        Assert.Throws<InvalidDataException>(() => ColumnCheckpoint.Parse(Gzip(raw)));
        byte[] shortChunk = ColumnRecordGunzip(good)[..20];
        Assert.Throws<InvalidDataException>(() => ColumnCheckpoint.Parse(Gzip(shortChunk)));
    }

    private static byte[] ColumnRecordGunzip(byte[] b)
    {
        using var o = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(new MemoryStream(b), System.IO.Compression.CompressionMode.Decompress))
        {
            gz.CopyTo(o);
        }
        return o.ToArray();
    }

    private static byte[] Gzip(byte[] b)
    {
        using var o = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(o, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write(b);
        }
        return o.ToArray();
    }
}
