using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Paths as typed (Places.ExpandHome): trimmed and unquoted, the home folder for a leading ~ alone or before a
/// separator, and another user's ~name left alone. And a folder that cannot be read: no subfolders, no world.
/// </summary>
public class PlacesTests
{
    [Fact]
    public void AFolderThatCannotBeReadHasNoSubfoldersAndNoWorld()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }
        string locked = Path.Combine(Path.GetTempPath(), "vwe-places-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(locked, "inside"));
        File.WriteAllText(Path.Combine(locked, "_main.1.chunks"), "");
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            Assert.Empty(Places.Subfolders(locked));
            Assert.False(Places.HasWorld(locked));
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(locked, true);
        }
        // A folder that is not there: none either.
        Assert.Empty(Places.Subfolders(locked));
        Assert.False(Places.HasWorld(locked));
    }

    [Fact]
    public void OnlyTheOwnHomeIsExpanded()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home, Places.ExpandHome(" ~ "));
        Assert.Equal(home + "/worlds", Places.ExpandHome("\"~/worlds\""));
        Assert.Equal("~bob/worlds", Places.ExpandHome("~bob/worlds"));
        Assert.Equal("/srv/~/x", Places.ExpandHome("/srv/~/x"));
    }
}
