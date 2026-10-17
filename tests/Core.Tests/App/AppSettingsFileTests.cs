using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The settings file (AppSettings): read and saved, a broken or held file kept aside or not saved over, a folder that cannot
/// be made, the recent worlds, and only the user able to read it.
/// </summary>
[Collection("DataDir")]
public class AppSettingsFileTests : AppSettingsTestBase
{
    [Fact]
    public void DataFolderFollowsTheEnvironment()
    {
        Assert.StartsWith(Data, AppSettings.DataDir);
    }

    // Claude may run scripts only once the user said so: off unless saved on.
    [Fact]
    public void ClaudeScriptsAreOffUnlessChosen()
    {
        Assert.False(AppSettings.Load().ClaudeScripts);
        new AppSettings { ClaudeScripts = true }.Save();
        Assert.True(AppSettings.Load().ClaudeScripts);
    }

    // Settings that cannot be read are kept aside before the defaults are written over them, and a
    // settings file that cannot be written is left alone without an error.
    // Lists stored as null read as empty, a stored path that is not a valid path does not stop a world opening, and the
    // same folder written with a separator at the end is one entry (they threw, or were listed twice).
    [Fact]
    public void TheRecentListTakesNullsBadPathsAndTheSameFolderWrittenTwoWays()
    {
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            File.WriteAllText(path, """{ "Recent": null, "WorldFolders": null, "BepInExFolders": [null], "Extensions": null }""");
            var s = AppSettings.Load();
            Assert.Empty(s.Recent);
            Assert.Empty(s.WorldFolders);
            Assert.Empty(s.BepInExFolders);
            Assert.Empty(s.Extensions);
            s.Recent.Add(new AppSettings.RecentWorld("bad\0path", "Bad", DateTime.Now));
            string world = Path.Combine(Data, "World1");
            s.AddRecent(world + Path.DirectorySeparatorChar, "World1");
            s.AddRecent(world, "World1");
            Assert.Equal([world, "bad\0path"], s.Recent.Select(r => r.Path));
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }

    [Fact]
    public void BrokenSettingsAreKeptAsideAndAFailedWriteIsQuiet()
    {
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            Check(path);
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }

    /// <summary>
    /// Checks the settings file at this path: a broken one is kept aside as .bad and read as defaults, a save leaves no
    /// .tmp behind, and a save where a folder blocks the file fails quietly.
    /// </summary>
    private static void Check(string path)
    {
        File.WriteAllText(path, "{\"ValheimPath\": \"/games/Valh");
        var s = AppSettings.Load();
        Assert.Null(s.ValheimPath);
        Assert.Equal("{\"ValheimPath\": \"/games/Valh", File.ReadAllText(path + ".bad"));
        s.ValheimPath = "/games/Valheim";
        s.Save();
        Assert.Equal("/games/Valheim", AppSettings.Load().ValheimPath);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        File.Delete(path);
        Directory.CreateDirectory(path);
        s.Save();
        Assert.True(Directory.Exists(path));
    }

    // A save that fails is said in the log, without the settings file's path.
    [Fact]
    public void AFailedSaveIsLoggedWithoutItsPath()
    {
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            // A folder where the file goes: the write fails.
            Directory.CreateDirectory(path);
            using var log = Log.Listen();

            Assert.NotNull(new AppSettings().Save());
            string line = Assert.Single(log.Lines);
            Assert.StartsWith("Caught at AppSettings.cs:", line);
            Assert.Contains("while saving settings.json (the settings stay in memory): ", line);
            Assert.DoesNotContain(Data, line);
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }

    // A settings file held by another program (a sync or antivirus tool) gives the defaults for the session, and a save
    // leaves it as it is: what it holds is the user's settings.
    [Fact]
    public void SettingsThatCannotBeReadAreNotSavedOver()
    {
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            File.WriteAllText(path, "{\"ValheimPath\": \"/games/Valheim\"}");
            AppSettings held;
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                held = AppSettings.Load();
            }
            Assert.True(held.FileUnread);
            Assert.Null(held.ValheimPath);
            held.ValheimPath = "/elsewhere";
            held.Save();
            Assert.Equal("{\"ValheimPath\": \"/games/Valheim\"}", File.ReadAllText(path));
            // Read: as the file says, and saved as usual.
            var read = AppSettings.Load();
            Assert.False(read.FileUnread);
            Assert.Equal("/games/Valheim", read.ValheimPath);
            read.ValheimPath = "/elsewhere";
            read.Save();
            Assert.Equal("/elsewhere", AppSettings.Load().ValheimPath);
            Assert.DoesNotContain("FileUnread", File.ReadAllText(path));
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }

    // A data folder that cannot be made (a file where it should be): the defaults, not saved, and neither the load nor
    // the save throws while saying so.
    [Fact]
    public void SettingsWhoseFolderCannotBeMadeAreDefaults()
    {
        string blocker = Path.Combine(Data, "not-a-folder");
        File.WriteAllText(blocker, "");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = null;
        AppSettings.DataDirOverride = Path.Combine(blocker, "data");
        try
        {
            var settings = AppSettings.Load();
            Assert.True(settings.FileUnread);
            settings.Save();
        }
        finally
        {
            AppSettings.PathOverride = old;
            AppSettings.DataDirOverride = null;
        }
    }

    // No settings file at the start, and one there at the first save (a sync program put the user's back meanwhile):
    // left as it is. Written by the editor itself, it is written again.
    [Fact]
    public void ASettingsFileThatAppearedSinceTheStartIsNotSavedOver()
    {
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            var fresh = AppSettings.Load();
            Assert.False(fresh.FileUnread);
            File.WriteAllText(path, "{\"ValheimPath\": \"/games/Valheim\"}");
            fresh.ValheimPath = "/elsewhere";
            fresh.Save();
            Assert.Equal("{\"ValheimPath\": \"/games/Valheim\"}", File.ReadAllText(path));
            File.Delete(path);
            fresh.Save();
            Assert.Equal("/elsewhere", AppSettings.Load().ValheimPath);
            fresh.ValheimPath = "/again";
            fresh.Save();
            Assert.Equal("/again", AppSettings.Load().ValheimPath);
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }

    // The settings hold Claude's token: only the user can read them, even over a file others could read.
    [Fact]
    public void OnlyTheUserCanReadTheSettings()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        string path = Path.Combine(Data, "settings.json");
        string? old = AppSettings.PathOverride;
        AppSettings.PathOverride = path;
        try
        {
            File.WriteAllText(path, "{}");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            new AppSettings { ClaudeToken = "secret" }.Save();
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal("secret", AppSettings.Load().ClaudeToken);
        }
        finally
        {
            AppSettings.PathOverride = old;
        }
    }
}
