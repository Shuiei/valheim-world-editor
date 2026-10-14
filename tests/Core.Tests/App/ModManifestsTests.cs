using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' worldeditor.json manifests (ModManifests) in the settings' plugins folders, at any depth: what they say of
/// data keys (label, read-only, help) and prefabs (hidden, group); comments and trailing commas allowed; a file of
/// another version, or not JSON, skipped; entries not in the expected form left out; the newest file winning a name
/// two say something of. Runs alone, without the player's own Valheim and profiles.
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
        Manifest("zolantris-ValheimRAFT/Assets", """
            {
              // Comments and trailing commas are fine.
              "version": 1,
              "data": {
                "MBParentId": { "label": "Parent vehicle", "readOnly": true, "help": "The vehicle this piece is part of." },
                "MBPosition": { "label": "Place on the vehicle" },
              },
              "prefabs": {
                "BoundaryChunk": { "hidden": true },
                "hull_rib": { "group": "Hulls" },
              },
            }
            """);

        Assert.Equal(1, ModManifests.Refresh(Settings));

        Assert.Equal(new ModManifests.DataNote("Parent vehicle", true, "The vehicle this piece is part of."), ModManifests.DataOf("MBParentId"));
        Assert.Equal(new ModManifests.DataNote("Place on the vehicle", false, null), ModManifests.DataOf("MBPosition"));
        Assert.Null(ModManifests.DataOf("MBRotation"));
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
}
