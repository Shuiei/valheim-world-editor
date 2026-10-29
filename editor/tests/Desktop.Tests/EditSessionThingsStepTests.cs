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
    private static readonly int[] Expected4 = [0];
    private static readonly int[] Expected3 = [0];
    private static readonly int[] Expected2 = [0];
    private static readonly int[] Expected1 = [1];
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
        var s = TestScenes.Flat(1, TestScenes.BeechInFlat(1, 32, 32), TestScenes.BeechInFlat(2, 50, 32));
        Assert.Equal(Expected4, s.ObjectsToClear(32, 32, 3, (_, _) => 1000f, null));
    }

    [Fact]
    public void ObjectsToClearKeepsThingsTheGroundDoesNotRiseOver()
    {
        var s = TestScenes.Flat(1, TestScenes.BeechInFlat(1, 32, 32));
        Assert.Empty(s.ObjectsToClear(32, 32, 3, (_, _) => 0f, null));
    }

    [Fact]
    public void TakeThingChangesRemovesThingsTheGameRemoved()
    {
        var s = TestScenes.Flat(1, Rock());
        var changed = new List<int>();
        s.TakeThingChanges([3], [], changed);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.Equal(Expected3, changed);
    }

    [Fact]
    public void TakeThingChangesShowsAChangedThingAsItIsNow()
    {
        var s = TestScenes.Flat(1, Rock());
        var moved = Rock() with { Position = new Vector3(5, 30, 5) };
        var changed = new List<int>();
        s.TakeThingChanges([], new Dictionary<int, WorldScene.Thing> { [3] = moved }, changed);
        Assert.Equal(new Vector3(5, 30, 5), s.Scene.Things[0].Position);
        Assert.Equal(Expected2, changed);
    }

    [Fact]
    public void TakeThingChangesAddsANewThingInsideTheAreaOnly()
    {
        var s = TestScenes.Flat(1, Rock());
        var inside = TestScenes.BeechInFlat(9, 32, 32);
        var outside = TestScenes.BeechInFlat(10, 500, 32);
        var changed = new List<int>();
        s.TakeThingChanges([], new Dictionary<int, WorldScene.Thing> { [9] = inside, [10] = outside }, changed);
        Assert.Equal(2, s.Scene.Things.Count);
        Assert.Equal(9, s.Scene.Things[1].Id);
        Assert.Equal(Expected1, changed);
    }
}
