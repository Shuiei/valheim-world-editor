using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The boss's key and the hidden caches of a level (DungeonPlanner.Build.Caches): the key chest is made only when the
/// settings ask for a boss key, and the hidden caches only when they ask for secrets.
/// </summary>
public class DungeonPlannerKeyAndCacheTests
{
    [Fact]
    public void PlaceBossKeyPutsTheKeyChestInARoomWhenTheSettingsAskForIt()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1, BossKey: true));
        var entrance = p.PlaceEntrance(l0);
        p.Call("PlaceArena", l0, entrance);
        p.Call("PlaceBossKey");
        Assert.Equal(1, p.Plan.Spaces.Count(s => s.KeyChest));
    }

    [Fact]
    public void PlaceBossKeyDoesNothingWhenTheSettingsDoNotAskForIt()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1, BossKey: false));
        var entrance = p.PlaceEntrance(l0);
        p.Call("PlaceArena", l0, entrance);
        p.Call("PlaceBossKey");
        Assert.DoesNotContain(p.Plan.Spaces, s => s.KeyChest);
    }

    [Fact]
    public void PlaceSecretCachesAddsHiddenRoomsWhenTheSettingsAskForThem()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1, Secrets: true));
        p.PlaceEntrance(l0);
        p.Call("PlaceWings", l0, 3);
        p.Call("PlaceSecretCaches");
        Assert.Contains(p.Plan.Spaces, s => s.Room == DungeonKit.Room.Secret);
    }

    [Fact]
    public void PlaceSecretCachesAddsNothingWhenTheSettingsDoNotAskForThem()
    {
        var (p, l0) = DungeonPlannerProbe.Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: 1, Secrets: false));
        p.PlaceEntrance(l0);
        p.Call("PlaceWings", l0, 3);
        p.Call("PlaceSecretCaches");
        Assert.DoesNotContain(p.Plan.Spaces, s => s.Room == DungeonKit.Room.Secret);
    }
}
