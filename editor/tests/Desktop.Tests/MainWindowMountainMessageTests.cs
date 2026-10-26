using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The messages of the mountain tool: the one for a mountain that does not fit in the open area, and the one for a
/// mountain put in. Each kind has its own message.
/// </summary>
public class MainWindowMountainMessageTests
{
    [Theory]
    [InlineData(MountainKind.Peak, "mountain.msg.tooBig.peak")]
    [InlineData(MountainKind.Ridge, "mountain.msg.tooBig.ridge")]
    [InlineData(MountainKind.Range, "mountain.msg.tooBig.range")]
    [InlineData(MountainKind.Mesa, "mountain.msg.tooBig.mesa")]
    [InlineData(MountainKind.Volcano, "mountain.msg.tooBig.volcano")]
    [InlineData(MountainKind.Hills, "mountain.msg.tooBig.hills")]
    public void EachKindSaysItDoesNotFitWithItsReach(MountainKind kind, string key)
    {
        Assert.Equal(Said.Of(key, 120f), PrivateMember.Call<Said>(typeof(MainWindow), "MountainTooBig", null, kind, 120f));
    }

    [Theory]
    [InlineData(MountainKind.Peak, "mountain.msg.placed.peak")]
    [InlineData(MountainKind.Ridge, "mountain.msg.placed.ridge")]
    [InlineData(MountainKind.Range, "mountain.msg.placed.range")]
    [InlineData(MountainKind.Mesa, "mountain.msg.placed.mesa")]
    [InlineData(MountainKind.Volcano, "mountain.msg.placed.volcano")]
    [InlineData(MountainKind.Hills, "mountain.msg.placed.hills")]
    public void EachKindSaysItWasPlacedWithItsHeightAndTheBuriedClause(MountainKind kind, string key)
    {
        var buried = Said.Of("mountain.msg.buried", 3);

        Assert.Equal(Said.Of(key, 25f, buried), PrivateMember.Call<Said>(typeof(MainWindow), "MountainPlaced", null, kind, 25f, buried));
    }
}
