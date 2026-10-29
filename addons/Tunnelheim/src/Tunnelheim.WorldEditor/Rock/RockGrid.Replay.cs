namespace Tunnelheim.WorldEditor;

public sealed partial class RockGrid
{
    /// <summary>
    /// Does one edit (VoxelWorld.ApplySaved): carving edits carve, fills fill, planes smooth, hoe strokes reshape the
    /// rock surface; ore vein marks change no rock, and a hoe edit naming a terrain operation the game does not have
    /// is skipped.
    /// </summary>
    /// <param name="op">The edit.</param>
    /// <param name="clip">The only column written (a record's own, when replaying it), or null for all.</param>
    /// <param name="changed">Collects the columns whose rock changed, when given.</param>
    /// <returns>Whether a sample changed.</returns>
    public bool Apply(in DigOp op, (int X, int Z)? clip = null, ISet<(int X, int Z)>? changed = null)
    {
        return op.Kind switch
        {
            _ when op.Carves => Carve(op, clip, changed),
            DigKind.Fill => Fill(op, clip, changed),
            DigKind.Plane => Plane(op, clip, changed),
            DigKind.Hoe => Rules.HoeOps.TryGetValue(op.Code, out var known) && Hoe(op.Position, op.Value, known, clip, changed),
            DigKind.SizedHoe when op.Settings != null => Hoe(op.Position, op.Value, op.Settings, clip, changed),
            _ => false,
        };
    }

    /// <summary>
    /// Lays a column's checkpoint (VoxelWorld.ApplySnapshot): each changed sample of its chunks becomes the seed
    /// ground rounded as stored plus its change, and a chunk's paint, when kept, replaces the chunk's.
    /// </summary>
    /// <param name="cx">The column's index along x.</param>
    /// <param name="cz">The column's index along z.</param>
    /// <param name="checkpoint">The checkpoint.</param>
    public void Lay(int cx, int cz, ColumnCheckpoint checkpoint)
    {
        bool wide = checkpoint.Format == 1;
        foreach (var c in checkpoint.Chunks)
        {
            var chunk = EnsureChunk(cx, c.Y, cz);
            int gy0 = c.Y * Samples;
            for (int i = 0; i < Samples; i++)
            {
                for (int j = 0; j < Samples; j++)
                {
                    float seed = SeedAt(cx * Samples + j, cz * Samples + i);
                    for (int k = 0; k < Samples; k++)
                    {
                        int at = j + Samples * (k + Samples * i);
                        if (c.Delta[at] != 0)
                        {
                            float v = seed - (gy0 + k) * Step;
                            float b = wide ? RockChunk.QuantizeWide(v) : RockChunk.Quantize(v);
                            chunk[at] = b + c.Delta[at] / 256f;
                        }
                    }
                }
            }
            if (c.Paint != null)
            {
                chunk.TakePaint(c.Paint);
            }
        }
    }

    /// <summary>
    /// Replays a column's records as the game builds the column when it loads (VoxelColumnSync.Update): first every
    /// record's seeds, then for each record with edits or a checkpoint, the checkpoint and the edits in order, each
    /// clipped to the column. A record whose checkpoint is missing builds nothing, as in the game.
    /// </summary>
    /// <param name="columns">Each column's records, with the checkpoints of that column by id.</param>
    public void Build(IEnumerable<ColumnRecords> columns)
    {
        var all = columns.ToList();
        foreach (var c in all)
        {
            foreach (var r in c.Records)
            {
                SetSeeds(c.X, c.Z, r.Seeds);
            }
        }
        foreach (var c in all)
        {
            foreach (var r in c.Records)
            {
                Replay(c, r);
            }
        }
    }

    private void Replay(ColumnRecords c, ColumnRecord r)
    {
        ColumnCheckpoint? checkpoint = null;
        if (r.CheckpointId != 0 && !c.Checkpoints.TryGetValue(r.CheckpointId, out checkpoint))
        {
            return;
        }
        if (r.Ops.Count == 0 && checkpoint == null)
        {
            return;
        }
        EnsureColumn(c.X, c.Z);
        if (checkpoint != null)
        {
            Lay(c.X, c.Z, checkpoint);
        }
        foreach (var op in r.Ops)
        {
            Apply(op, (c.X, c.Z));
        }
    }
}

/// <summary>The readable records of one column and the checkpoints there, by id.</summary>
/// <param name="X">The column's index along x.</param>
/// <param name="Z">The column's index along z.</param>
/// <param name="Records">Its records, in the order they are replayed.</param>
/// <param name="Checkpoints">Its checkpoints by <see cref="TunnelheimMod.CheckpointIdKey"/>.</param>
public sealed record ColumnRecords(int X, int Z, IReadOnlyList<ColumnRecord> Records, IReadOnlyDictionary<int, ColumnCheckpoint> Checkpoints);
