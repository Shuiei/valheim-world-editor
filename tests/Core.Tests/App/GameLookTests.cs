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
    private readonly GameHome _sandbox = new("vwe-gamelook-");

    /// <summary>
    /// Leaves the game missing again (no pretend game kept in use), puts the environment and settings back, and
    /// deletes the pretend home. A reading the test started is waited for first, and the home is deleted before the
    /// check: with a pretend game still in it, the check would find it and start reading it in the background, into
    /// the folder being deleted and on into the next test.
    /// </summary>
    public void Dispose()
    {
        Settled().GetAwaiter().GetResult();
        if (Directory.Exists(_sandbox.Folder))
        {
            Directory.Delete(_sandbox.Folder, true);
        }
        GameLook.Check(new AppSettings());
        _sandbox.Dispose();
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
        string valheim = Path.Combine(_sandbox.Folder, "Library", "Application Support", "Steam", "steamapps", "common", "Valheim");
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
        string other = Path.Combine(_sandbox.Folder, "Other");
        Directory.CreateDirectory(Path.Combine(other, "Other.app", "Contents", "Resources", "Data"));
        Assert.Null(GameLook.DataDir(other));
        Assert.Null(GameLook.GameFolder(Path.Combine(other, "Other.app")));
        // On Windows and Linux the data folder's game is the folder above it.
        string linux = FakeValheim(Path.Combine(_sandbox.Folder, "Valheim"));
        Assert.Equal(linux, GameLook.GameOfData(Path.Combine(linux, "valheim_Data")));
    }

    [Fact]
    public void TheGameIsFoundInASteamLibraryListedBySteam()
    {
        Assert.Null(GameLook.FindValheim(null));
        // Steam in the home, with Valheim in a second library it lists (one listed library does not exist, and Steam's
        // own folder is listed too, as Steam does).
        string steam = Path.Combine(_sandbox.Folder, ".steam", "steam");
        string library = Path.Combine(_sandbox.Folder, "Games", "SteamLibrary");
        string valheim = FakeValheim(Path.Combine(library, "steamapps", "common", "Valheim"));
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
                "0" { "path"		"{{steam}}" }
                "1" { "path"		"{{Path.Combine(_sandbox.Folder, "Gone")}}" }
                "2" { "path"		"{{library}}" }
            }
            """);
        Assert.Equal(valheim, GameLook.FindValheim(null));
        // A folder set in the settings comes first, when it is the game's; else the search goes on.
        string chosen = FakeValheim(Path.Combine(_sandbox.Folder, "Valheim"));
        Assert.Equal(chosen, GameLook.FindValheim(chosen));
        Assert.Equal(valheim, GameLook.FindValheim(Path.Combine(_sandbox.Folder, "nowhere")));
    }

    // Steam's library list that cannot be read (being rewritten, no access): the search goes on without it (it threw).
    [Fact]
    public void ASteamLibraryListThatCannotBeReadIsPassedOver()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }
        string vdf = Path.Combine(_sandbox.Folder, ".steam", "steam", "steamapps", "libraryfolders.vdf");
        Directory.CreateDirectory(Path.GetDirectoryName(vdf)!);
        File.WriteAllText(vdf, "\"libraryfolders\" { }");
        File.SetUnixFileMode(vdf, UnixFileMode.None);

        Assert.Null(GameLook.FindValheim(null));
    }

    [Fact]
    public async Task AChosenFolderIsTakenOnlyWhenItHoldsTheGame()
    {
        var settings = new AppSettings();
        string? refused = GameLook.Start(Path.Combine(_sandbox.Folder, "nowhere"), settings);
        Assert.Contains("not Valheim's game folder", refused);
        Assert.Null(settings.ValheimPath);
        // Steam's common folder chosen, quoted as a file manager copies it: the game inside it is taken and kept.
        string valheim = FakeValheim(Path.Combine(_sandbox.Folder, "common", "Valheim"));
        Assert.Null(GameLook.Start($"  \"{Path.Combine(_sandbox.Folder, "common")}\" ", settings));
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
        var settings = new AppSettings { ValheimPath = FakeValheim(Path.Combine(_sandbox.Folder, "Valheim")) };
        GameLook.Check(settings);
        var bundles = GameLook.Bundles;
        Assert.NotNull(bundles);
        Assert.Equal("failed", await Settled());
        // Checked again (the game's files now read, even if they hold no model): the same reader, ready once its
        // game data is checked (from the caches), without reading the files again.
        GameLook.Check(settings);
        Assert.Same(bundles, GameLook.Bundles);
        Assert.Equal("ready", await Settled());
        Assert.Null(GameLook.Message);
        Assert.Equal(1, GameLook.Now().Progress);
    }

    [Fact]
    public async Task AReadGameWhoseDataCannotBeHadEndsFailed()
    {
        var settings = new AppSettings { ValheimPath = FakeValheim(Path.Combine(_sandbox.Folder, "Valheim")) };
        GameLook.Check(settings);
        Assert.Equal("failed", await Settled());
        try
        {
            // Its files read, so not read again; its game data neither in use, cached nor readable (no game code).
            GameData.Forget();
            GameLook.Check(settings);
            Assert.Equal("failed", await Settled());
            Assert.Contains("game's data", GameLook.Message);
        }
        finally
        {
            GameData.UseCopies(Fixtures.Root);
        }
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

    [Fact]
    public void AReadingOfAnotherGameLeavesTheStateAsItIs()
    {
        // Only the game in use takes the reading's state: another game's reading (a folder never set) changes nothing.
        string state = GameLook.State;
        string? message = GameLook.Message;
        using var other = new GameBundles(_sandbox.Folder, null);
        GameLook.FinishReading(other, "failed", "not this game");
        Assert.Equal(state, GameLook.State);
        Assert.Equal(message, GameLook.Message);
    }

    [Fact]
    public void AGameWithNoModelsIsIndexedWithNoEntries()
    {
        // The index of a folder of bundles holding no model: indexed, and empty (the state then says so).
        string valheim = FakeValheim(Path.Combine(_sandbox.Folder, "Valheim"));
        using var game = new GameBundles(GameLook.BundlesDir(valheim)!, null);
        GameLook.IndexBundles(game);
        Assert.True(game.Indexed);
        Assert.Empty(game.Index);
    }
}
