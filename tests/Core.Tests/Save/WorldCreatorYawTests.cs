using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using Rnd = ValheimGen.UnityEngine.Random;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// The turn a spawned location gets (WorldCreator.Yaw): along the slope under it when its kind asks, else a random
/// multiple of 22.5° from the zone's seed when its kind asks, else none.
/// </summary>
public class WorldCreatorYawTests
{
    /// <summary>The turn, with the shared Unity random put back as it was (the creator's caller does the same).</summary>
    private static float Yaw(SeedLocations.Rule rule, TerrainService terrain, Vector3 at, int zoneSeed)
    {
        var was = Rnd.State;
        try
        {
            return PrivateMember.Call<float>(typeof(WorldCreator), "Yaw", null, rule, terrain, at, zoneSeed);
        }
        finally
        {
            Rnd.State = was;
        }
    }

    [Fact]
    public void TheTurnFollowsTheKindsRule()
    {
        using var w = new TempWorld();
        var terrain = new TerrainService(w.Load());
        var at = new Vector3(10f, 0f, 20f);

        Assert.Equal(0f, Yaw(new SeedLocations.Rule(), terrain, at, 5));

        // Random: a multiple of 22.5° below 360, the same for the same zone seed, and taken from the seed.
        float random = Yaw(new SeedLocations.Rule { RandomRotation = 1 }, terrain, at, 5);
        Assert.InRange(random, 0f, 337.5f);
        Assert.Equal(0f, random % 22.5f);
        Assert.Equal(random, Yaw(new SeedLocations.Rule { RandomRotation = 1 }, terrain, at, 5));
        Assert.Contains(Enumerable.Range(0, 64), seed => Yaw(new SeedLocations.Rule { RandomRotation = 1 }, terrain, at, seed) != random);

        // Along the slope: an angle, finite, that changes with the ground; the slope wins over the random turn.
        float slope = Yaw(new SeedLocations.Rule { SlopeRotation = 1, RandomRotation = 1 }, terrain, at, 5);
        Assert.True(float.IsFinite(slope));
        Assert.InRange(slope, -180f, 180f);
        Assert.Contains(Enumerable.Range(0, 32), i => Yaw(new SeedLocations.Rule { SlopeRotation = 1 }, terrain, new Vector3(at.X + (i * 7f), 0f, at.Z - (i * 5f)), 5) != slope);
    }
}
