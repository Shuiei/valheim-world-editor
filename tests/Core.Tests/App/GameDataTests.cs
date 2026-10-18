using System.Text.Json;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game data's caches on a pretend game (a game folder with no bundles, and a .NET assembly in place of the
/// game's code) and a pretend home (see GameHome): each kind from its cache while the game and the editor are
/// unchanged, read again and kept when its cache is out of date, none when the game cannot be read, and Ensure's
/// answers. The tests' copies are in use again afterwards.
/// </summary>
[Collection("DataDir")]
public sealed class GameDataTests : IDisposable
{
    private readonly GameHome _sandbox = new("vwe-gamedata-");

    /// <summary>
    /// Puts the tests' copies back, then the pretend home (its environment, settings and folder).
    /// </summary>
    public void Dispose()
    {
        GameData.UseCopies(Fixtures.Root);
        _sandbox.Dispose();
    }

    [Fact]
    public void EveryKindComesFromItsCacheWhileTheGameIsUnchanged()
    {
        var (valheim, bundles) = _sandbox.Game("Valheim", code: false);
        GameHome.Caches(valheim, bundles);
        var read = GameData.Load(bundles, cache: true)!;
        Assert.Equal(GameData.KindNames.Order(), read.Keys.Order());
        Assert.All(GameData.KindNames, k => Assert.Equal(GameHome.Copy(k), read[k]));
        // The caches live in the user's data folder.
        Assert.StartsWith(Path.Combine(_sandbox.Folder, ".local", "share"), GameData.CacheFile("prefabs"));
    }

    [Fact]
    public void AKindWhoseCacheIsOutOfDateIsReadAgainAndKept()
    {
        var (valheim, bundles) = _sandbox.Game("Valheim", code: true);
        GameHome.Caches(valheim, bundles);
        string file = GameData.CacheFile("zdo-keys");
        // Written for another game (or editor build): read again from the game's code.
        File.WriteAllText(file, "zdo-keys another-game\n{}");
        var read = GameData.Load(bundles, cache: true)!;
        string keys = read["zdo-keys"];
        Assert.Equal(GameCatalogues.ZdoKeysJson(valheim), keys);
        using (var doc = JsonDocument.Parse(keys))
        {
            Assert.True(doc.RootElement.EnumerateObject().Any());
        }
        // Kept, for this game: the next start takes it from the cache.
        string stamp = GameData.Stamp(valheim, bundles.Folder);
        Assert.Equal("zdo-keys " + stamp + "\n" + keys, File.ReadAllText(file));
        Assert.Equal(keys, GameData.Load(bundles, cache: true)!["zdo-keys"]);
        // A cache that cannot be written (a folder in its place): what was read is had all the same.
        File.Delete(file);
        Directory.CreateDirectory(file);
        Assert.Equal(keys, GameData.Load(bundles, cache: true)?["zdo-keys"]);
    }

    [Fact]
    public void ACacheThatCannotBeWrittenIsOnlyLogged()
    {
        string file = GameData.CacheFile("prefabs");
        Assert.True(GameData.KeepCache(file, "kept"));
        Assert.Equal("kept", File.ReadAllText(file));
        // A folder in its place: not kept, nothing thrown, and no half-written file left.
        File.Delete(file);
        Directory.CreateDirectory(file);
        Assert.False(GameData.KeepCache(file, "lost"));
        Assert.True(Directory.Exists(file));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, Path.GetFileName(file) + ".*.tmp"));
    }

    [Fact]
    public void AMacGamesCodeIsReadFromItsAppBundle()
    {
        var (valheim, bundles) = _sandbox.Game("Valheim", code: true, mac: true);
        GameHome.Caches(valheim, bundles, "zdo-keys");
        // Its game folder found from the bundles inside valheim.app, and the game's code in the bundle's Managed.
        Assert.Contains("assembly_valheim.dll", GameData.Stamp(valheim, bundles.Folder));
        string keys = GameData.Load(bundles, cache: true)!["zdo-keys"];
        Assert.Equal(GameCatalogues.ZdoKeysJson(valheim), keys);
        using var doc = JsonDocument.Parse(keys);
        Assert.True(doc.RootElement.EnumerateObject().Any());
    }

    [Fact]
    public void NothingIsHadWhenTheGameCannotBeRead()
    {
        // No game code: the zdo-keys kind cannot be read, so none is had (no world would open).
        var (valheim, bundles) = _sandbox.Game("Valheim", code: false);
        GameHome.Caches(valheim, bundles, "zdo-keys");
        Assert.Null(GameData.Load(bundles, cache: true));
        // The others' caches are left as they were.
        Assert.False(File.Exists(GameData.CacheFile("zdo-keys")));
        Assert.True(File.Exists(GameData.CacheFile("prefabs")));
    }

    [Fact]
    public void EnsureHasTheDataOnlyForAGameItCanRead()
    {
        // The tests' copies count as had, with or without a game.
        Assert.True(GameData.Ensure(null));
        var (valheim, bundles) = _sandbox.Game("Valheim", code: false);
        GameHome.Caches(valheim, bundles);
        var (_, unreadable) = _sandbox.Game("Other", code: false);
        try
        {
            GameData.Forget();
            // No game: nothing can be had.
            Assert.False(GameData.Ensure(null));
            // A game without its code read nor cached: not had either.
            Assert.False(GameData.Ensure(unreadable));
            // From the caches; asked again for the same game, nothing more to do.
            Assert.True(GameData.Ensure(bundles));
            File.Delete(GameData.CacheFile("prefabs"));
            Assert.True(GameData.Ensure(bundles));
            Assert.NotNull(TerrainEditor.Editing.Hammer.Get("woodwall"));
        }
        finally
        {
            GameData.UseCopies(Fixtures.Root);
        }
    }

    [Fact]
    public void AKindTheEditorCannotTakeLeavesNoneHad()
    {
        var (valheim, bundles) = _sandbox.Game("Valheim", code: false);
        GameHome.Caches(valheim, bundles);
        string stamp = GameData.Stamp(valheim, bundles.Folder);
        string file = GameData.CacheFile("zdo-keys");
        // Its keys are hashes as text: one that is not throws as it is taken, after the kinds before it.
        File.WriteAllText(file, "zdo-keys " + stamp + "\n{\"not a hash\": \"x\"}");
        GameData.Forget();
        Assert.False(GameData.Ensure(bundles));
        // Not counted as had: asked again, it is tried again.
        Assert.False(GameData.Ensure(bundles));
        File.WriteAllText(file, "zdo-keys " + stamp + "\n" + GameHome.Copy("zdo-keys"));
        Assert.True(GameData.Ensure(bundles));
    }
}
