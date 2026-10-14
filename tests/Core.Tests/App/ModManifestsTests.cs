using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' worldeditor.json manifests (ModManifests) in the settings' plugins folders, at any depth: what they say of
/// data keys (label, read-only, help), prefabs (hidden, group) and how objects link into one (links, used by
/// ObjectLinks); comments and trailing commas allowed; a file of another version, or not JSON, skipped; entries not in
/// the expected form left out; the newest file winning a name two say something of. Runs alone, without the player's
/// own Valheim and profiles.
/// </summary>
[Collection("DataDir")]
public sealed class ModManifestsTests : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-manifests-" + Guid.NewGuid().ToString("N")[..8]);

    public ModManifestsTests()
    {
        // Only the folders a test gives.
        LocalGame.SearchDefaultPlaces = false;
    }

    /// <summary>
    /// Forgets the manifests, puts the default places' search back, and deletes the folder.
    /// </summary>
    public void Dispose()
    {
        ModManifests.Forget();
        LocalGame.SearchDefaultPlaces = _defaults;
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Settings naming the test's BepInEx folder.</summary>
    private AppSettings Settings => new() { BepInExFolders = [Path.Combine(_dir, "BepInEx")] };

    /// <summary>Writes a manifest in a mod's folder under plugins (made); returns its path.</summary>
    private string Manifest(string mod, string json)
    {
        string folder = Path.Combine(_dir, "BepInEx", "plugins", mod);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, ModManifests.FileName);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void AManifestNamesDataAndHidesAndGroupsPrefabs()
    {
        Manifest("SomeMod/Assets", """
            {
              // Comments and trailing commas are fine.
              "version": 1,
              "data": {
                "Mod_Parent": { "label": "Parent vehicle", "readOnly": true, "help": "The vehicle this piece is part of." },
                "Mod_Place": { "label": "Place on the vehicle" },
              },
              "prefabs": {
                "BoundaryChunk": { "hidden": true },
                "hull_rib": { "group": "Hulls" },
              },
            }
            """);

        Assert.Equal(1, ModManifests.Refresh(Settings));

        Assert.Equal(new ModManifests.DataNote("Parent vehicle", true, "The vehicle this piece is part of."), ModManifests.DataOf("Mod_Parent"));
        Assert.Equal(new ModManifests.DataNote("Place on the vehicle", false, null), ModManifests.DataOf("Mod_Place"));
        Assert.Null(ModManifests.DataOf("Mod_Turn"));
        Assert.Null(ModManifests.DataOf(null));
        Assert.True(ModManifests.IsHidden("BoundaryChunk"));
        Assert.False(ModManifests.IsHidden("hull_rib"));
        Assert.Equal("Hulls", ModManifests.PrefabOf("hull_rib")?.Group);
        Assert.Null(ModManifests.PrefabOf("woodwall"));
    }

    [Theory]
    [InlineData("""{ "data": {} }""")]
    [InlineData("""{ "version": 2, "data": { "k": { "label": "x" } } }""")]
    [InlineData("""{ "version": "1", "data": { "k": { "label": "x" } } }""")]
    [InlineData("""[1, 2]""")]
    [InlineData("""not json""")]
    public void AFileThatIsNotAManifestOfThisVersionIsSkipped(string json)
    {
        Manifest("Mod", json);

        Assert.Equal(0, ModManifests.Refresh(Settings));
        Assert.Null(ModManifests.DataOf("k"));
    }

    [Fact]
    public void EntriesNotInTheExpectedFormAreLeftOut()
    {
        Manifest("Mod", $$"""
            {
              "version": 1,
              "data": {
                "good": { "label": "Good" },
                "number": { "label": 3 },
                "empty": { "label": " " },
                "long": { "label": "{{new string('x', 201)}}" },
                "flag": { "readOnly": "yes" },
                "list": [1]
              },
              "prefabs": { "good": { "group": "G" }, "flag": { "hidden": 1 } }
            }
            """);

        Assert.Equal(1, ModManifests.Refresh(Settings));

        Assert.Equal("Good", ModManifests.DataOf("good")?.Label);
        foreach (string bad in (string[])["number", "empty", "long", "flag", "list"])
        {
            Assert.Null(ModManifests.DataOf(bad));
        }
        Assert.Equal("G", ModManifests.PrefabOf("good")?.Group);
        Assert.Null(ModManifests.PrefabOf("flag"));
    }

    [Fact]
    public void TheNewestFileWinsANameTwoSayAnythingOf()
    {
        string old = Manifest("Old", """{ "version": 1, "data": { "shared": { "label": "Old" }, "own": { "label": "Mine" } } }""");
        string newer = Manifest("New", """{ "version": 1, "data": { "shared": { "label": "New" } } }""");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

        Assert.Equal(2, ModManifests.Refresh(Settings));

        Assert.Equal("New", ModManifests.DataOf("shared")?.Label);
        Assert.Equal("Mine", ModManifests.DataOf("own")?.Label);
    }

    [Fact]
    public void WithoutManifestsNothingIsSaidAndTheOldNotesGo()
    {
        string path = Manifest("Mod", """{ "version": 1, "prefabs": { "p": { "hidden": true } } }""");
        ModManifests.Refresh(Settings);
        int before = ModManifests.Generation;
        File.Delete(path);

        Assert.Equal(0, ModManifests.Refresh(Settings));

        Assert.False(ModManifests.IsHidden("p"));
        Assert.NotEqual(before, ModManifests.Generation);
        Assert.Equal(0, ModManifests.Refresh(new AppSettings { BepInExFolders = [Path.Combine(_dir, "missing")] }));
    }

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
