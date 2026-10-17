using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The lowest snap point of a piece, which is the bottom an object is set down by: a name with no snap points has no
/// bottom at all.
/// </summary>
public class PlaceToolBottomOfTests
{
    [Fact]
    public void APieceWithNoSnapPointsHasNoBottom()
    {
        Assert.Null((float?)typeof(PlaceTool).GetMethod("BottomOf", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, ["no_such_piece"]));
    }
}
