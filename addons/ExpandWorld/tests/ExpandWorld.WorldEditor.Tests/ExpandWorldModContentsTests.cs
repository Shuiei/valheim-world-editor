using TerrainEditor.Save;
using ValheimGen;
using WorldEditor.Tests;
using Xunit;
using static ExpandWorld.WorldEditor.Tests.ExpandWorldEntries;
using Vector3 = System.Numerics.Vector3;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// What the location file changes about a kind's contents, as New world builds it: the added objects, the swaps and
/// data of its objects (its own lists over the shared ones), its own data, the scale, offset and turn; and the kinds
/// the game must make itself, with why.
/// </summary>
public class ExpandWorldModContentsTests
{
    /// <summary>The data entries the entries name.</summary>
    private static readonly ExpandWorldMod.DataTable Data = new([E(("name", "marked"), ("ints", "mark, 1")), E(("name", "rolled"), ("ints", "mark, 1;3"))]);

    /// <summary>The contents of one kind made of the start temple from these fields.</summary>
    private static LocationContents Contents(params (string Key, object Value)[] fields)
    {
        Fixtures.UseLocations();
        var rules = ExpandWorldMod.LocationsOf([E([("prefab", "StartTemple:Changed"), .. fields])], SeedLocations.Current.Locations, BiomeTable.Vanilla, 10000f, null, [], Data);
        return Assert.Single(rules).Contents!;
    }

    [Fact]
    public void TheAddedObjectsAreReadAsTheModReadsThem()
    {
        var c = Contents(("objects", new List<object?> { "Pickable_Stone, 1, 2, 3", "Bush01, -1, , 4, snap, 90, 0, 0, 2, 3, 4, 0.5, marked" }));
        Assert.Null(c.LeftToGame);
        Assert.Equal(new ContentObject("Pickable_Stone", new Vector3(1, 3, 2), Vector3.Zero, Vector3.One, null, 1f, false), c.Objects[0]);
        // A blank field is dropped as the mod drops it, so the rest moves up one place.
        var bush = c.Objects[1];
        Assert.Equal(("Bush01", new Vector3(-1, 0, 4), new Vector3(0, 90, 0), new Vector3(2, 4, 3), true), (bush.Prefab, bush.Position, bush.Euler, bush.Scale, bush.Snap));
        Assert.Equal(0.5f, bush.Chance);
        Assert.Equal([new ContentField("ints", StableHash.Of("mark"), "1")], bush.Data!);
    }

    [Fact]
    public void SwapsAndDataAreWeightedAndTheLocationsOwnListsWin()
    {
        var c = Contents(
            ("objectSwap", new List<object?> { "Pickable_Stone, Bush01:3, :1", "Pickable_Stone:2, Pickable_Flint" }),
            ("locationObjectSwap", new List<object?> { "Bush01, Pickable_Stone" }),
            ("objectData", new List<object?> { "all, marked" }),
            ("locationObjectData", new List<object?> { "Bush01, marked:1, :3" }));
        Assert.Null(c.LeftToGame);
        // Rows of a source together, each target's weight times its row's, made to sum to 1.
        Assert.Equal([new(0.5f, "Bush01"), new(1 / 6f, ""), new(2 / 6f, "Pickable_Flint")], c.Swaps["Pickable_Stone"]);
        Assert.Equal([new(1f, "Pickable_Stone")], c.Swaps["Bush01"]);
        Assert.Equal([0.25f, 0.75f], c.Data["Bush01"].Select(w => w.Weight));
        Assert.Null(c.Data["Bush01"][1].Value);
        Assert.Single(c.Data["all"]);
    }

    [Fact]
    public void TheScaleOffsetTurnAndOwnDataAreKept()
    {
        var c = Contents(("scaleMin", "0.5"), ("scaleMax", "1, 2, 3"), ("scaleUniform", "false"), ("groundOffset", "2"), ("randomCardinal", "true"), ("data", "marked"));
        Assert.Equal((new Vector3(0.5f), new Vector3(1, 3, 2), false), (c.ScaleMin, c.ScaleMax, c.ScaleUniform));
        Assert.Equal((2f, true), (c.Offset, c.RandomCardinal));
        Assert.Equal([new ContentField("ints", StableHash.Of("mark"), "1")], c.ProxyData!);
        // "offset" wins over "groundOffset"; one scale at both ends is no draw.
        var plain = Contents(("offset", "1"), ("groundOffset", "2"), ("scaleMin", "2"), ("scaleMax", "2"));
        Assert.Equal(1f, plain.Offset);
        Assert.Null(plain.ScaleMin);
    }

    [Theory]
    [InlineData("commands", "say hi")]
    [InlineData("randomSeed", "true")]
    [InlineData("dungeon", "DG_Cave")]
    [InlineData("data", "rolled")]
    [InlineData("objects", "Nothing_Like_It, 1, 1, 1")]
    [InlineData("objectSwap", "Pickable_Stone, Nothing_Like_It")]
    [InlineData("objectSwap", "<group>, Pickable_Stone")]
    [InlineData("objectData", "Pickable_Stone, rolled")]
    public void WhatTheEditorCannotBuildIsLeftToTheGame(string key, string value) =>
        Assert.NotNull(Contents((key, value)).LeftToGame);

    [Fact]
    public void TheGroundIsLevelledAndPaintedAsTheFileSays()
    {
        float exterior = SeedLocations.Current.Locations.First(r => r.Prefab == "StartTemple").ExteriorRadius;
        // A fraction of the exterior radius levelled fully, the rest blended; a named paint over the exterior radius and 5 m.
        var c = Contents(("levelArea", "0.6"), ("paint", "dirt"));
        Assert.Equal(new LevelArea(0.6f * exterior, (1f - 0.6f) * exterior), c.Level);
        Assert.Equal(new PaintArea(exterior, 5f, new Vector3(1f, 0f, 0f), 1f), c.Paint);
        Assert.Null(c.LeftToGame);
        // Its own radius and border; a paint given as numbers (a missing one 0, a 1).
        c = Contents(("levelArea", "true"), ("levelRadius", "4"), ("levelBorder", "6"), ("paint", "0.2, 0.5"), ("paintRadius", "7"), ("paintBorder", "1"));
        Assert.Equal(new LevelArea(4f, 6f), c.Level);
        Assert.Equal(new PaintArea(7f, 1f, new Vector3(0.2f, 0.5f, 0f), 1f), c.Paint);
        // Not a number: half and half.
        Assert.Equal(new LevelArea(0.5f * exterior, 0.5f * exterior), Contents(("levelArea", "yes")).Level);
        // "false", or nothing for a location that is no blueprint: no levelling.
        Assert.Null(Contents(("levelArea", "false"), ("paint", "grass")).Level);
        Assert.Null(Contents(("data", "marked")).Level);
    }

    [Fact]
    public void RandomDamageIsBuiltWithTheFilesWord()
    {
        Fixtures.UseLocations();
        var rule = Assert.Single(ExpandWorldMod.LocationsOf([E(("prefab", "StartTemple:Changed"), ("randomDamage", "all"))], SeedLocations.Current.Locations, BiomeTable.Vanilla, 10000f, null, [], Data));
        Assert.Null(rule.Contents!.LeftToGame);
        Assert.Equal("all", rule.RandomDamage);
    }

    [Fact]
    public void AKindWithoutChangedContentsHasNone()
    {
        Fixtures.UseLocations();
        var rules = ExpandWorldMod.LocationsOf([E(("prefab", "StartTemple"), ("quantity", "1"))], SeedLocations.Current.Locations, BiomeTable.Vanilla, 10000f, null, [], Data);
        Assert.Null(Assert.Single(rules).Contents);
    }

    [Fact]
    public void WeightsAddingToNothingEndOnTheLastChoice()
    {
        var swaps = Contents(("objectSwap", "Pickable_Stone, Bush01:0, Pickable_Branch:0")).Swaps["Pickable_Stone"];
        // No NaN: weights of 0, and the draw (any roll above 0) runs past them all to the last, as the mod's does.
        Assert.All(swaps, s => Assert.Equal(0f, s.Weight));
        Assert.Equal(["Bush01", "Pickable_Branch"], swaps.Select(s => s.Value));
    }
}
