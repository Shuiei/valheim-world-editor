using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' worldeditor.json manifests (ModManifests) with links (links, used by ObjectLinks): a manifest's links come after
/// the base ones, a load stays where it is drawn, a link not in the expected form is left out, and the editor ships no
/// manifest of its own. Runs alone, without the player's own Valheim and profiles.
/// </summary>
[Collection("DataDir")]
public sealed class ModManifestsLinksTests : ModManifestsTestBase
{
    // A made-up mod's carts and their loads: each load hangs on its cart, kept where it is drawn (not at the cart).
    private const string Carts = """
        {
          "version": 1,
          "links": [
            {
              "name": "cart",
              "id": "CartId",
              "parents": ["CartParent"],
              "offset": "CartOffset",
              "rotation": "CartTurn",
              "storedAt": "own",
              "references": ["CartOwner"],
              "dropOnCopy": ["CartPacked"]
            }
          ]
        }
        """;

    [Fact]
    public void AManifestsLinksComeAfterTheBaseOnes()
    {
        Manifest("Carts", Carts);

        Assert.Equal(1, ModManifests.Refresh(Settings));

        var rules = ObjectLinks.Current.Rules;
        Assert.Equal("vehicle", rules[0].Name);
        var cart = rules[^1];
        Assert.Equal(("cart", StableHash.Of("CartId"), StableHash.Of("CartOffset"), StableHash.Of("CartTurn"), 0, false),
            (cart.Name, cart.IdKey, cart.OffsetKey, cart.EulerKey, cart.QuatKey, cart.StoredAtRoot));
        Assert.Equal([StableHash.Of("CartParent")], cart.ParentKeys);
        Assert.Equal([StableHash.Of("CartOwner")], cart.ReferenceKeys);
        Assert.Equal([StableHash.Of("CartPacked")], cart.DropKeys);
        Assert.True(ObjectLinks.IsLinkKey(StableHash.Of("CartOffset")));
        Assert.False(ObjectLinks.IsLinkKey(StableHash.Of("CartOwner")));
    }

    // Stored where it is drawn: a pasted load stays where it was pasted (not moved to its cart), its id and its
    // cart's following the copy, what copies leave out gone.
    [Fact]
    public void APartKeptWhereItIsDrawnIsPastedWhereItGoes()
    {
        Manifest("Carts", Carts);
        ModManifests.Refresh(Settings);
        static ObjectField Int(string key, int value) => new("ints", StableHash.Of(key), value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        int prefab = StableHash.Of("Cart");
        var copy = ObjectLinks.Remap(
        [
            new(0, prefab, new Vector3(10, 30, 10), Vector3.Zero, 0, Data: [Int("CartId", 5)]),
            new(0, prefab, new Vector3(11, 31, 10), Vector3.Zero, 0, Data: [Int("CartParent", 5), new ObjectField("vec3", StableHash.Of("CartOffset"), "1 1 0"),
                new ObjectField("bytes", StableHash.Of("CartPacked"), "AQ==")]),
        ], new HashSet<int> { 5 });

        int id = int.Parse(copy[0].Data!.Single(f => f.Key == StableHash.Of("CartId")).Value!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotEqual(5, id);
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), copy[1].Data!.Single(f => f.Key == StableHash.Of("CartParent")).Value);
        Assert.Equal(new Vector3(11, 31, 10), copy[1].Position);
        Assert.Null(copy[1].Data!.Single(f => f.Key == StableHash.Of("CartPacked")).Value);
    }

    [Theory]
    [InlineData("""{ "parents": ["P"], "offset": "O" }""")]
    [InlineData("""{ "id": "I", "offset": "O" }""")]
    [InlineData("""{ "id": "I", "parents": [], "offset": "O" }""")]
    [InlineData("""{ "id": "I", "parents": "P", "offset": "O" }""")]
    [InlineData("""{ "id": "I", "parents": ["P"] }""")]
    [InlineData("""{ "id": "I", "parents": ["P"], "offset": "O", "storedAt": "elsewhere" }""")]
    [InlineData("""{ "id": "I", "parents": ["P"], "offset": "O", "references": [3] }""")]
    [InlineData("""{ "id": "I", "parents": ["P"], "offset": "O", "rotation": 1 }""")]
    [InlineData("""7""")]
    public void ALinkNotInTheExpectedFormIsLeftOut(string rule)
    {
        Manifest("Mod", $$"""{ "version": 1, "links": [{{rule}}], "prefabs": { "p": { "hidden": true } } }""");

        Assert.Equal(1, ModManifests.Refresh(Settings));

        Assert.DoesNotContain(ObjectLinks.Current.Rules, r => r.IdKey == StableHash.Of("I"));
        Assert.Single(ObjectLinks.Current.Rules);
        Assert.True(ModManifests.IsHidden("p"));
    }

    // The editor ships no manifest for any mod; the tests' example mod stands in (ModManifests.Base). A newer file's
    // rule of the same id key replaces an older one's in its place: first stays first.
    [Fact]
    public void TheEditorShipsNoManifestAndANewerRuleTakesAnOlderOnesPlace()
    {
        var example = ExampleMod.Rule;
        Assert.Equal(("vehicle", StableHash.Of(ExampleMod.IdKey), true), (example.Name, example.IdKey, example.StoredAtRoot));
        ModManifests.Base = [];
        try
        {
            Assert.Empty(ObjectLinks.Current.Rules);
            Assert.Equal(0, ModManifests.Refresh(Settings));
            Assert.Empty(ObjectLinks.Current.Rules);
        }
        finally
        {
            ModManifests.Base = [ExampleMod.Manifest];
        }

        // The same rule and one reference more, newer than the carts' file.
        string own = Manifest("ExampleVehicles", ExampleMod.Manifest.Replace($""""{ExampleMod.DrivesKey}"]"""", $""""{ExampleMod.DrivesKey}", "TestReference"]"""", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(own, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Manifest("Carts", Carts), new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        ModManifests.Refresh(Settings);

        var rules = ObjectLinks.Current.Rules;
        Assert.Equal(2, rules.Count);
        Assert.Contains(StableHash.Of("TestReference"), rules[0].ReferenceKeys);
        Assert.Equal("cart", rules[1].Name);
        ModManifests.Forget();
        Assert.DoesNotContain(StableHash.Of("TestReference"), Assert.Single(ObjectLinks.Current.Rules).ReferenceKeys);
    }
}
