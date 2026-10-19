using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// YAML read through a YamlDotNet library found under a BepInEx plugins folder (the one the mod ships; here the test
/// project's copy): finding and loading it, a document as plain lists and maps, and what cannot be read.
/// </summary>
[Collection("DataDir")]
public class YamlReaderTests : AppSettingsTestBase
{
    /// <summary>A plugins folder with the library in a package subfolder, as a mod manager lays it out.</summary>
    private string Plugins()
    {
        string plugins = Path.Combine(Data, "plugins");
        string package = Path.Combine(plugins, "ValheimModding-YamlDotNet");
        Directory.CreateDirectory(package);
        File.Copy(typeof(YamlDotNet.Serialization.Deserializer).Assembly.Location, Path.Combine(package, YamlReader.DllName));
        return plugins;
    }

    [Fact]
    public void TheLibraryIsFoundUnderThePluginsFolderAndLoadedOnce()
    {
        string plugins = Plugins();
        Assert.Equal(Path.Combine(plugins, "ValheimModding-YamlDotNet", YamlReader.DllName), YamlReader.FindDll(plugins));
        var reader = YamlReader.Find(plugins);
        Assert.NotNull(reader);
        Assert.Same(reader, YamlReader.Find(plugins));
        Assert.Same(reader, YamlReader.Load(reader.Path));
    }

    [Fact]
    public void AFolderWithoutTheLibraryGivesNoReader()
    {
        string plugins = Path.Combine(Data, "plugins");
        Directory.CreateDirectory(plugins);
        Assert.Null(YamlReader.FindDll(plugins));
        Assert.Null(YamlReader.Find(plugins));
        Assert.Null(YamlReader.Find(Path.Combine(Data, "nowhere")));
        // A file that is no library.
        string fake = Path.Combine(plugins, "Fake", YamlReader.DllName);
        Directory.CreateDirectory(Path.GetDirectoryName(fake)!);
        File.WriteAllText(fake, "not a library");
        Assert.Null(YamlReader.Find(plugins));
        Assert.Throws<BadImageFormatException>(() => YamlReader.Load(fake));
    }

    [Fact]
    public void ADocumentIsReadAsPlainListsAndMaps()
    {
        var reader = YamlReader.Find(Plugins())!;
        var list = reader.ReadList("- biome: ashlands\n  centerY: 0.4\n  boiling: true\n  names: [a, b]\n  environments:\n  - environment: Clear\n    weight: 1\n- just a text\n- biome: \"ocean\"\n  maxAltitude: -26\n");
        Assert.Equal(2, list.Count);
        Assert.Equal("ashlands", list[0]["biome"]);
        Assert.Equal("0.4", list[0]["centerY"]);
        Assert.Equal("true", list[0]["boiling"]);
        Assert.Equal(["a", "b"], Assert.IsType<List<object?>>(list[0]["names"]));
        var env = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(list[0]["environments"])));
        Assert.Equal("Clear", env["environment"]);
        Assert.Equal("ocean", list[1]["biome"]);
        Assert.Equal("-26", list[1]["maxAltitude"]);
        Assert.Empty(reader.ReadList(""));
        Assert.Empty(reader.ReadList("# only a comment\n"));
        Assert.Empty(reader.ReadList("biome: not a list\n"));
    }

    [Fact]
    public void ADocumentThatIsNotYamlSaysWhy()
    {
        var reader = YamlReader.Find(Plugins())!;
        var ex = Assert.Throws<InvalidDataException>(() => reader.ReadList("- biome: [unclosed\n"));
        Assert.NotEmpty(ex.Message);
    }
}
