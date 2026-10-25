using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Steps fitted to the world as it is now: their ground values put onto the generated ground there now (ground discs
/// under them since), and what they did in zones reset since left out.
/// </summary>
public sealed class HistoryFileFittingTests
{
    /// <summary>One point's values: no edit, the lift given.</summary>
    private static Ground.State State(params float[] lifts) => new(new float[lifts.Length], new float[lifts.Length], new byte[lifts.Length],
        new float[lifts.Length * 4], new byte[lifts.Length], lifts);

    /// <summary>A fitting with the generated ground at <paramref name="now"/> everywhere and these zones reset.</summary>
    private static HistoryFile.Fitting Fit(float now, params ZoneReset[] reset) => new((_, _) => now, reset.ToDictionary(r => (r.X, r.Z)), Carried: true);

    [Fact]
    public void AStepGivesTheSameGroundOnTheGroundAsItIsNow()
    {
        // Kept on ground at 30 m; discs have raised it to 42 m since: the lift makes up for them, before and after.
        var before = State(0f, 5f);
        var after = State(10f, -0.0004f);
        Fit(42f).Rebase([(0, 0), (1, 0)], [30f, 42.001f], before, after);
        Assert.Equal(-12f, before.Lift[0]);
        Assert.Equal(-2f, after.Lift[0]);
        Assert.Equal(5.001f, before.Lift[1], 0.0001f);
        // Under a millimetre a lift is none, as the ground keeps it.
        Assert.Equal(0f, after.Lift[1]);
    }

    [Fact]
    public void WhatAStepDidInAZoneWhoseGroundWasResetIsLeftOut()
    {
        // Zone 0 holds x -32 to 32, zone 1 x 32 to 96: x 32 is on both, so its reset takes that point too.
        List<(int X, int Z)> points = [(0, 0), (32, 0), (40, 0)];
        var (kept, before, after, zones) = Fit(0f, new ZoneReset(1, 0, KeepBuildings: true, Ground: true))
            .LeaveOutReset(points, State(1f, 2f, 3f), State(4f, 5f, 6f), [(0, 0), (1, 0)]);
        Assert.Equal([(0, 0)], kept);
        Assert.Equal([1f], before.Lift);
        Assert.Equal([4f], after.Lift);
        Assert.Equal(4, after.Paint.Length);
        Assert.Equal([(0, 0)], zones);
    }

    [Fact]
    public void AResetThatKeptTheGroundLeavesTheStepAsItIs()
    {
        List<(int X, int Z)> points = [(40, 0)];
        var before = State(1f);
        var (kept, b, _, zones) = Fit(0f, new ZoneReset(1, 0, KeepBuildings: true, Ground: false)).LeaveOutReset(points, before, State(2f), [(1, 0)]);
        Assert.Same(points, kept);
        Assert.Same(before, b);
        Assert.Equal([(1, 0)], zones);
    }
}
