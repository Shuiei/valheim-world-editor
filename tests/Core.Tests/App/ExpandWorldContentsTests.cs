using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using static WorldEditor.Tests.ExpandWorldEntries;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// What the location file changes about a kind's contents, as New world builds it: the added objects, the swaps and
/// data of its objects (its own lists over the shared ones), its own data, the scale, offset and turn; and the kinds
/// the game must make itself, with why.
/// </summary>
public class ExpandWorldContentsTests
{
    /// <summary>The data entries the entries name.</summary>
    private static readonly ExpandWorld.DataTable Data = new([E(("name", "marked"), ("ints", "mark, 1")), E(("name", "rolled"), ("ints", "mark, 1;3"))]);

    /// <summary>The contents of one kind made of the start temple from these fields.</summary>
    private static LocationContents Contents(params (string Key, object Value)[] fields)
    {
        Fixtures.UseLocations();
        var rules = ExpandWorld.LocationsOf([E([("prefab", "StartTemple:Changed"), .. fields])], SeedLocations.Current.Locations, BiomeTable.Vanilla, 10000f, null, [], Data);
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
    [InlineData("levelArea", "true")]
    [InlineData("paint", "grass")]
    [InlineData("randomDamage", "true")]
    [InlineData("data", "rolled")]
    [InlineData("objects", "Nothing_Like_It, 1, 1, 1")]
    [InlineData("objectSwap", "Pickable_Stone, Nothing_Like_It")]
    [InlineData("objectSwap", "<group>, Pickable_Stone")]
    [InlineData("objectData", "Pickable_Stone, rolled")]
    public void WhatTheEditorCannotBuildIsLeftToTheGame(string key, string value) =>
        Assert.NotNull(Contents((key, value)).LeftToGame);

    [Fact]
    public void AKindWithoutChangedContentsHasNone()
    {
        Fixtures.UseLocations();
        var rules = ExpandWorld.LocationsOf([E(("prefab", "StartTemple"), ("quantity", "1"))], SeedLocations.Current.Locations, BiomeTable.Vanilla, 10000f, null, [], Data);
        Assert.Null(Assert.Single(rules).Contents);
    }
}
