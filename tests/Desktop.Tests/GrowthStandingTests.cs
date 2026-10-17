using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the growing reads from an area: the things within reach of the kept spots, with their names and kinds, and
/// nothing gone or far off.
/// </summary>
public sealed class GrowthStandingTests
{
    /// <summary>The name of the two prefabs these tests use, null for any other.</summary>
    private static string? NameOf(int prefab) => prefab == StableHash.Of("wood_wall_half") ? "wood_wall_half" : null;

    /// <summary>
    /// What stands near a Birch spot at the origin, in a flat area with the one thing given.
    /// </summary>
    /// <param name="thing">The only thing in the area.</param>
    private static List<(float X, float Z, string? Name, ObjectKind Kind)> StandingNearBirch(WorldScene.Thing thing)
    {
        var s = TestScenes.Flat(1, thing);
        var kept = new List<Regrow.Spot> { new("Birch", 0, 0, 0, 0, 0, 0, 1) };
        return Growth.StandingNear(s.Scene, kept, NameOf);
    }

    [Fact]
    public void AThingWithinReachIsReadWithItsNameAndKind()
    {
        var wall = new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(2, 0, 2), Vector3.Zero, 1, true);
        var standing = StandingNearBirch(wall);
        Assert.Equal([(2f, 2f, (string?)"wood_wall_half", ObjectKinds.Of("wood_wall_half", true, false))], standing);
    }

    [Fact]
    public void AThingBeyondReachIsLeftOut()
    {
        var wall = new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(100, 0, 100), Vector3.Zero, 1, true);
        Assert.Empty(StandingNearBirch(wall));
    }

    [Fact]
    public void AGoneThingIsLeftOut()
    {
        var wall = new WorldScene.Thing(1, StableHash.Of("wood_wall_half"), new Vector3(2, 0, 2), Vector3.Zero, 1, true) { Gone = true };
        Assert.Empty(StandingNearBirch(wall));
    }
}
