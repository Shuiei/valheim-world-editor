using TerrainEditor.App;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// The steps that place the vegetation of a rule: how many groups it tries, the groups, a spot that passes every check
/// is added with its rotation, and a spot that fails one is not. The placement reads a real world's terrain (one world
/// at a time, as the other Regrow tests).
/// </summary>
[Collection("World files")]
public class RegrowSpotsTests
{
    private static readonly Type DrawType = PrivateMember.Nested(typeof(Regrow), "SpotDraw");

    private static ValheimGen.TerrainService Terrain(TempWorld w) => new(w.Load(), null);

    private static object Draw(float rx = 0f, float ry = 0f, float rz = 0f, float scale = 1f) =>
        PrivateMember.New(DrawType, rx, ry, rz, scale);

    /// <summary>One Regrow call over the flat test ground of a world's terrain, no rules and no clear areas.</summary>
    private static object NewPass(ValheimGen.TerrainService terrain) =>
        PrivateMember.New(PrivateMember.Nested(typeof(Regrow), "Pass"), terrain, RegrowFixtures.FlatGround(), new List<ValheimGen.VegetationRule>(), null);

    [Fact]
    public void NoGroupsAreTriedWhenTheChanceOfAnyMisses()
    {
        // The draw is seeded: an unseeded generator draws exactly 0, which a chance of 0 does not miss.
        ValheimGen.UnityEngine.Random.InitState(1);
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "min", 0f);
        PrivateMember.Set(rule, "max", 0f);
        object?[] args = [rule, 0];
        Assert.False((bool)PrivateMember.Call(typeof(Regrow), "TryRollGroupCount", null, args)!);
        Assert.Equal(1, args[1]);
    }

    [Fact]
    public void TheCountIsDrawnBetweenTheRulesMinimumAndMaximum()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "min", 2f);
        PrivateMember.Set(rule, "max", 2f);
        object?[] args = [rule, 0];
        Assert.True((bool)PrivateMember.Call(typeof(Regrow), "TryRollGroupCount", null, args)!);
        Assert.Equal(2, args[1]);
    }

    [Fact]
    public void SpotKeepsItsDrawnTiltAndTurnWithoutAGroundTilt()
    {
        object rule = RegrowFixtures.OpenRule();
        Vector3 rotation = PrivateMember.Call<Vector3>(typeof(Regrow), "SpotRotation", null, rule, Vector3.UnitY, Draw(1f, 2f, 3f));
        Assert.Equal(new Vector3(1f, 2f, 3f), rotation);
    }

    [Fact]
    public void SpotTurnedToTheGroundIsNotTheDrawnOne()
    {
        // With the chance of a ground tilt at 1 the turn comes from the slope's normal, not from the draw.
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "chanceToUseGroundTilt", 1f);
        Vector3 rotation = PrivateMember.Call<Vector3>(typeof(Regrow), "SpotRotation", null, rule, Vector3.UnitY, Draw(1f, 2f, 3f));
        Assert.NotEqual(new Vector3(1f, 2f, 3f), rotation);
    }

    [Fact]
    public void ASpotThatPassesEveryCheckIsAddedWithItsDraw()
    {
        using var w = new TempWorld();
        var terrain = Terrain(w);
        object rule = RegrowFixtures.OpenRule("Pine");
        var spots = new List<Regrow.Spot>();
        bool placed = (bool)PrivateMember.Call(typeof(Regrow), "TryPlaceSpot", null, NewPass(terrain), rule, 0f, 0f, Draw(scale: 2f), spots)!;
        Assert.True(placed);
        Regrow.Spot spot = Assert.Single(spots);
        Assert.Equal("Pine", spot.Name);
        Assert.Equal(0f, spot.X);
        Assert.Equal(2f, spot.Scale);
    }

    [Fact]
    public void ASpotOutsideTheGroundIsNotAdded()
    {
        using var w = new TempWorld();
        var spots = new List<Regrow.Spot>();
        bool placed = (bool)PrivateMember.Call(typeof(Regrow), "TryPlaceSpot", null, NewPass(Terrain(w)), RegrowFixtures.OpenRule(), 100f, 0f, Draw(), spots)!;
        Assert.False(placed);
        Assert.Empty(spots);
    }

    [Fact]
    public void ASpotInAnotherBiomeThanTheRuleGrowsInIsNotAdded()
    {
        using var w = new TempWorld();
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "biome", 0);
        var spots = new List<Regrow.Spot>();
        bool placed = (bool)PrivateMember.Call(typeof(Regrow), "TryPlaceSpot", null, NewPass(Terrain(w)), rule, 0f, 0f, Draw(), spots)!;
        Assert.False(placed);
        Assert.Empty(spots);
    }

    [Fact]
    public void AGroupOfNoObjectsPlacesNothing()
    {
        using var w = new TempWorld();
        var spots = new List<Regrow.Spot>();
        bool placed = (bool)PrivateMember.Call(typeof(Regrow), "PlaceGroup", null, NewPass(Terrain(w)), RegrowFixtures.OpenRule(), 0f, 0f, 0, spots)!;
        Assert.False(placed);
        Assert.Empty(spots);
    }

    [Fact]
    public void AGroupOfOnePassingObjectIsPlaced()
    {
        using var w = new TempWorld();
        var spots = new List<Regrow.Spot>();
        bool placed = (bool)PrivateMember.Call(typeof(Regrow), "PlaceGroup", null, NewPass(Terrain(w)), RegrowFixtures.OpenRule(), 0f, 0f, 1, spots)!;
        Assert.True(placed);
        Assert.Single(spots);
    }

    [Fact]
    public void ARuleWithNoCountPlacesNothing()
    {
        using var w = new TempWorld();
        var spots = new List<Regrow.Spot>();
        var terrain = Terrain(w);
        PrivateMember.Call(typeof(Regrow), "PlaceRule", null, NewPass(terrain), RegrowFixtures.OpenRule(), 0f, 0f, 0, spots);
        Assert.Empty(spots);
    }

    [Fact]
    public void ARuleWithOneGroupPlacesOneGroupAndStops()
    {
        using var w = new TempWorld();
        var spots = new List<Regrow.Spot>();
        var terrain = Terrain(w);
        PrivateMember.Call(typeof(Regrow), "PlaceRule", null, NewPass(terrain), RegrowFixtures.OpenRule(), 0f, 0f, 1, spots);
        Assert.Single(spots);
    }
}
