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

    // A file where a folder is expected (a world folder listed in the settings that was replaced by a file): read as a
    // folder it fails, which is passed over as a folder that cannot be read is.
    [Fact]
    public void AFileWhereAFolderIsExpectedHasNoSubfoldersAndNoWorld()
    {
        string file = Path.Combine(Path.GetTempPath(), "vwe-places-" + Guid.NewGuid().ToString("N")[..8]);
        File.WriteAllText(file, "not a folder");
        try
        {
            Assert.Empty(Places.Subfolders(file));
            Assert.False(Places.HasWorld(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    // A folder reached through a link that cannot be followed (two links pointing at each other): its key is the path as
    // written from there on, and the failure is said in the log.
    [Fact]
    public void AFolderLinkThatCannotBeFollowedLeavesThePathAsWritten()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        string dir = Path.Combine(Path.GetTempPath(), "vwe-folderkey-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.CreateSymbolicLink(Path.Combine(dir, "a"), Path.Combine(dir, "b"));
            File.CreateSymbolicLink(Path.Combine(dir, "b"), Path.Combine(dir, "a"));
            using var log = Log.Listen();

            string key = Places.FolderKey(Path.Combine(dir, "a", "worlds"));

            Assert.Equal(Path.Combine(Path.GetFullPath(dir), "a", "worlds"), key);
            Assert.Contains(log.Lines, l => l.Contains("following a folder's link", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
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
