using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Edge cases of the edit session: the history's step limit and memory budget, calls outside a stroke, saving during a stroke or from an
/// area opened on its own, an Apply live coming back during a stroke, and taking out a change that marked zones for reset.
/// </summary>
[Collection("World files")]
public class EditSessionEdgeTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    [Fact]
    public void TheHistoryKeepsTheStepsChosen()
    {
        var s = TestScenes.Flat(2);
        s.KeepSteps = 200;
        for (int i = 0; i < 205; i++)
        {
            s.Shape(10 + i % 100, 64, Two, 1, 0, $"change {i}");
        }
        Assert.Equal(200, s.UndoList.Count);
        Assert.Equal("change 5", s.UndoList[0].Label);
        // Deleting (another way into the history) keeps the limit too.
        var t = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        t.KeepSteps = 200;
        for (int i = 0; i < 200; i++)
        {
            t.Shape(20, 20, Two, 1, 0, "x");
        }
        t.Delete([0]);
        Assert.Equal(200, t.UndoList.Count);
        Assert.Equal("Deleted 1", t.UndoLabel);
        // Fewer chosen since: the oldest go at the next change.
        t.KeepSteps = 3;
        t.Shape(20, 20, Two, 1, 0, "last");
        Assert.Equal(["x", "Deleted 1", "last"], t.UndoList.Select(c => c.Label));
    }

    [Fact]
    public void WithNoLimitEveryStepIsKept()
    {
        var s = TestScenes.Flat(2);
        s.KeepSteps = 0;
        for (int i = 0; i < 250; i++)
        {
            s.Shape(10 + i % 100, 64, Two, 1, 0, $"change {i}");
        }
        Assert.Equal(250, s.UndoList.Count);
        Assert.Equal("change 0", s.UndoList[0].Label);
    }

    [Fact]
    public void PastTheMemoryBudgetTheOldestGoButTheNewestStays()
    {
        var s = TestScenes.Flat(2);
        s.KeepSteps = 0;
        s.Shape(30, 30, Two, 3, 0, "one");
        long one = s.UndoList[0].Bytes;
        s.KeepBytes = one * 5 / 2;
        s.Shape(60, 60, Two, 3, 0, "two");
        s.Shape(90, 90, Two, 3, 0, "three");
        Assert.Equal(["two", "three"], s.UndoList.Select(c => c.Label));
        // A budget smaller than one step: that step still stays, and undoes.
        s.KeepBytes = 1;
        s.Shape(30, 90, Two, 3, 0, "four");
        Assert.Equal("four", Assert.Single(s.UndoList).Label);
        Assert.True(s.Undo());
    }

    [Fact]
    public void AStepsBytesCountItsPointsThingsZonesAndResets()
    {
        var s = TestScenes.Flat(2);
        s.Shape(30, 30, Two, 3, 0, "one");
        var c = s.UndoList[0];
        Assert.NotEmpty(c.Points);
        Assert.Equal(64L * c.Points.Length + 8L * c.Zones.Count, c.Bytes);
        var more = c with { Things = [(0, true, false), (1, false, true)], Resets = [(new ZoneReset(0, 0, false, false), false, true)] };
        Assert.Equal(c.Bytes + 16 + 16, more.Bytes);
    }

    [Fact]
    public void OutsideAStrokeStepsAndEndsDoNothing()
    {
        var s = TestScenes.Flat(2);
        Assert.False(s.Stroking);
        Assert.False(s.StrokeStep(64, 64, 0.1f));
        Assert.Equal("", s.EndStroke().Shown);
        // During a stroke: undo, removing a change and saving wait for it to end.
        s.Shape(64, 64, Two, 3, 0, "raise");
        s.BeginStroke(BrushTool.Raise, 64, 64);
        Assert.True(s.Stroking);
        Assert.False(s.Undo());
        Assert.Equal("", s.RemoveChange(s.UndoList[0]).Shown);
        Assert.Throws<InvalidOperationException>(s.Save);
        s.EndStroke();
        Assert.True(s.Undo());
    }

    [Fact]
    public void AnAreaOpenedFromAFolderSavesThroughItsWorld()
    {
        // An area outside any open world (only tests make one) cannot be saved.
        Assert.Contains("not part of an open world", Assert.Throws<InvalidOperationException>(() => TestScenes.Flat(1).Save()).Message);
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            Assert.NotNull(scene.Owner);
            var s = scene.Session!;
            s.Shape(32, 32, Two, 3, 0, "raise");
            bool reset = false;
            s.ThingsReset += () => reset = true;
            var r = s.Save();
            Assert.True(r.Saved, r.Message);
            // Not read again (no zone reset, no ground discs): the objects and the history stay.
            Assert.False(reset);
            Assert.Equal(0, s.Pending.Zones);
            Assert.Single(s.UndoList);
            Assert.Equal(3, WorldSave.Load(dir).SaveNumber);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, true);
        }
    }

    [Fact]
    public async Task AppliedZoneResetsReadTheAreaAgain()
    {
        using var game = new FakeGame();
        var (world, s) = await TestScenes.LiveArea(game);
        s.Shape(32, 32, Two, 3, 0, "raise");
        world.Edits.SetReset(new ZoneReset(5, 5, true, false), true);
        bool reset = false;
        s.ThingsReset += () => reset = true;
        var o = await s.ApplyLive();
        Assert.True(o.Reloaded, o.Message);
        Assert.True(reset);
        Assert.Empty(s.UndoList);
    }

    /// <summary>
    /// A live area with a raise at (32, 32), its Apply live held by the game until the stroke given has begun at
    /// (40, 40) and taken a step; then the game answers.
    /// </summary>
    /// <returns>The session, the stroke's point and its height before the stroke, and the Apply's outcome.</returns>
    private static async Task<(EditSession S, int P, float Before, WorldSession.Outcome O)> ApplyDuringAStroke(FakeGame game, bool reset)
    {
        var (world, s) = await TestScenes.LiveArea(game);
        s.Shape(32, 32, Two, 3, 0, "raise");
        if (reset)
        {
            world.Edits.SetReset(new ZoneReset(5, 5, true, false), true);
        }
        int p = 40 * s.Ground.W + 40;
        float before = s.Ground.HeightOf(p);
        game.HoldTerrain = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var applying = s.ApplyLive();
        if (await Task.WhenAny(game.TerrainArrived.Task, applying) == applying)
        {
            Assert.Fail("The Apply ended before the ground reached the game: " + (await applying).Message);
        }
        // Small: far from the game's limit, and clear of the raise.
        s.Brush.Radius = 3;
        s.Brush.Strength = 0.5f;
        s.BeginStroke(BrushTool.Raise, 40, 40);
        Assert.True(s.StrokeStep(40, 40, 0.1f));
        Assert.NotEqual(before, s.Ground.HeightOf(p));
        game.HoldTerrain.SetResult();
        var o = await applying;
        Assert.True(o.Done, o.Message);
        return (s, p, before, o);
    }

    // Live: an Apply that comes back during a stroke leaves the stroke's ground alone; the stroke is one whole undo step.
    [Fact]
    public async Task AnApplyLiveEndingDuringAStrokeKeepsIt()
    {
        using var game = new FakeGame();
        var (s, p, before, _) = await ApplyDuringAStroke(game, false);
        float raised = s.Ground.HeightOf(p);
        Assert.NotEqual(before, raised);
        Assert.True(s.StrokeStep(40, 40, 0.1f));
        Assert.Equal("", s.EndStroke().Shown);
        Assert.True(s.Ground.HeightOf(p) > raised);
        Assert.Equal(2, s.UndoList.Count);
        Assert.True(s.Undo());
        Assert.Equal(before, s.Ground.HeightOf(p), 3);
    }

    // Live: an Apply that reads the area again (zone resets) during a stroke drops the stroke, and says so when it ends.
    [Fact]
    public async Task AnApplyLiveReadingTheAreaAgainDropsAStroke()
    {
        using var game = new FakeGame();
        var (s, p, before, o) = await ApplyDuringAStroke(game, true);
        Assert.True(o.Reloaded, o.Message);
        Assert.False(s.Stroking);
        Assert.Equal(before, s.Ground.HeightOf(p), 3);
        Assert.False(s.StrokeStep(40, 40, 0.1f));
        Assert.Contains("stroke was dropped", s.EndStroke().Shown);
        Assert.Empty(s.UndoList);
        Assert.Equal("", s.EndStroke().Shown);
    }

    [Fact]
    public void TakingOutAChangeUnmarksItsZoneResets()
    {
        var s = TestScenes.Flat(2);
        var reset = new ZoneReset(0, 0, true, false);
        s.Commit("Area: Reset", null, [], [], [(reset, false, true)]);
        Assert.Equal(1, s.Pending.Resets);
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[0]).Shown);
        Assert.Equal(0, s.Pending.Resets);
        s.Undo();
        Assert.Equal(1, s.Pending.Resets);
    }
}
