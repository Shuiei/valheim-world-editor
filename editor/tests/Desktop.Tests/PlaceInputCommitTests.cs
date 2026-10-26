using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Committing placements: a stroke's or a shape's placements become one undo step with the label they were given; a
/// stroke that placed nothing says so.
/// </summary>
public class PlaceInputCommitTests
{
    [Fact]
    public void PlacementsBecomeOneUndoStepWithTheirLabel()
    {
        var s = TestScenes.Flat(2);
        int before = s.UndoList.Count;
        var list = new List<PlaceTool.Placement>
        {
            new("Beech1", new Vector3(40, 30, 40), Vector3.Zero, 1, Vector2.Zero),
            new("Beech1", new Vector3(44, 30, 40), Vector3.Zero, 1, Vector2.Zero),
        };
        PlaceInput.Commit(s, list, "Place two");
        Assert.Equal(before + 1, s.UndoList.Count);
        Assert.Equal("Place two", s.UndoList[^1].Label);
    }

    // A brush click where nothing fits (here nothing under the preview, as in a wood already full) makes no step and
    // says so: it said "Placed 0 object(s). Ctrl+Z removes them; …", with nothing for Ctrl+Z to take back.
    [AvaloniaFact]
    public void ABrushClickThatPlacesNothingSaysSo()
    {
        var r = new PlaceInputTests.Rig(PlaceTool.Modes.Brush);
        int steps = r.S.UndoList.Count;
        r.Click(64, 64);
        Assert.Equal("Nothing placed here.", r.Last);
        Assert.Equal(steps, r.S.UndoList.Count);
    }
}
