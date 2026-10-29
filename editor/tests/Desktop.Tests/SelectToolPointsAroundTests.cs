using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The other objects a moved object may snap to: those in the cell it stands in and the eight round it, each once, in
/// order of their index.
/// </summary>
public sealed class SelectToolPointsAroundTests
{
    /// <summary>Objects in the cell and the cells next to it are given, sorted; objects further away are not.</summary>
    [Fact]
    public void ObjectsInTheCellAndTheEightAroundItAreGivenSorted()
    {
        var move = new SelectTool.Move { Items = [] };
        move.OthersAt[(0, 0)] = [5, 2];
        move.OthersAt[(1, -1)] = [9];
        move.OthersAt[(3, 3)] = [7];

        var around = (List<int>)PrivateMethods.Call(typeof(SelectTool), "PointsAround", null, move, new Vector3(0.5f, 0f, 0.5f))!;

        Assert.Equal([2, 5, 9], around);
    }
}
