namespace Tunnelheim.WorldEditor;

public sealed partial class RockGrid
{
    /// <summary>
    /// The seed ground's height at a grid point: its column's seeds when it has some, else the ground the game shows
    /// there (VoxelWorld.SeedAt).
    /// </summary>
    /// <param name="gx">The point's index along x.</param>
    /// <param name="gz">The point's index along z.</param>
    public float SeedAt(int gx, int gz)
    {
        var key = ColumnOf(gx, gz);
        return _seeds.TryGetValue(key, out float[]? seeds)
            ? seeds[gx - key.X * Samples + Samples * (gz - key.Z * Samples)]
            : Ground(gx * Step, gz * Step);
    }

    /// <summary>
    /// The density at a sample: its chunk's when there is one, else the seed ground's, not rounded (VoxelWorld.GetDensity).
    /// </summary>
    /// <param name="gx">The sample's grid x.</param>
    /// <param name="gy">Its grid y.</param>
    /// <param name="gz">Its grid z.</param>
    public float DensityAt(int gx, int gy, int gz)
    {
        var chunk = Chunk(FloorDiv(gx, Samples), FloorDiv(gy, Samples), FloorDiv(gz, Samples));
        return chunk != null ? chunk[IndexIn(gx, gy, gz)] : SeedAt(gx, gz) - gy * Step;
    }

    /// <summary>
    /// The seeds of a column taken from the ground the game shows (VoxelWorld.CaptureSeeds): the height at each of its
    /// points, x fastest.
    /// </summary>
    /// <param name="cx">The column's index along x.</param>
    /// <param name="cz">The column's index along z.</param>
    public float[] GroundSeeds(int cx, int cz)
    {
        float[] seeds = new float[Samples * Samples];
        for (int i = 0; i < Samples; i++)
        {
            for (int j = 0; j < Samples; j++)
            {
                seeds[j + Samples * i] = Ground((cx * Samples + j) * Step, (cz * Samples + i) * Step);
            }
        }
        return seeds;
    }

    /// <summary>
    /// Gives a column the seeds its record keeps (VoxelWorld.SetColumnSeeds): unless they are the ones it has (to a
    /// millimetre), they replace them and a voxelised column starts over from them.
    /// </summary>
    /// <param name="cx">The column's index along x.</param>
    /// <param name="cz">The column's index along z.</param>
    /// <param name="seeds">The record's seeds; any other count than <see cref="ColumnRecord.SeedCount"/> is ignored.</param>
    public void SetSeeds(int cx, int cz, float[]? seeds)
    {
        if (seeds is not { Length: Samples * Samples } || (_seeds.TryGetValue((cx, cz), out float[]? had) && SameGround(had, seeds)))
        {
            return;
        }
        _seeds[(cx, cz)] = seeds;
        foreach (int y in ChunksOf(cx, cz))
        {
            _chunks[(cx, y, cz)].Reset(seeds);
        }
    }

    private static bool SameGround(float[] a, float[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 0.001f)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Voxelises a column (VoxelWorld.EnsureColumn): it takes its seeds from the ground unless it has some, and gets
    /// the chunks its ground passes through, a metre above and below.
    /// </summary>
    /// <param name="cx">The column's index along x.</param>
    /// <param name="cz">The column's index along z.</param>
    public void EnsureColumn(int cx, int cz)
    {
        if (_columns.ContainsKey((cx, cz)))
        {
            return;
        }
        if (!_seeds.ContainsKey((cx, cz)))
        {
            _seeds[(cx, cz)] = GroundSeeds(cx, cz);
        }
        _columns[(cx, cz)] = [];
        GroundRange(cx, cz, out float lo, out float hi);
        int y0 = FloorDiv(RockMath.FloorStep((float)(lo - 1.0)), Samples);
        int y1 = FloorDiv(RockMath.CeilStep((float)(hi + 1.0)), Samples);
        for (int y = y0; y <= y1; y++)
        {
            AddChunk(cx, y, cz);
        }
    }

    /// <summary>Makes sure a chunk is there, voxelising its column first (VoxelWorld.EnsureChunk).</summary>
    /// <param name="cx">The chunk's column index along x.</param>
    /// <param name="cy">Its height index.</param>
    /// <param name="cz">Its column index along z.</param>
    public RockChunk EnsureChunk(int cx, int cy, int cz)
    {
        if (_chunks.TryGetValue((cx, cy, cz), out var chunk))
        {
            return chunk;
        }
        EnsureColumn(cx, cz);
        return _chunks.TryGetValue((cx, cy, cz), out chunk) ? chunk : AddChunk(cx, cy, cz);
    }

    /// <summary>Makes sure every chunk of a box of grid points is there (and the one-sample margin around it).</summary>
    /// <param name="x0">The box's lowest grid x.</param>
    /// <param name="y0">Its lowest grid y.</param>
    /// <param name="z0">Its lowest grid z.</param>
    /// <param name="x1">Its highest grid x.</param>
    /// <param name="y1">Its highest grid y.</param>
    /// <param name="z1">Its highest grid z.</param>
    public void EnsureBox(int x0, int y0, int z0, int x1, int y1, int z1)
    {
        int cx0 = FloorDiv(x0 - 1, Samples), cy0 = FloorDiv(y0 - 1, Samples), cz0 = FloorDiv(z0 - 1, Samples);
        int cx1 = FloorDiv(x1 + 1, Samples), cy1 = FloorDiv(y1 + 1, Samples), cz1 = FloorDiv(z1 + 1, Samples);
        for (int z = cz0; z <= cz1; z++)
        {
            for (int y = cy0; y <= cy1; y++)
            {
                for (int x = cx0; x <= cx1; x++)
                {
                    EnsureChunk(x, y, z);
                }
            }
        }
    }

    private RockChunk AddChunk(int cx, int cy, int cz)
    {
        var chunk = new RockChunk(_seeds[(cx, cz)], cy);
        _chunks[(cx, cy, cz)] = chunk;
        var ys = _columns[(cx, cz)];
        int at = ys.BinarySearch(cy);
        ys.Insert(at < 0 ? ~at : at, cy);
        return chunk;
    }

    /// <summary>The lowest and highest seed ground over a column and its one-sample margin (VoxelWorld.GroundRange).</summary>
    private void GroundRange(int cx, int cz, out float lo, out float hi)
    {
        lo = float.MaxValue;
        hi = float.MinValue;
        for (int i = -1; i <= Samples + 1; i++)
        {
            for (int j = -1; j <= Samples + 1; j++)
            {
                float h = SeedAt(cx * Samples + j, cz * Samples + i);
                lo = RockMath.Min(lo, h);
                hi = RockMath.Max(hi, h);
            }
        }
    }
}
