using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.DungeonCaveSupport;

namespace WorldEditor.Tests;

/// <summary>
/// What the rooms of the Frost Cave hold once rolled: the random parts of each room, rolled from its place as the game
/// rolls them, and the game's random numbers left as they were.
/// </summary>
public class DungeonCaveContentsTests
{
    [Fact]
    public void ARoomsContentsAreRolledTheGamesWay()
    {
        // What the game made in that Frost Cave, as its save holds it (all 235 found there, at these
        // places): the rooms' networked objects its random parts kept, rolled from each room's place.
        var expected = new Dictionary<string, int>
        {
            ["CastleKit_metal_groundtorch_unlit"] = 6,
            ["Ice_floor"] = 5,
            ["MountainKit_brazier"] = 17,
            ["MountainKit_wood_gate"] = 2,
            ["Pickable_ForestCryptRemains01"] = 4,
            ["Pickable_ForestCryptRemains02"] = 5,
            ["Pickable_ForestCryptRemains03"] = 2,
            ["Pickable_MeatPile"] = 24,
            ["Pickable_MountainCaveCrystal"] = 4,
            ["Pickable_MountainCaveRandom"] = 12,
            ["Spawner_Cultist"] = 5,
            ["Spawner_Ulv"] = 16,
            ["caverock_ice_pillar_wall"] = 2,
            ["caverock_ice_stalagmite"] = 16,
            ["caverock_ice_stalagtite"] = 60,
            ["cloth_hanging_door"] = 4,
            ["cloth_hanging_long"] = 6,
            ["fenrirhide_hanging"] = 6,
            ["hanging_hairstrands"] = 21,
            ["lox_ribs"] = 6,
            ["mountainkit_chair"] = 11,
            ["mountainkit_table"] = 1,
        };
        var kind = Dungeons.KindOf(StableHash.Of("DG_Cave"))!;
        var made = Cave().SelectMany(r => Dungeons.Contents(r, kind, CaveAt, 509094289)).ToList();
        Assert.Equal(235, made.Count);
        Assert.Equal(expected, made.GroupBy(m => m.Prefab).ToDictionary(g => g.Key, g => g.Count()));
        var first = Dungeons.Contents(Cave()[0], kind, CaveAt, 509094289)[0];
        Assert.Equal("caverock_ice_stalagtite", first.Prefab);
        Assert.Equal(new Vector3(3.01f, 5165.703f, -2642.79f), first.Position, new Vector3Comparer(1e-3f));
        // The roll leaves the game's random numbers as they were (the world generator shares them).
        ValheimGen.UnityEngine.Random.InitState(42);
        float a = ValheimGen.UnityEngine.Random.Value;
        ValheimGen.UnityEngine.Random.InitState(42);
        Dungeons.Contents(Cave()[1], kind, CaveAt, 1);
        Assert.Equal(a, ValheimGen.UnityEngine.Random.Value);
        // A room placed elsewhere rolls differently.
        var moved = Cave()[5] with { Position = Cave()[5].Position + new Vector3(24, 0, 0) };
        Assert.NotEqual(Dungeons.Contents(Cave()[5], kind, CaveAt, 1).Select(m => m.Prefab), Dungeons.Contents(moved, kind, CaveAt, 1).Select(m => m.Prefab));
    }
}
