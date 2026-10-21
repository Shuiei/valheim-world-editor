using System.Numerics;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The rock grid as Tunnelheim keeps it: columns and chunks found from grid points (negative ones too), a column
/// voxelised with the chunks its ground passes through, seeds from its record unless the same to a millimetre, edits
/// clipped to a column, rock never opened deeper than 64 m nor added higher than the building height, hoe strokes
/// only where rock is, and a record waiting for its checkpoint building nothing.
/// </summary>
public class RockGridTests
{
    private static RockGrid Flat(float h = 40f, RockRules? rules = null) => new((_, _) => h, rules);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(31, 0)]
    [InlineData(32, 1)]
    [InlineData(-1, -1)]
    [InlineData(-32, -1)]
    [InlineData(-33, -2)]
    public void GridPointsFallInTheirColumns(int g, int column)
    {
        Assert.Equal(column, RockGrid.FloorDiv(g, RockGrid.Samples));
        Assert.Equal((column, column), RockGrid.ColumnOf(g, g));
    }

    [Fact]
    public void AColumnGetsTheChunksItsGroundPassesThrough()
    {
        var grid = Flat(40f);
        grid.EnsureColumn(2, -3);
        // 39 m to 41 m: samples 78 to 82, all in chunk 2.
        Assert.Equal([2], grid.ChunksOf(2, -3));
        Assert.True(grid.IsColumn(2, -3));
        var edge = Flat(32.5f);
        edge.EnsureColumn(0, 0);
        Assert.Equal([1, 2], edge.ChunksOf(0, 0));
    }

    [Fact]
    public void ARecordsSeedsReplaceTheGroundUnlessTheSame()
    {
        var grid = Flat(40f);
        grid.EnsureColumn(0, 0);
        grid.Carve(new DigOp(DigKind.Sphere, new Vector3(8f, 40f, 8f), 1.5f));
        float[] near = [.. grid.SeedsOf(0, 0)!.Select(h => h + 0.0005f)];
        grid.SetSeeds(0, 0, near);
        Assert.NotSame(near, grid.SeedsOf(0, 0));
        Assert.True(grid.Chunk(0, 2, 0)!.IsStored);
        float[] other = [.. Enumerable.Repeat(45f, ColumnRecord.SeedCount)];
        grid.SetSeeds(0, 0, other);
        Assert.Same(other, grid.SeedsOf(0, 0));
        Assert.False(grid.Chunk(0, 2, 0)!.IsStored);
        grid.SetSeeds(0, 0, new float[10]);
        Assert.Same(other, grid.SeedsOf(0, 0));
    }

    [Fact]
    public void AnEditWritesOnlyItsClipColumnButMakesTheChunksItReaches()
    {
        var grid = Flat();
        var changed = new HashSet<(int X, int Z)>();
        Assert.True(grid.Carve(new DigOp(DigKind.Sphere, new Vector3(16f, 40f, 8f), 2f), (0, 0), changed));
        Assert.Equal([(0, 0)], changed);
        Assert.True(grid.IsColumn(1, 0));
        Assert.False(grid.Chunk(1, 2, 0)!.IsStored);
        Assert.True(grid.DensityAt(31, 80, 16) < 0f);
    }

    [Fact]
    public void RockIsNeverOpenedDeeperThan64MetresBelowTheGround()
    {
        var grid = Flat();
        // A box from -27 m to -23 m under ground at 40 m: open down to -24 m only.
        grid.Carve(new DigOp(DigKind.Box, new Vector3(8f, -25f, 8f), 2f));
        Assert.Equal(-1f, grid.DensityAt(16, -48, 16));
        Assert.Equal(RockGrid.Band, grid.DensityAt(16, -49, 16));
    }

    [Fact]
    public void AddedRockIsHeldToTheBuildingHeight()
    {
        var grid = Flat(40f, RockRules.Default with { MaxBuildHeight = 2f });
        Assert.True(grid.Fill(new DigOp(DigKind.Fill, new Vector3(8f, 42f, 8f), 1f)));
        Assert.True(grid.DensityAt(16, 84, 16) <= 0f);
        Assert.True(grid.DensityAt(16, 83, 16) > 0f);
        Assert.False(grid.Fill(new DigOp(DigKind.Fill, new Vector3(8f, 50f, 8f), 1f)));
    }

    [Fact]
    public void APlaneSmoothsRockTowardsItself()
    {
        var grid = Flat();
        // A floor 1 m below the ground, the normal straight up (pitch 90 degrees).
        Assert.True(grid.Plane(new DigOp(DigKind.Plane, new Vector3(8f, 39f, 8f), 2f, 90000)));
        Assert.Equal(-0.5f, grid.DensityAt(16, 79, 16));
    }

    [Fact]
    public void AHoeStrokeChangesOnlyRockAndNamesAKnownOperation()
    {
        var level = new HoeSettings(0f, true, 2f, false, false, 0f, 0f, 0f, false, 0f, 0f, true, 2, 2f);
        var grid = Flat(40f, RockRules.Default with { HoeOps = new Dictionary<int, HoeSettings> { [7] = level } });
        var at = new Vector3(8f, 38f, 8f);
        Assert.False(grid.Hoe(at, 0f, level));
        grid.EnsureColumn(0, 0);
        Assert.False(grid.Apply(new DigOp(DigKind.Hoe, at, 0f, 8)));
        Assert.True(grid.Apply(new DigOp(DigKind.Hoe, at, 0f, 7)));
        Assert.True(grid.DensityAt(16, 77, 16) < 0f);
        Assert.Equal(RockPaint.Paved, grid.Chunk(0, 2, 0)!.PaintAt(RockGrid.IndexIn(16, 76, 16)));
        Assert.False(grid.Apply(new DigOp(DigKind.Vein, at, 0f)));
    }

    [Fact]
    public void ARecordWaitingForItsCheckpointBuildsNothing()
    {
        var grid = Flat();
        var dig = new DigOp(DigKind.Sphere, new Vector3(8f, 40f, 8f), 1.5f);
        grid.Build([
            new ColumnRecords(0, 0, [new ColumnRecord(5, null, 42, [dig])], new Dictionary<int, ColumnCheckpoint>()),
            new ColumnRecords(1, 0, [new ColumnRecord(5, null, 0, [])], new Dictionary<int, ColumnCheckpoint>()),
        ]);
        Assert.Empty(grid.Columns);
    }

    [Fact]
    public void ACheckpointIsLaidBeforeItsRecordsEdits()
    {
        short[] delta = new short[RockGrid.ChunkSamples];
        delta[RockGrid.IndexIn(16, 80, 16)] = -512;
        byte[] paint = new byte[RockGrid.ChunkSamples];
        paint[RockGrid.IndexIn(16, 80, 16)] = RockPaint.Dirt;
        var grid = Flat();
        grid.Build([new ColumnRecords(0, 0, [new ColumnRecord(5, null, 9, [])], new Dictionary<int, ColumnCheckpoint> { [9] = new(2, [new CheckpointChunk(2, delta, paint)]) })]);
        Assert.Equal(-2f, grid.DensityAt(16, 80, 16));
        Assert.Equal(RockPaint.Dirt, grid.Chunk(0, 2, 0)!.PaintAt(RockGrid.IndexIn(16, 80, 16)));
    }
}
