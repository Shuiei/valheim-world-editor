using System.Numerics;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' made-up situations (TestScenes) that do more than build an area, tested once here so the tests using them
/// can rely on them.
/// </summary>
public sealed class TestScenesTests
{
    [Fact]
    public void AnObjectMovedThenRemovedByTheGameIsMovedHereAndGoneFromTheGamesSave()
    {
        using var w = new TempWorld();
        using var world = WorldSession.Open(w.Dir);
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        var before = WorldSave.Load(w.Dir);

        var thing = TestScenes.MovedThenRemovedByTheGame(s, w.Dir);
        Assert.Equal(thing.Position + new Vector3(3, 0, 3), s.Scene.Things.Single(t => t.Id == thing.Id).Position);
        var game = WorldSave.Load(w.Dir);
        Assert.Equal(before.SaveNumber + 1, game.SaveNumber);
        Assert.Equal(before.ObjectCount - 1, game.ObjectCount);
    }
}
