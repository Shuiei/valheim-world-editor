using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Zones marked for reset and unmarked: a shape taken out or cleared refreshes only around itself, and a reset step loses
/// its zones' ground edits in one step.
/// </summary>
public class EditSessionZoneResetTests
{
    [Fact]
    public void TakingAShapeOutOrClearingItsZoneRefreshesOnlyAroundIt()
    {
        var s = TestScenes.Flat(3);
        void ShapeOnly()
        {
            var (x0, z0, x1, z1) = s.TakeDirty()!.Value;
            Assert.True(x0 >= 27 && z0 >= 27 && x1 <= 37 && z1 <= 37, $"{(x0, z0, x1, z1)}");
        }
        s.Shape(32, 32, _ => 1, 3, 0, "Shape");
        s.TakeDirty();
        Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[^1]).Shown);
        ShapeOnly();
        s.Shape(32, 32, _ => 1, 3, 0, "Shape");
        s.TakeDirty();
        s.MarkResets([(0, 0)], keepBuildings: true, ground: true, "Reset");
        ShapeOnly();
        Assert.Equal(30, s.Ground.HeightOf(32 * s.Ground.W + 32));
    }

    [Fact]
    public void ZonesMarkedForResetLoseTheirGroundEditsInOneStep()
    {
        var s = TestScenes.Flat(2);
        s.Shape(10, 10, _ => 1, 3, 0, "Bump");
        float H() => s.Ground.HeightOf(10 * s.Ground.W + 10);
        Assert.Equal(31, H(), 3);
        s.MarkResets([(0, 0), (1, 0)], keepBuildings: true, ground: true, "Reset");
        Assert.Equal([new Editing.ZoneReset(0, 0, true, true), new Editing.ZoneReset(1, 0, true, true)], s.Resets.OrderBy(r => r.X));
        Assert.Equal(30, H(), 3);
        Assert.True(s.Undo());
        Assert.Empty(s.Resets);
        Assert.Equal(31, H(), 3);
        // Without ground, the ground stays.
        s.MarkResets([(0, 0)], keepBuildings: false, ground: false, "Reset");
        Assert.Equal(31, H(), 3);
        Assert.Equal(new Editing.ZoneReset(0, 0, false, false), Assert.Single(s.Resets));
    }

    [Fact]
    public void OnlyMarkedZonesAreUnmarked()
    {
        var s = TestScenes.Flat(2);
        Assert.Equal(0, s.UnmarkResets([(0, 0)], "Unmark"));
        Assert.Empty(s.UndoList);
        s.MarkResets([(0, 0), (1, 1)], true, false, "Reset");
        Assert.Equal(1, s.UnmarkResets([(0, 0), (0, 0), (1, 0)], "Unmark"));
        Assert.Equal((1, 1), (s.Resets.Single().X, s.Resets.Single().Z));
        Assert.Equal("Unmark", s.UndoLabel);
    }
}
