using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The things parts of a step: the removals a step makes, the natural objects a mountain buries, and the things the
/// game changed taken into the scene.
/// </summary>
public class EditSessionThingsStepTests
{
    private static WorldScene.Thing Rock() => new(3, StableHash.Of("Rock_4"), new Vector3(0, 30, 0), Vector3.Zero, 1, false);

    [Fact]
    public void ChangeThingsRemovesTheThingsGivenAndSkipsBadOnesAndGoneOnes()
    {
        var s = TestScenes.Flat(1, Rock());
        var (indices, changes, added) = s.ChangeThings([0, 0, 7], []);
        Assert.Empty(indices);
        Assert.Empty(added);
        Assert.Equal(new[] { (0, false, true) }, changes.ToArray());
    }

    [Fact]
    public void ChangeThingsSkipsAThingAlreadyGone()
    {
        var s = TestScenes.Flat(1, Rock() with { Gone = true });
        var (_, changes, _) = s.ChangeThings([0], []);
        Assert.Empty(changes);
    }

    [Fact]
    public void ObjectsToClearFindsNaturalThingsWhereTheMountainRisesHigh()
    {
        var probe = TestScenes.Flat(1);
        float ox = probe.Scene.OriginX, oz = probe.Scene.OriginZ;
        var s = TestScenes.Flat(1, TestScenes.Beech(1, new Vector3(ox + 32, 30, oz + 32)), TestScenes.Beech(2, new Vector3(ox + 50, 30, oz + 32)));
        Assert.Equal(new[] { 0 }, s.ObjectsToClear(32, 32, 3, (_, _) => 1000f, null));
    }

    [Fact]
    public void ObjectsToClearKeepsThingsTheGroundDoesNotRiseOver()
    {
        var probe = TestScenes.Flat(1);
        float ox = probe.Scene.OriginX, oz = probe.Scene.OriginZ;
        var s = TestScenes.Flat(1, TestScenes.Beech(1, new Vector3(ox + 32, 30, oz + 32)));
        Assert.Empty(s.ObjectsToClear(32, 32, 3, (_, _) => 0f, null));
    }

    [Fact]
    public void TakeThingChangesRemovesThingsTheGameRemoved()
    {
        var s = TestScenes.Flat(1, Rock());
        var changed = new List<int>();
        s.TakeThingChanges([3], [], changed);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.Equal(new[] { 0 }, changed);
    }

    [Fact]
    public void TakeThingChangesShowsAChangedThingAsItIsNow()
    {
        var s = TestScenes.Flat(1, Rock());
        var moved = Rock() with { Position = new Vector3(5, 30, 5) };
        var changed = new List<int>();
        s.TakeThingChanges([], new Dictionary<int, WorldScene.Thing> { [3] = moved }, changed);
        Assert.Equal(new Vector3(5, 30, 5), s.Scene.Things[0].Position);
        Assert.Equal(new[] { 0 }, changed);
    }

    [Fact]
    public void TakeThingChangesAddsANewThingInsideTheAreaOnly()
    {
        var probe = TestScenes.Flat(1);
        float ox = probe.Scene.OriginX, oz = probe.Scene.OriginZ;
        var s = TestScenes.Flat(1, Rock());
        var inside = TestScenes.Beech(9, new Vector3(ox + 32, 30, oz + 32));
        var outside = TestScenes.Beech(10, new Vector3(ox + 500, 30, oz + 32));
        var changed = new List<int>();
        s.TakeThingChanges([], new Dictionary<int, WorldScene.Thing> { [9] = inside, [10] = outside }, changed);
        Assert.Equal(2, s.Scene.Things.Count);
        Assert.Equal(9, s.Scene.Things[1].Id);
        Assert.Equal(new[] { 1 }, changed);
    }
}
