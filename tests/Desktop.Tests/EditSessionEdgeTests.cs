using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Edge cases of the edit session and the brushes: the history's length limit, calls outside a stroke, saving during a
/// stroke or from an area opened on its own, an Apply live coming back during a stroke, taking out a change that marked zones for reset; every falloff's profile,
/// the ragged and turned brushes and their outlines, and erosion by rain.
/// </summary>
[Collection("World files")]
public class EditSessionEdgeTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    [Fact]
    public void TheHistoryKeepsTheLast200Changes()
    {
        var s = TestScenes.Flat(2);
        for (int i = 0; i < EditSession.HistoryLength + 5; i++)
        {
            s.Shape(10 + i % 100, 64, Two, 1, 0, $"change {i}");
        }
        Assert.Equal(EditSession.HistoryLength, s.UndoList.Count);
        Assert.Equal("change 5", s.UndoList[0].Label);
        // Deleting (another way into the history) keeps the limit too.
        var t = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        for (int i = 0; i < EditSession.HistoryLength; i++)
        {
            t.Shape(20, 20, Two, 1, 0, "x");
        }
        t.Delete([0]);
        Assert.Equal(EditSession.HistoryLength, t.UndoList.Count);
        Assert.Equal("Deleted 1", t.UndoLabel);
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

    [Theory]
    [InlineData(Falloff.Smooth)]
    [InlineData(Falloff.Linear)]
    [InlineData(Falloff.Dome)]
    [InlineData(Falloff.Flat)]
    [InlineData(Falloff.Peak)]
    [InlineData(Falloff.Sharp)]
    public void EveryFalloffIsFullInTheMiddleAndNothingAtTheEdge(Falloff f)
    {
        Assert.Equal(1, Brush.Fade(f, 1), 4);
        Assert.Equal(0, Brush.Fade(f, 0), 4);
        // Never weaker nearer the middle.
        float last = 0;
        for (int i = 0; i <= 20; i++)
        {
            float v = Brush.Fade(f, i / 20f);
            Assert.True(v >= last - 1e-5f, $"{f} drops at {i / 20f}");
            last = v;
        }
    }

    [Fact]
    public void TheFalloffsDifferWhereTheyShould()
    {
        Assert.Equal(0.5f, Brush.Fade(Falloff.Linear, 0.5f), 4);
        Assert.True(Brush.Fade(Falloff.Dome, 0.5f) > 0.8f);
        Assert.Equal(1, Brush.Fade(Falloff.Flat, 0.3f), 4);
        Assert.Equal(0.125f, Brush.Fade(Falloff.Peak, 0.5f), 4);
        Assert.Equal(1, Brush.Fade(Falloff.Sharp, 0.01f), 4);
    }

    [Fact]
    public void TheRaggedBrushWavesAroundTheCircle()
    {
        var b = new Brush { Radius = 10, Shape = BrushShape.Noise };
        Assert.Equal(10 * 1.5f, b.Reach, 3);
        // Along the edge, some points are in and some out: the edge is ragged.
        var w = Enumerable.Range(0, 64).Select(i => b.Weight(9.5f * MathF.Cos(i * MathF.Tau / 64), 9.5f * MathF.Sin(i * MathF.Tau / 64), 300 + 9.5f * MathF.Cos(i * MathF.Tau / 64), 200 + 9.5f * MathF.Sin(i * MathF.Tau / 64))).ToList();
        Assert.Contains(w, v => v > 0.01f);
        Assert.Contains(w, v => v == 0);
    }

    [Fact]
    public void ATurnedSquareReachesItsCornersAndItsOutlineTurns()
    {
        var b = new Brush { Radius = 10, Shape = BrushShape.Square, Turn = 45 };
        Assert.Equal(14.2f, b.Reach, 1);
        // Turned 45°: the corner now points along x, past the radius.
        Assert.True(b.Weight(13, 0) > 0);
        Assert.Equal(0, b.Weight(9, 9));
        var outline = b.Outline(8);
        Assert.Equal(8, outline.Count);
        Assert.Contains(outline, p => MathF.Abs(MathF.Abs(p.X) - 14.14f) < 0.1f && MathF.Abs(p.Z) < 0.1f);
    }

    [Fact]
    public void RainErodesTheGround()
    {
        var s = TestScenes.Flat(2);
        s.Shape(64, 64, Formula.Compile("8 * (1 - d / r)", ["x", "z", "d", "r", "h"]), 12, 0, "cone");
        float[] before = (float[])s.Scene.Heights.Clone();
        s.Brush.ErodeWater = true;
        s.Brush.Radius = 12;
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Erode, 64, 64);
        for (int i = 0; i < 20; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        s.EndStroke();
        Assert.Equal("Erode", s.UndoLabel);
        Assert.Contains(Enumerable.Range(0, before.Length), p => MathF.Abs(before[p] - s.Scene.Heights[p]) > 0.01f);
        // Far from the brush, untouched.
        Assert.Equal(before[10 * s.Scene.W + 10], s.Scene.Heights[10 * s.Scene.W + 10]);
    }

    [Fact]
    public void ABrushEntirelyOutsideTheAreaDoesNothing()
    {
        var s = TestScenes.Flat(2);
        s.Brush.Radius = 3;
        s.BeginStroke(BrushTool.Raise, -50, -50);
        Assert.False(s.StrokeStep(-50, -50, 0.1f));
        Assert.Equal("", s.EndStroke().Shown);
        Assert.False(s.CanUndo);
    }
}
