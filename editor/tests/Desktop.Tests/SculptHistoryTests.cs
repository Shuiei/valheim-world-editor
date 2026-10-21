using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.StrokeRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Undo and redo of strokes, the zones a stroke over an edge changes, what saving writes into the world (ground discs
/// past the limit included). Loads the test world: the world generator is the game's, with static state, so tests that
/// read a world never run at the same time (one collection).
/// </summary>
[Collection("World files")]
public class SculptHistoryTests
{
    [Fact]
    public void UndoAndRedoPutTheGroundBackAndForth()
    {
        var s = TestScenes.Flat();
        Stroke(s, BrushTool.Raise, 64, 64);
        float raised = At(s, 64, 64);
        Assert.True(s.CanUndo);
        Assert.Equal("Raise", s.UndoLabel);
        Assert.Equal((1, 0, 0, 0), (s.Pending.Zones > 0 ? 1 : 0, s.Pending.Deleted, s.Pending.Added, s.Pending.Resets));
        Assert.True(s.Undo());
        Assert.Equal(30, At(s, 64, 64), 4);
        // Back as loaded: nothing waits to be saved.
        Assert.Equal(0, s.Pending.Zones);
        Assert.True(s.CanRedo);
        Assert.True(s.Redo());
        Assert.Equal(raised, At(s, 64, 64), 4);
        Assert.True(s.Pending.Zones > 0);
        // A new stroke after an undo drops the redo.
        s.Undo();
        Stroke(s, BrushTool.Lower, 20, 20);
        Assert.False(s.CanRedo);
    }

    [Fact]
    public void AStrokeOverAZoneEdgeChangesBothZones()
    {
        var s = TestScenes.Flat(2);
        Stroke(s, BrushTool.Raise, 64, 30);
        Assert.Equal(2, s.Pending.Zones);
        var left = s.Edits.Get(0, 0)!;
        var right = s.Edits.Get(1, 0)!;
        // The shared edge point: the last column of the left zone, the first of the right one.
        Assert.True(left.Modified[30 * 65 + 64]);
        Assert.True(right.Modified[30 * 65]);
        Assert.Equal(left.Level[30 * 65 + 64], right.Level[30 * 65], 5);
    }

    [Fact]
    public void ZonesOfEdgePointsAreBothSides()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        Assert.Equal(new[] { (0, 0) }, g.ZonesOf([10 * 129 + 10]));
        Assert.Equal(new[] { (0, 0), (1, 0) }, g.ZonesOf([10 * 129 + 64]));
        Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }, g.ZonesOf([64 * 129 + 64]));
    }

    [Fact]
    public void UntouchedZonesKeepTheirSavedValues()
    {
        var s = TestScenes.Flat(1);
        var e = s.Ground.ZoneEdit(0, 0);
        Assert.True(e.IsEmpty);
    }

    [Fact]
    public void SavingWritesTheGroundIntoTheWorld()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            int g = 32 * scene.W + 32;
            float before = scene.Heights[g];
            Stroke(s, BrushTool.Raise, 32, 32, 60);
            float after = scene.Heights[g];
            Assert.True(after > before + 1);
            var res = s.Save();
            Assert.True(res.Saved, res.Message);
            Assert.Equal(0, s.Pending.Zones);
            // Like Apply live, the history stays, its steps saved.
            Assert.True(s.CanUndo);
            Assert.Equal(0, s.UnappliedSteps);
            var again = WorldScene.Load(dir, 0, 0, 1);
            Assert.Equal(after, again.Heights[g], 3);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Fact]
    public void NoLimitRaisesPastTheLimitAndSavingKeepsTheGroundWithGroundDiscs()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            // Zones 1 to 3 (the test world keeps zones 0 to 15).
            var scene = WorldScene.Load(dir, 2, 2, 3);
            var s = scene.Session!;
            s.Brush.Radius = 20;
            s.Brush.Strength = 1;
            int g = 96 * scene.W + 96;
            float before = scene.Heights[g];
            Stroke(s, BrushTool.Raise, 96, 96, 300);
            Assert.InRange(scene.Heights[g] - before, 7.9f, 8.1f);
            s.Ground.NoLimit = true;
            Stroke(s, BrushTool.Raise, 96, 96, 300);
            float after = scene.Heights[g];
            Assert.True(after > before + 30, $"{after - before:0.0} m");
            Assert.True(s.Ground.Lift[g] > 20);
            var res = s.Save();
            Assert.True(res.Saved, res.Message);
            Assert.Contains("ground disc", res.Message);
            Assert.Equal(0, s.Pending.Zones);
            // The area shows the saved ground at once (the ground discs under it, no lift left).
            Assert.Equal(after, scene.Heights[g], 0.5f);
            Assert.Equal(0, s.Ground.Lift[g]);
            var again = WorldScene.Load(dir, 2, 2, 3);
            Assert.Equal(after, again.Heights[g], 0.5f);
            Assert.NotEmpty(again.World.Discs);
            // Not things of the area: they are just the ground.
            Assert.DoesNotContain(again.Things, t => t.Prefab == WorldSave.LocationProxyPrefab);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
