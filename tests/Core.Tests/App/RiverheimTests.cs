using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Riverheim as the editor reads it: its header in a world file (its preset kept, the rest of the head read as usual;
/// its beta's, another generator's or an unknown setting refused with why; written as Riverheim writes it for a world
/// made with its rules), why such a world cannot be opened, and where its plugin is found.
/// </summary>
[Collection("DataDir")]
public class RiverheimTests : AppSettingsTestBase
{
    /// <summary>The test world's head rewritten with these Riverheim settings, then read.</summary>
    private static WorldSave.Metadata Head(TempWorld w, (string, object)[] riverheim, string? generator = null)
    {
        string path = Path.Combine(w.Dir, "_main.2.fwl2");
        WorldFileHead.Write(path, 42, ["defeated_eikthyr"], riverheim, generator);
        return WorldSave.ReadMetadata(path);
    }

    [Fact]
    public void AWorldItMadeKeepsItsPresetAndTheRestOfItsHead()
    {
        using var w = new TempWorld();
        var head = Head(w, WorldFileHead.RiverheimPreset);
        Assert.Equal(new Riverheim.Tag("builtin.amur", 2), head.RiverheimTag);
        Assert.Equal((WorldFileHead.Name, WorldFileHead.SeedName, StableHash.Of(WorldFileHead.SeedName), 2), (head.Name, head.SeedName, head.Seed, head.WorldGenVersion));
        Assert.Equal(["defeated_eikthyr"], head.StartingGlobalKeys);
        // The save opened from it keeps the settings; a world it did not make has none.
        Assert.Equal(head.RiverheimTag, w.Load().RiverheimTag);
        WorldFileHead.Write(Path.Combine(w.Dir, "_main.2.fwl2"), 42, []);
        Assert.Null(w.Load().RiverheimTag);
    }

    [Fact]
    public void ItsBetaAndOtherGeneratorsCannotBeRead()
    {
        using var w = new TempWorld();
        Assert.Equal(Lang.T("riverheim.beta"), Assert.Throws<GeneratorHeaderException>(() => Head(w, WorldFileHead.RiverheimPreset, Riverheim.BetaPluginId)).Message);
        Assert.Equal(Lang.T("riverheim.unknownGenerator", "made.up.generator"), Assert.Throws<GeneratorHeaderException>(() => Head(w, WorldFileHead.RiverheimPreset, "made.up.generator")).Message);
    }

    [Fact]
    public void ASettingItDoesNotKnowOrNoPresetCannotBeRead()
    {
        using var w = new TempWorld();
        // A setting's value has the size its name gives: an unknown name ends the read.
        Assert.Equal(Lang.T("riverheim.unknownSetting", "made.up"), Assert.Throws<GeneratorHeaderException>(() => Head(w, [("preset", "builtin.amur"), ("made.up", 3)])).Message);
        Assert.Equal(Lang.T("riverheim.noPreset"), Assert.Throws<GeneratorHeaderException>(() => Head(w, [("preset.version", 2)])).Message);
        Assert.Equal(Lang.T("riverheim.noPreset"), Assert.Throws<GeneratorHeaderException>(() => Head(w, [("preset", "builtin.amur")])).Message);
    }

    [Fact]
    public void AWorldMadeWithItsRulesHasItsHeader()
    {
        string folder = Path.Combine(Data, "Made");
        var rules = new WorldRules { Riverheim = new RiverheimRules("1.2.1", "builtin.amur", 2) };
        WorldCreator.Create(folder, "Made", "abcde", rules: rules);
        var head = WorldSave.ReadMetadata(Path.Combine(folder, "_main.1.fwl2"));
        Assert.Equal((new Riverheim.Tag("builtin.amur", 2), "Made", "abcde"), (head.RiverheimTag, head.Name, head.SeedName));
        WorldCreator.Create(Path.Combine(Data, "Plain"), "Plain", "abcde");
        Assert.Null(WorldSave.ReadMetadata(Path.Combine(Data, "Plain", "_main.1.fwl2")).RiverheimTag);
    }

    [Fact]
    public void TheRefusalSaysWhatIsMissing()
    {
        var tag = new Riverheim.Tag("builtin.amur", 2);
        int asked = 0;
        Func<string?> none = () => { asked++; return null; }, found = () => { asked++; return Data; };
        // A world Riverheim did not make, and the switch off: the folders are not looked at.
        Assert.Null(Riverheim.Refusal(null, new AppSettings { Riverheim = true }, none));
        Assert.Equal(Lang.T("riverheim.off"), Riverheim.Refusal(tag, new AppSettings(), found));
        Assert.Equal(0, asked);
        Assert.Equal(Lang.T("riverheim.missing"), Riverheim.Refusal(tag, new AppSettings { Riverheim = true }, none));
        Assert.Null(Riverheim.Refusal(tag, new AppSettings { Riverheim = true }, found));
    }

    [Fact]
    public void ItsPluginIsFoundInTheExpandWorldFolderFirst()
    {
        string first = Bep("first", Riverheim.DllName), chosen = Bep("chosen", Riverheim.DllName), off = Bep("off", Riverheim.DllName + ".old");
        Assert.Equal(chosen, Riverheim.Find(new AppSettings { BepInExFolders = { first }, ExpandWorldFolder = chosen }));
        // A mod manager's file of a mod turned off does not count: the next folder has it.
        Assert.Equal(first, Riverheim.Find(new AppSettings { BepInExFolders = { first }, ExpandWorldFolder = off }));
    }

    /// <summary>A BepInEx folder with this file in a mod manager's plugin folder.</summary>
    private string Bep(string name, string file)
    {
        string plugins = Path.Combine(Data, name, "BepInEx", "plugins", "Gurebu-Riverheim");
        Directory.CreateDirectory(plugins);
        File.WriteAllBytes(Path.Combine(plugins, file), []);
        return Path.Combine(Data, name, "BepInEx");
    }
}
