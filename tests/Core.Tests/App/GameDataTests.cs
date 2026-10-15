using System.Text.Json;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game data's caches and gate on a pretend game (a game folder with no bundles, and a .NET assembly in place of
/// the game's code) and a pretend home (the XDG folders point at a temporary folder, put back afterwards): each kind
/// from its cache while the game and the editor are unchanged, read again and kept when its cache is out of date,
/// none when the game cannot be read; Ensure's answers; --export-test-data's checks and how it trims the copies.
/// The tests' copies are in use again afterwards.
/// </summary>
[Collection("DataDir")]
public sealed class GameDataTests : IDisposable
{
    private static readonly string[] Vars = ["HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA"];
    private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
    private readonly string? _settings = AppSettings.PathOverride, _dataDir = AppSettings.DataDirOverride;
    private readonly bool _outside = GameLook.SearchOutsideHome;
    private readonly string _home = Path.Combine(Path.GetTempPath(), "vwe-gamedata-" + Guid.NewGuid().ToString("N")[..8]);

    public GameDataTests()
    {
        Directory.CreateDirectory(_home);
        Environment.SetEnvironmentVariable("HOME", _home);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(_home, ".config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(_home, ".local", "share"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(_home, "AppData", "Local"));
        AppSettings.PathOverride = Path.Combine(_home, "settings.json");
        AppSettings.DataDirOverride = null;
        // Only the pretend home is searched for Steam.
        GameLook.SearchOutsideHome = false;
    }

    /// <summary>
    /// Puts the environment, the overridden paths and the tests' copies back, and deletes the pretend home.
    /// </summary>
    public void Dispose()
    {
        GameData.UseCopies(Fixtures.Root);
        foreach (var (k, v) in _old)
        {
            Environment.SetEnvironmentVariable(k, v);
        }
        AppSettings.PathOverride = _settings;
        AppSettings.DataDirOverride = _dataDir;
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
    /// A pretend game folder in the home: its (empty) bundles folder, and with code, a .NET assembly where the game's
    /// code is (its strings are what the zdo-keys kind reads). With mac, laid out as on a Mac: its data folder inside
    /// valheim.app.
    /// </summary>
    private (string Valheim, GameBundles Bundles) Game(string name, bool code, bool mac = false)
    {
        string valheim = Path.Combine(_home, name);
        string data = mac ? Path.Combine(valheim, "valheim.app", "Contents", "Resources", "Data") : Path.Combine(valheim, "valheim_Data");
        string bundles = Path.Combine(data, "StreamingAssets", "SoftRef", "Bundles");
        Directory.CreateDirectory(bundles);
        // Named after the game: the caches' stamp tells games apart by their files.
        File.WriteAllText(Path.Combine(bundles, name + "_bundle"), "not read");
        if (code)
        {
            string managed = Path.Combine(data, "Managed");
            Directory.CreateDirectory(managed);
            File.Copy(typeof(GameData).Assembly.Location, Path.Combine(managed, "assembly_valheim.dll"));
        }
        return (valheim, new GameBundles(bundles, null));
    }

    /// <summary>
    /// The tests' copy of a kind.
    /// </summary>
    private static string Copy(string kind) => File.ReadAllText(Path.Combine(Fixtures.Root, kind + ".json"));

    /// <summary>
    /// Writes each kind's cache for this game (the tests' copy as its data), but those left out.
    /// </summary>
    private static void Caches(string valheim, GameBundles bundles, params string[] leftOut)
    {
        string stamp = GameData.Stamp(valheim, bundles.Folder);
        foreach (string kind in GameData.KindNames.Except(leftOut))
        {
            string file = GameData.CacheFile(kind);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, kind + " " + stamp + "\n" + Copy(kind));
        }
    }

    [Fact]
    public void EveryKindComesFromItsCacheWhileTheGameIsUnchanged()
    {
        var (valheim, bundles) = Game("Valheim", code: false);
        Caches(valheim, bundles);
        var read = GameData.Load(bundles, cache: true)!;
        Assert.Equal(GameData.KindNames.Order(), read.Keys.Order());
        Assert.All(GameData.KindNames, k => Assert.Equal(Copy(k), read[k]));
        // The caches live in the user's data folder.
        Assert.StartsWith(Path.Combine(_home, ".local", "share"), GameData.CacheFile("prefabs"));
    }

    [Fact]
    public void AKindWhoseCacheIsOutOfDateIsReadAgainAndKept()
    {
        var (valheim, bundles) = Game("Valheim", code: true);
        Caches(valheim, bundles);
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
        // A cache that cannot be written (a folder in its place): nothing is had.
        File.Delete(file);
        Directory.CreateDirectory(file);
        Assert.Null(GameData.Load(bundles, cache: true));
    }

    [Fact]
    public void AMacGamesCodeIsReadFromItsAppBundle()
    {
        var (valheim, bundles) = Game("Valheim", code: true, mac: true);
        Caches(valheim, bundles, "zdo-keys");
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
        var (valheim, bundles) = Game("Valheim", code: false);
        Caches(valheim, bundles, "zdo-keys");
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
        var (valheim, bundles) = Game("Valheim", code: false);
        Caches(valheim, bundles);
        var (_, unreadable) = Game("Other", code: false);
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
    public void ExportNeedsTheCopiesFolderTheGameAndEveryCopy()
    {
        Assert.Equal(2, GameData.Export(null));
        Assert.Equal(2, GameData.Export(Path.Combine(_home, "no-such-folder")));
        string copies = Path.Combine(_home, "fixtures");
        Directory.CreateDirectory(copies);
        // No Valheim in the pretend home, nor set in the settings.
        Assert.Equal(1, GameData.Export(copies));
        var (valheim, _) = Game("Valheim", code: false);
        new AppSettings { ValheimPath = valheim }.Save();
        // A copy missing.
        Assert.Equal(1, GameData.Export(copies));
        foreach (string kind in GameData.KindNames)
        {
            File.WriteAllText(Path.Combine(copies, kind + ".json"), Copy(kind));
        }
        // The game's data cannot be read (no bundles, no code): the copies are left as they were.
        Assert.Equal(1, GameData.Export(copies));
        Assert.All(GameData.KindNames, k => Assert.Equal(Copy(k), File.ReadAllText(Path.Combine(copies, k + ".json"))));
    }

    [Fact]
    public void ACopyKeepsTheGamesEntriesItHad()
    {
        var (text, kept, now) = GameData.Trim("terrain-modifiers", """{"a":[1,2],"b":[3],"c":5,"d":6,"e":7}""", """{"a":0,"c":0,"x":0}""");
        // One entry a line, each written compact.
        Assert.Equal("{\n  \"a\": [1,2],\n  \"c\": 5\n}\n", text);
        Assert.Equal(["a", "c", "x"], kept);
        Assert.Equal(["a", "c"], now);
        // A copy with no entries stays empty.
        Assert.Equal("{}\n", GameData.Trim("prefabs", """{"a":1}""", "{}").Text);
    }

    [Fact]
    public void APieceCostCopyKeepsItsPiecesAndTheItemsTheyTake()
    {
        string game = """
            {"items":{"Wood":"Wood","Stone":"Stone","Flint":"Flint"},"pieces":{"wall":{"n":"Wall","r":[["Wood",2]]},
            "floor":{"n":"Floor","r":[["Wood",1],["Stone",1]]},"beam":{"n":"Beam","r":[["Wood",1]]},"pole":{"n":"Pole","r":[["Wood",1]]},
            "door":{"n":"Door","r":[["Wood",4]]},"gone":{"n":"Gone","r":[["Flint",1]]}}}
            """;
        string copy = """{"items":{"Wood":"Wood"},"pieces":{"wall":{},"floor":{},"beam":{},"pole":{},"door":{},"old":{}}}""";
        var (text, kept, now) = GameData.Trim("piece-cost", game, copy);
        // Its two groups opened, one entry a line.
        Assert.Equal("""
            {
              "items": {
                "Wood": "Wood",
                "Stone": "Stone"
              },
              "pieces": {
                "wall": {"n":"Wall","r":[["Wood",2]]},
                "floor": {"n":"Floor","r":[["Wood",1],["Stone",1]]},
                "beam": {"n":"Beam","r":[["Wood",1]]},
                "pole": {"n":"Pole","r":[["Wood",1]]},
                "door": {"n":"Door","r":[["Wood",4]]}
              }
            }

            """.ReplaceLineEndings("\n"), text);
        Assert.Equal(["items/Wood", "pieces/wall", "pieces/floor", "pieces/beam", "pieces/pole", "pieces/door", "pieces/old"], kept);
        Assert.Equal(["items/Wood", "items/Stone", "pieces/wall", "pieces/floor", "pieces/beam", "pieces/pole", "pieces/door"], now);
    }

    [Fact]
    public void ADungeonCopyKeepsEveryDungeonAndItsRooms()
    {
        string game = """{"dungeons":{"Crypt":{"k":1},"Cave":{"k":2}},"rooms":{"r1":{"s":1},"r2":{"s":2}}}""";
        var (text, kept, now) = GameData.Trim("dungeon-rooms", game, """{"dungeons":{},"rooms":{"r2":{}}}""");
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(2, doc.RootElement.GetProperty("dungeons").EnumerateObject().Count());
        Assert.Equal(["r2"], doc.RootElement.GetProperty("rooms").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["rooms/r2"], kept);
        Assert.Equal(["dungeons/Crypt", "dungeons/Cave", "rooms/r2"], now);
    }

    [Fact]
    public void AVegetationCopyKeepsTheRulesItHadByNameAndPrefab()
    {
        string game = """[{"name":"A","prefab":"p1","x":1},{"name":"A","prefab":"p2"},{"name":"B","prefab":"p1"}]""";
        var (text, kept, now) = GameData.Trim("vegetation", game, """[{"name":"A","prefab":"p1"},{"name":"C","prefab":"p9"}]""");
        Assert.Equal("[\n  {\"name\":\"A\",\"prefab\":\"p1\",\"x\":1}\n]\n", text);
        Assert.Equal(["A/p1", "C/p9"], kept);
        Assert.Equal(["A/p1"], now);
        Assert.Equal("[]\n", GameData.Trim("vegetation", game, "[]").Text);
    }
}
