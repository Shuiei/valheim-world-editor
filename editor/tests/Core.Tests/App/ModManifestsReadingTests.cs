using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading one manifest's parts: the root object of a version this editor reads, the version check, and the placement
/// and option fields of a link rule (a field in the wrong form makes the rule not read at all).
/// </summary>
public class ModManifestsReadingTests
{
    private static readonly string[] Expected3 = ["p"];
    private static readonly string[] Expected2 = ["r"];
    private static readonly string[] Expected1 = ["d"];
    private static object? Read(string name, params object?[] args) =>
        PrivateMember.Call(typeof(ModManifests), name, null, args);

    private static object? Item(object? tuple, string field) =>
        tuple == null ? null : tuple.GetType().GetField(field)!.GetValue(tuple);

    [Fact]
    public void ARootOfASupportedVersionIsTheManifestObject()
    {
        Assert.NotNull(Read("ParseManifest", "{\"version\":1,\"data\":{}}"));
    }

    [Theory]
    [InlineData("{\"version\":2}")]
    [InlineData("[1]")]
    [InlineData("{\"data\":{}}")]
    public void ATextThatIsNoManifestOfAnyReadVersionHasNoRoot(string json)
    {
        Assert.Null(Read("ParseManifest", json));
    }

    [Fact]
    public void TextThatIsNotJsonThrows()
    {
        // JsonNode.Parse throws JsonReaderException (a JsonException), as it did at the base commit; callers catch JsonException.
        Assert.ThrowsAny<JsonException>(() => Read("ParseManifest", "{\"version\":1"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(2, false)]
    public void OnlyTheVersionsUpToTheOneReadAreSupported(int version, bool supported)
    {
        Assert.Equal(supported, Read("IsSupportedVersion", JsonValue.Create(version)));
    }

    [Fact]
    public void AVersionGivenAsTextOrMissingIsNotSupported()
    {
        Assert.False((bool)Read("IsSupportedVersion", JsonValue.Create("1"))!);
        Assert.False((bool)Read("IsSupportedVersion", (JsonNode?)null)!);
    }

    [Fact]
    public void APlacementGivesTheIdParentsOffsetAndRotations()
    {
        JsonObject o = JsonNode.Parse("{\"id\":\"a\",\"parents\":[\"p\"],\"offset\":\"o\",\"rotation\":\"1 2 3\"}")!.AsObject();
        object? placement = Read("LinkPlacement", o);
        Assert.Equal("a", Item(placement, "Item1"));
        Assert.Equal(Expected3, Item(placement, "Item2"));
        Assert.Equal("o", Item(placement, "Item3"));
        Assert.Equal("1 2 3", Item(placement, "Item4"));
        Assert.Null(Item(placement, "Item5"));
    }

    [Theory]
    [InlineData("{\"parents\":[\"p\"],\"offset\":\"o\"}")]
    [InlineData("{\"id\":\"a\",\"parents\":[],\"offset\":\"o\"}")]
    [InlineData("{\"id\":\"a\",\"parents\":[\"p\"]}")]
    public void APlacementWithoutAnIdParentOrOffsetIsNoRule(string json)
    {
        Assert.Null(Read("LinkPlacement", JsonNode.Parse(json)!.AsObject()));
    }

    [Fact]
    public void AnOptionOfALinkRuleIsItsReferencesDropsNameAndStorage()
    {
        JsonObject o = JsonNode.Parse("{\"references\":[\"r\"],\"dropOnCopy\":[\"d\"],\"name\":\"n\",\"storedAt\":\"own\"}")!.AsObject();
        object? options = Read("LinkOptions", o);
        Assert.Equal(Expected2, Item(options, "Item1"));
        Assert.Equal(Expected1, Item(options, "Item2"));
        Assert.Equal("n", Item(options, "Item3"));
        Assert.Equal("own", Item(options, "Item4"));
    }

    [Fact]
    public void AnOmittedOptionIsEmptyOrNull()
    {
        object? options = Read("LinkOptions", JsonNode.Parse("{}")!.AsObject());
        Assert.Empty((string[])Item(options, "Item1")!);
        Assert.Empty((string[])Item(options, "Item2")!);
        Assert.Null(Item(options, "Item3"));
        Assert.Null(Item(options, "Item4"));
    }

    [Fact]
    public void AStorageOtherThanRootOrOwnIsNoRule()
    {
        Assert.Null(Read("LinkOptions", JsonNode.Parse("{\"storedAt\":\"elsewhere\"}")!.AsObject()));
    }

    [Fact]
    public void MoreThan32ReferencesIsNoRule()
    {
        string many = "[" + string.Join(",", Enumerable.Repeat("\"x\"", 33)) + "]";
        Assert.Null(Read("LinkOptions", JsonNode.Parse("{\"references\":" + many + "}")!.AsObject()));
    }
}
