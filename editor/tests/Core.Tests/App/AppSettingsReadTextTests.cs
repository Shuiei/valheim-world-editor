using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading the settings file's text: its contents when it is there, and none (the defaults) when its folder or the file
/// is not there.
/// </summary>
public class AppSettingsReadTextTests : AppSettingsTestBase
{
    [Fact]
    public void ASettingsFileThatIsThereIsReadAsItsText()
    {
        string path = Path.Combine(Data, "settings.json");
        File.WriteAllText(path, "{\"theme\":\"dark\"}");

        Assert.Equal("{\"theme\":\"dark\"}", PrivateCall.Static(typeof(AppSettings), "ReadText", path));
    }

    [Fact]
    public void ASettingsFileMissingFromAFolderThatIsThereIsNone()
    {
        // The folder is there but the file never was: the read is tried a second time, then gives none.
        string path = Path.Combine(Data, "settings.json");

        Assert.Null(PrivateCall.Static(typeof(AppSettings), "ReadText", path));
    }

    [Fact]
    public void ASettingsFileInAFolderThatIsNotThereIsNone()
    {
        string path = Path.Combine(Data, "no-such-folder", "settings.json");

        Assert.Null(PrivateCall.Static(typeof(AppSettings), "ReadText", path));
    }
}
