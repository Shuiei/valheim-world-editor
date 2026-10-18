using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The mods' worldeditor.json manifests (ModManifests) in the settings' plugins folders, at any depth: what they say of
/// data keys (label, read-only, help) and prefabs (hidden, group); a file of another version, or not JSON, skipped; entries
/// not in the expected form left out; the newest file winning a name two say something of. Runs alone, without the player's
/// own Valheim and profiles.
/// </summary>
[Collection("DataDir")]
public sealed class ModManifestsTests : ModManifestsTestBase
{
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

    [Fact]
    public void AManifestLyingInPluginsItselfOffersNoExtension()
    {
        string mod = Path.Combine(Dir, "BepInEx", "plugins", "SomeMod");
        Directory.CreateDirectory(mod);
        File.WriteAllBytes(Path.Combine(mod, "Some.dll"), [0]);
        File.WriteAllText(Path.Combine(Dir, "BepInEx", "plugins", ModManifests.FileName),
            """{ "version": 1, "extension": "SomeMod/Some.dll", "data": { "k": { "label": "x" } } }""");

        Assert.Equal(1, ModManifests.Refresh(Settings));

        // What it says of data is still used; the DLL is not offered under the name "plugins".
        Assert.Equal("x", ModManifests.DataOf("k")?.Label);
        Assert.Empty(ModManifests.Extensions);
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
        Assert.Equal(0, ModManifests.Refresh(new AppSettings { BepInExFolders = [Path.Combine(Dir, "missing")] }));
    }
}
