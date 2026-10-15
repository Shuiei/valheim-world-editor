using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Finding Valheim and following its files' state, on a pretend home (HOME and the XDG folders point at a temporary
/// folder, put back afterwards; Steam's folders outside the home are not looked at): the game in another Steam
/// library listed in libraryfolders.vdf, the folder set in the settings first, a chosen folder taken or refused, the
/// state from missing to read, and the copy older editors made deleted.
/// </summary>
[Collection("DataDir")]
public sealed class GameLookTests : IDisposable
{
    private static readonly string[] Vars = ["HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA"];
    private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
    private readonly string? _settings = AppSettings.PathOverride;
    private readonly bool _outside = GameLook.SearchOutsideHome;
    private readonly string _home = Path.Combine(Path.GetTempPath(), "vwe-gamelook-" + Guid.NewGuid().ToString("N")[..8]);

    public GameLookTests()
    {
        Directory.CreateDirectory(_home);
        Environment.SetEnvironmentVariable("HOME", _home);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(_home, ".config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(_home, ".local", "share"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(_home, "AppData", "Local"));
        AppSettings.PathOverride = Path.Combine(_home, "settings.json");
        GameLook.SearchOutsideHome = false;
    }

    /// <summary>
    /// Leaves the game missing again (no pretend game kept in use), puts the environment and settings back, and
    /// deletes the pretend home.
    /// </summary>
    public void Dispose()
    {
        GameLook.Check(new AppSettings());
        foreach (var (k, v) in _old)
        {
            Environment.SetEnvironmentVariable(k, v);
        }
        AppSettings.PathOverride = _settings;
        GameLook.SearchOutsideHome = _outside;
        try
        {
            Directory.Delete(_home, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// A folder that looks like the game's (its bundles folder, empty).
    /// </summary>
    private static string FakeValheim(string valheim)
    {
        Directory.CreateDirectory(Path.Combine(valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
        return valheim;
    }

    /// <summary>
    /// Waits for the game's state to leave "running" (its files read in the background), at most 30 seconds.
    /// </summary>
    private static async Task<string> Settled()
    {
        for (int i = 0; i < 300 && GameLook.State == "running"; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        return GameLook.State;
    }

    [Fact]
    public void AMacGameIsFoundByItsAppBundle()
    {
        // As Steam installs it on a Mac: the game's data inside valheim.app, next to BepInEx.
        string valheim = Path.Combine(_home, "Library", "Application Support", "Steam", "steamapps", "common", "Valheim");
        string app = Path.Combine(valheim, "valheim.app");
        string data = Path.Combine(app, "Contents", "Resources", "Data");
        Directory.CreateDirectory(Path.Combine(data, "StreamingAssets", "SoftRef", "Bundles"));
        Directory.CreateDirectory(Path.Combine(valheim, "BepInEx"));
        Assert.Equal(data, GameLook.DataDir(valheim));
        Assert.Equal(Path.Combine(data, "StreamingAssets", "SoftRef", "Bundles"), GameLook.BundlesDir(valheim));
        Assert.Equal(valheim, GameLook.GameOfData(data));
        Assert.Equal(valheim, GameLook.FindValheim(valheim));
        // Chosen at the bundle or inside it (the Mac's folder picker can open it): the folder holding it.
        foreach (string chosen in new[] { valheim, app, Path.Combine(app, "Contents"), data })
        {
            Assert.Equal(valheim, GameLook.GameFolder(chosen));
        }
        // An app without the game's bundles is not the game.
        string other = Path.Combine(_home, "Other");
        Directory.CreateDirectory(Path.Combine(other, "Other.app", "Contents", "Resources", "Data"));
        Assert.Null(GameLook.DataDir(other));
        Assert.Null(GameLook.GameFolder(Path.Combine(other, "Other.app")));
        // On Windows and Linux the data folder's game is the folder above it.
        string linux = FakeValheim(Path.Combine(_home, "Valheim"));
        Assert.Equal(linux, GameLook.GameOfData(Path.Combine(linux, "valheim_Data")));
    }

    [Fact]
    public void TheGameIsFoundInASteamLibraryListedBySteam()
    {
        Assert.Null(GameLook.FindValheim(null));
        // Steam in the home, with Valheim in a second library it lists (one listed library does not exist, and Steam's
        // own folder is listed too, as Steam does).
        string steam = Path.Combine(_home, ".steam", "steam");
        string library = Path.Combine(_home, "Games", "SteamLibrary");
        string valheim = FakeValheim(Path.Combine(library, "steamapps", "common", "Valheim"));
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
                "0" { "path"		"{{steam}}" }
                "1" { "path"		"{{Path.Combine(_home, "Gone")}}" }
                "2" { "path"		"{{library}}" }
            }
            """);
        Assert.Equal(valheim, GameLook.FindValheim(null));
        // A folder set in the settings comes first, when it is the game's; else the search goes on.
        string chosen = FakeValheim(Path.Combine(_home, "Valheim"));
        Assert.Equal(chosen, GameLook.FindValheim(chosen));
        Assert.Equal(valheim, GameLook.FindValheim(Path.Combine(_home, "nowhere")));
    }

    [Fact]
    public async Task AChosenFolderIsTakenOnlyWhenItHoldsTheGame()
    {
        var settings = new AppSettings();
        string? refused = GameLook.Start(Path.Combine(_home, "nowhere"), settings);
        Assert.Contains("not Valheim's game folder", refused);
        Assert.Null(settings.ValheimPath);
        // Steam's common folder chosen, quoted as a file manager copies it: the game inside it is taken and kept.
        string valheim = FakeValheim(Path.Combine(_home, "common", "Valheim"));
        Assert.Null(GameLook.Start($"  \"{Path.Combine(_home, "common")}\" ", settings));
        Assert.Equal(valheim, settings.ValheimPath);
        Assert.Equal(valheim, AppSettings.Load().ValheimPath);
        Assert.Equal(valheim, GameLook.ValheimPath);
        Assert.NotNull(GameLook.Bundles);
        // Its files are read in the background: this pretend game has none, so it ends failed.
        Assert.Equal("failed", await Settled());
        Assert.NotNull(GameLook.Now().Message);
        Assert.Null(GameLook.Now().Progress);
    }

    [Fact]
    public async Task TheStateGoesFromMissingToReadyOnceTheGamesFilesAreRead()
    {
        GameLook.Check(new AppSettings());
        Assert.Equal("missing", GameLook.State);
        Assert.Null(GameLook.Bundles);
        Assert.Null(GameLook.ValheimPath);
        Assert.Equal("missing", GameLook.Now().State);
        var settings = new AppSettings { ValheimPath = FakeValheim(Path.Combine(_home, "Valheim")) };
        GameLook.Check(settings);
        var bundles = GameLook.Bundles;
        Assert.NotNull(bundles);
        Assert.Equal("failed", await Settled());
        // Checked again (the game's files now read, even if they hold no model): the same reader, ready at once.
        GameLook.Check(settings);
        Assert.Same(bundles, GameLook.Bundles);
        Assert.Equal("ready", GameLook.State);
        Assert.Null(GameLook.Message);
        Assert.Equal(1, GameLook.Now().Progress);
    }

    [Fact]
    public void TheCopyOlderEditorsMadeIsDeleted()
    {
        Directory.CreateDirectory(Path.Combine(GameLook.Dir, "models"));
        File.WriteAllText(Path.Combine(GameLook.Dir, "models", "a.bin"), "old");
        string oldCache = Path.Combine(AppSettings.UserDataDir, "export-cache");
        Directory.CreateDirectory(oldCache);
        GameLook.DeleteOldCopy();
        Assert.False(Directory.Exists(GameLook.Dir));
        Assert.False(Directory.Exists(oldCache));
        // Nothing left to delete: nothing happens.
        GameLook.DeleteOldCopy();
    }

    [Fact]
    public void AFolderNameThatCannotBeAPathIsNotTheGames() => Assert.Null(GameLook.GameFolder("bad\0name"));
}
