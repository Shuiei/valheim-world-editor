using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Settings, saved servers, plugin settings and world discovery. These use the app's data folder, so they point it at a
/// temporary one (XDG_DATA_HOME, and their own servers file) and run alone.
/// </summary>
[Collection("DataDir")]
public class AppSettingsTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), "vwe-data-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string? _old = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

    public AppSettingsTests()
    {
        Directory.CreateDirectory(_data);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _data);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _data);
        // The native tests send saved servers to a test file: this test gets its own, in its folder.
        _oldServers = ServerConfig.PathOverride;
        ServerConfig.PathOverride = Path.Combine(_data, "servers.cfg");
        // The data folder from the environment here, not the tests' own.
        _oldDataDir = AppSettings.DataDirOverride;
        AppSettings.DataDirOverride = null;
    }

    private readonly string? _oldServers, _oldDataDir;

    /// <summary>
    /// Puts the data folder and servers file back and deletes the temporary data folder.
    /// </summary>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _old);
        ServerConfig.PathOverride = _oldServers;
        AppSettings.DataDirOverride = _oldDataDir;
        if (Directory.Exists(_data))
        {
            Directory.Delete(_data, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DataFolderFollowsTheEnvironment()
    {
        Assert.StartsWith(_data, AppSettings.DataDir);
    }

    // Claude may run scripts only once the user said so: off unless saved on.
    [Fact]
    public void ClaudeScriptsAreOffUnlessChosen()
    {
        Assert.False(AppSettings.Load().ClaudeScripts);
        new AppSettings { ClaudeScripts = true }.Save();
        Assert.True(AppSettings.Load().ClaudeScripts);
    }

    [Fact]
    public void SavedServersRoundTripWithoutThePasswordUnlessAsked()
    {
        ServerConfig.Remember(new ServerConfig.Server { Name = "A", Host = "my.server.com", User = "valheim", Token = "t1", HostKey = "SHA256:abc", GameFolder = "/home/valheim/server", BridgePort = 5190 });
        ServerConfig.Remember(new ServerConfig.Server { Name = "B", Host = "203.0.113.10", SshPort = 2222, User = "admin", Password = "secret", Token = "t2" });
        var list = ServerConfig.Load();
        Assert.Equal(2, list.Count);
        Assert.Equal("B", list[0].Name);
        var a = list.Single(s => s.Name == "A");
        Assert.Null(a.Password);
        Assert.Equal("t1", a.Token);
        Assert.Equal("SHA256:abc", a.HostKey);
        Assert.Equal("/home/valheim/server", a.GameFolder);
        Assert.Equal(5190, a.BridgePort);
        Assert.Equal("secret", list.Single(s => s.Name == "B").Password);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ServerConfig.FilePath));
        }
        ServerConfig.Forget(a.Id);
        Assert.Single(ServerConfig.Load());
    }

    // A saved password keeps spaces at its ends (it was trimmed, and the login then failed).
    [Fact]
    public void ASavedPasswordKeepsItsSpaces()
    {
        ServerConfig.Remember(new ServerConfig.Server { Name = "S", Host = "my.server.com", User = "valheim", Password = " pass word  " });
        Assert.Equal(" pass word  ", Assert.Single(ServerConfig.Load()).Password);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ServerConfig.FilePath)!, "*.tmp"));
    }

    // Settings that cannot be read are kept aside before the defaults are written over them, and a
    // settings file that cannot be written is left alone without an error.
    // Lists stored as null read as empty, a stored path that is not a valid path does not stop a world opening, and the
    // same folder written with a separator at the end is one entry (they threw, or were listed twice).
    [Fact]
    public void TheRecentListTakesNullsBadPathsAndTheSameFolderWrittenTwoWays()
    {
        string path = Path.Combine(_data, "settings.json");
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
            string world = Path.Combine(_data, "World1");
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
        string path = Path.Combine(_data, "settings.json");
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
        string path = Path.Combine(_data, "settings.json");
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
            Assert.DoesNotContain(_data, line);
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
        string path = Path.Combine(_data, "settings.json");
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
        string blocker = Path.Combine(_data, "not-a-folder");
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
        string path = Path.Combine(_data, "settings.json");
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
        string path = Path.Combine(_data, "settings.json");
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

    [Fact]
    public void RememberingTheSameServerUpdatesIt()
    {
        ServerConfig.Remember(new ServerConfig.Server { Name = "Old name", Host = "my.server.com", User = "valheim", Token = "t1" });
        ServerConfig.Remember(new ServerConfig.Server { Host = "my.server.com", User = "valheim", Token = "t2" });
        var s = Assert.Single(ServerConfig.Load());
        Assert.Equal("Old name", s.Name);
        Assert.Equal("t2", s.Token);
    }

    [Fact]
    public void PluginSettingsAreParsed()
    {
        var b = LocalGame.ParseText("[Http]\nBindAddress = 127.0.0.1\nPort = 5190\nToken = abc123\n", "x.cfg", "test");
        Assert.NotNull(b);
        Assert.Equal(5190, b!.Port);
        Assert.Equal("abc123", b.Token);
        Assert.Null(LocalGame.ParseText("Port = 5182\nToken = \n", "x.cfg", "test"));
        Assert.Equal(5182, LocalGame.ParseText("Token = z", "x.cfg", "test")!.Port);
    }

    [Fact]
    public void WorldFoldersAreChecked()
    {
        using var w = new TempWorld();
        Assert.Null(Worlds.Check(w.Dir));
        Assert.Contains("does not exist", Worlds.Check("/no/such/folder")!);
        Assert.Contains("holds worlds", Worlds.Check(Path.GetDirectoryName(w.Dir)!)!);
        Assert.Contains("No Valheim world", Worlds.Check(_data)!);
    }

    [Fact]
    public void ExtraWorldFoldersAreListed()
    {
        using var w = new TempWorld();
        var settings = new AppSettings { WorldFolders = { Path.GetDirectoryName(w.Dir)! } };
        var found = Worlds.Find(settings);
        var world = Assert.Single(found, x => x.Path == w.Dir);
        Assert.Equal("yours", world.Where);
        Assert.True(world.Usable);
        Assert.Equal(2, world.SaveNumber);
    }

    [Fact]
    public void BepInExFoldersFromSettingsAreSearched()
    {
        string profiles = Path.Combine(_data, "profiles");
        string cfg = Path.Combine(profiles, "Default", "BepInEx", "config");
        Directory.CreateDirectory(cfg);
        Directory.CreateDirectory(Path.Combine(profiles, "Default", "BepInEx", "plugins"));
        File.WriteAllText(Path.Combine(cfg, "local.worldeditorbridge.cfg"), "Port = 5191\nToken = tok\n");
        var settings = new AppSettings { BepInExFolders = { profiles } };
        var bridges = LocalGame.FindBridges(settings, out bool bepInEx, out _);
        Assert.True(bepInEx);
        Assert.Contains(bridges, b => b.Port == 5191 && b.Token == "tok");
    }
}
