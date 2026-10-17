using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mod catalogue's reading of a JSON value as a number, and of a prefab entry's fields (ModCatalogue.Num and IsPrefab).
/// </summary>
public class ModCatalogueEntryRulesTests
{
    [Fact]
    public void AFractionIsANumberButNotAnIntegerNumber()
    {
        var fraction = JsonNode.Parse("1.5");

        Assert.True(PrivateMember.Call<bool>(typeof(ModCatalogue), "Num", null, fraction, false));
        Assert.False(PrivateMember.Call<bool>(typeof(ModCatalogue), "Num", null, fraction, true));
    }

    [Fact]
    public void AStringIsNotANumberAndNothingIsNotANumber()
    {
        Assert.False(PrivateMember.Call<bool>(typeof(ModCatalogue), "Num", null, JsonNode.Parse("\"2\""), false));
        Assert.False(PrivateMember.Call<bool>(typeof(ModCatalogue), "Num", null, null, false));
    }

    [Fact]
    public void AnEntryWithIntegerPDAndTIsAPrefab()
    {
        Assert.True(IsPrefab("{\"p\":1,\"d\":2,\"t\":3}"));
    }

    [Fact]
    public void AnEntryWithoutItsTypeIsNotAPrefab()
    {
        Assert.False(IsPrefab("{\"p\":1,\"d\":2}"));
    }

    [Fact]
    public void AnEntryNotAnObjectIsNotAPrefab()
    {
        Assert.False(IsPrefab("[1,2,3]"));
    }

    [Fact]
    public void AnIntegerListWithAWordInItIsNotAPrefab()
    {
        Assert.False(IsPrefab("{\"p\":1,\"d\":2,\"t\":3,\"is\":[1,\"x\"]}"));
    }

    [Fact]
    public void AnAsListOfIntegerListsIsAccepted()
    {
        Assert.True(IsPrefab("{\"p\":1,\"d\":2,\"t\":3,\"as\":[[1,2],[3]]}"));
    }

    [Fact]
    public void AsListThatIsNotAListOfListsIsNotAPrefab()
    {
        Assert.False(IsPrefab("{\"p\":1,\"d\":2,\"t\":3,\"as\":[1]}"));
    }

    [Fact]
    public void AFractionalGroundRuleValueIsAccepted()
    {
        Assert.True(IsPrefab("{\"p\":1,\"d\":2,\"t\":3,\"gr\":1.5}"));
    }

    [Fact]
    public void AnUnknownKeyMustHoldAnIntegerNumber()
    {
        Assert.False(IsPrefab("{\"p\":1,\"d\":2,\"t\":3,\"zz\":\"text\"}"));
        Assert.False(IsPrefab("{\"p\":1,\"d\":2,\"t\":3,\"zz\":1.5}"));
    }

    /// <summary>Whether the JSON text is a prefab entry, as the catalogue reads it.</summary>
    private static bool IsPrefab(string json) =>
        PrivateMember.Call<bool>(typeof(ModCatalogue), "IsPrefab", null, JsonNode.Parse(json)!);
}
