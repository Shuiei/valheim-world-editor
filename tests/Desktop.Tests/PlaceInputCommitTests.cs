using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Committing placements: a stroke's or a shape's placements become one undo step with the label they were given.
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
}
