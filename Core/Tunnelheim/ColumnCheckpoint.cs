using TerrainEditor.Save;

namespace TerrainEditor.Tunnelheim;

/// <summary>
/// One column's checkpoint, the data of a <see cref="TunnelheimMod.CheckpointPrefab"/> object: the rock of edits baked
/// out of the column's record (the mod bakes the older ones once a record grows long), kept as the change of each
/// sample from the seed ground, chunk by chunk. A record names its checkpoint by its id (<see
/// cref="TunnelheimMod.CheckpointIdKey"/>), and the mod builds the column only once both are there.
/// </summary>
/// <param name="Format">The layout it was read in: 2, or 1 for the oldest, whose seed ground was not limited to the density's band.</param>
/// <param name="Chunks">The changed chunks of the column.</param>
public sealed record ColumnCheckpoint(int Format, IReadOnlyList<CheckpointChunk> Chunks)
{
    /// <summary>The layout Tunnelheim 1.0.0 writes.</summary>
    public const int Current = 2;

    /// <summary>
    /// Reads a checkpoint from the object's bytes. Throws <see cref="InvalidDataException"/> when they are no
    /// checkpoint Tunnelheim 1.0.0 reads.
    /// </summary>
    /// <param name="bytes">The gzipped checkpoint.</param>
    public static ColumnCheckpoint Parse(byte[] bytes)
    {
        try
        {
            using var r = new ValheimReader(new MemoryStream(ColumnRecord.Gunzip(bytes)));
            int format = r.ReadInt();
            if (format is not (1 or Current))
            {
                throw new InvalidDataException($"Tunnelheim checkpoint format {format}");
            }
            int count = r.ReadCount(4 + 4 + RockGrid.ChunkSamples * 2 + 1);
            var chunks = new List<CheckpointChunk>(count);
            for (int i = 0; i < count; i++)
            {
                int y = r.ReadInt();
                byte[] raw = r.ReadByteArray();
                if (raw.Length != RockGrid.ChunkSamples * 2)
                {
                    throw new InvalidDataException($"Tunnelheim checkpoint chunk of {raw.Length} bytes");
                }
                short[] delta = new short[RockGrid.ChunkSamples];
                for (int k = 0; k < delta.Length; k++)
                {
                    delta[k] = (short)(raw[2 * k] | raw[2 * k + 1] << 8);
                }
                byte[]? paint = null;
                if (r.ReadBool())
                {
                    paint = r.ReadByteArray();
                    if (paint.Length != RockGrid.ChunkSamples)
                    {
                        throw new InvalidDataException($"Tunnelheim checkpoint paint of {paint.Length} bytes");
                    }
                }
                chunks.Add(new CheckpointChunk(y, delta, paint));
            }
            return new ColumnCheckpoint(format, chunks);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("Tunnelheim checkpoint cut short", e);
        }
    }

    /// <summary>
    /// The checkpoint's bytes as Tunnelheim writes them, gzipped, in its own <see cref="Format"/>: its changes are from
    /// that format's seed ground, so a checkpoint read in the oldest format is written back in it (Tunnelheim reads both).
    /// </summary>
    public byte[] Encode()
    {
        using var raw = new MemoryStream();
        using (var w = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Format);
            w.Write(Chunks.Count);
            foreach (var c in Chunks)
            {
                w.Write(c.Y);
                w.Write(c.Delta.Length * 2);
                foreach (short d in c.Delta)
                {
                    w.Write(d);
                }
                w.Write(c.Paint != null);
                if (c.Paint != null)
                {
                    w.Write(c.Paint.Length);
                    w.Write(c.Paint);
                }
            }
        }
        return ColumnRecord.Gzip(raw.ToArray());
    }
}

/// <summary>One chunk of a checkpoint.</summary>
/// <param name="Y">The chunk's height index in its column (16 m each, 0 from sea bottom's 0 m up).</param>
/// <param name="Delta">Each sample's change from the seed ground, in 1/256 m (x fastest, then y, then z).</param>
/// <param name="Paint">Each sample's paint, or null when the chunk keeps none.</param>
public sealed record CheckpointChunk(int Y, short[] Delta, byte[]? Paint);
