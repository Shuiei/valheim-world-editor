using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The data names the player's mods keep on objects (ModKeys): found among the string literals of the DLLs in the
/// settings' plugins folders, given to ZdoKeys (the game's own names still first, a ZDOID's "_u" and "_i" too), kept
/// in the data folder until a DLL changes. Runs alone, in a pretend home (the data folder's environment variables point
/// at a temporary folder, put back afterwards), without the player's own Valheim and profiles.
/// </summary>
[Collection("DataDir")]
public sealed class ModKeysTests : IDisposable
{
    private static readonly string[] Vars = ["HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA"];
    private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    private readonly string _home = Path.Combine(Path.GetTempPath(), "vwe-modkeys-" + Guid.NewGuid().ToString("N")[..8]);

    public ModKeysTests()
    {
        Directory.CreateDirectory(_home);
        Environment.SetEnvironmentVariable("HOME", _home);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(_home, ".config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(_home, ".local", "share"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(_home, "AppData", "Local"));
        // Only the folders a test gives.
        LocalGame.SearchDefaultPlaces = false;
    }

    /// <summary>
    /// Forgets the mods' names, puts the environment and the default places' search back, and deletes the pretend home.
    /// </summary>
    public void Dispose()
    {
        ZdoKeys.UseMods(new Dictionary<int, string>());
        foreach (var (k, v) in _old)
        {
            Environment.SetEnvironmentVariable(k, v);
        }
        LocalGame.SearchDefaultPlaces = _defaults;
        try
        {
            Directory.Delete(_home, true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// A profile's BepInEx plugins folder in the pretend home (made).
    /// </summary>
    private string Plugins(string profile)
    {
        string plugins = Path.Combine(_home, profile, "BepInEx", "plugins");
        Directory.CreateDirectory(plugins);
        return plugins;
    }

    /// <summary>
    /// Settings that name these profiles' BepInEx folders.
    /// </summary>
    private AppSettings Settings(params string[] profiles) => new() { BepInExFolders = [.. profiles.Select(p => Path.Combine(_home, p, "BepInEx"))] };

    /// <summary>The kept file.</summary>
    private static string Kept => Path.Combine(AppSettings.UserDataDir, "mod-zdo-keys.json");

    [Fact]
    public void TheModsDataNamesNameTheirKeys()
    {
        string plugins = Plugins("profile");
        Directory.CreateDirectory(Path.Combine(plugins, "zolantris-ValheimRAFT"));
        TestDll.Write(Path.Combine(plugins, "zolantris-ValheimRAFT", "ValheimVehicles.dll"), strings: ["MBParentId", "MBPosition", "not a key!", "creator"]);
        TestDll.Write(Path.Combine(plugins, "OtherMod.dll"), strings: ["OM_Level"]);

        int names = ModKeys.Refresh(Settings("profile"));

        Assert.Equal(12, names);
        Assert.Equal("MBParentId", ZdoKeys.NameOf(StableHash.Of("MBParentId")));
        Assert.Equal("MBParentId_u", ZdoKeys.NameOf(StableHash.Of("MBParentId_u")));
        Assert.Equal("MBPosition_i", ZdoKeys.NameOf(StableHash.Of("MBPosition_i")));
        Assert.Equal("OM_Level", ZdoKeys.NameOf(StableHash.Of("OM_Level")));
        Assert.Null(ZdoKeys.NameOf(StableHash.Of("not a key!")));
    }

    [Fact]
    public void TheGamesOwnNameComesFirst()
    {
        int creator = StableHash.Of("creator");
        string? game = ZdoKeys.NameOf(creator);
        Assert.NotNull(game);

        ZdoKeys.UseMods(new Dictionary<int, string> { [creator] = "a mod's name" });

        Assert.Equal(game, ZdoKeys.NameOf(creator));
    }

    [Fact]
    public void FilesThatAreNotDllsOfCodeGiveNoNames()
    {
        string plugins = Plugins("profile");
        File.WriteAllText(Path.Combine(plugins, "Broken.dll"), "MBParentId");
        File.WriteAllText(Path.Combine(plugins, "readme.txt"), "MBParentId");

        Assert.Equal(0, ModKeys.Refresh(Settings("profile")));
        Assert.Equal(0, ModKeys.Refresh(Settings("missing")));
        Assert.Null(ZdoKeys.NameOf(StableHash.Of("MBParentId")));
    }

    [Fact]
    public void TheNamesAreKeptWhileTheDllsAreUnchanged()
    {
        string plugins = Plugins("profile");
        string dll = Path.Combine(plugins, "Mod.dll");
        TestDll.Write(dll, strings: ["MBParentId"]);
        ModKeys.Refresh(Settings("profile"));
        // What is kept is used: a name changed in the file shows.
        var kept = JsonNode.Parse(File.ReadAllText(Kept))!;
        kept["Names"]![StableHash.Of("MBParentId").ToString(System.Globalization.CultureInfo.InvariantCulture)] = "from the kept file";
        File.WriteAllText(Kept, kept.ToJsonString());

        ModKeys.Refresh(Settings("profile"));
        Assert.Equal("from the kept file", ZdoKeys.NameOf(StableHash.Of("MBParentId")));

        // A DLL changed: read again.
        File.SetLastWriteTimeUtc(dll, DateTime.UtcNow.AddMinutes(1));
        ModKeys.Refresh(Settings("profile"));
        Assert.Equal("MBParentId", ZdoKeys.NameOf(StableHash.Of("MBParentId")));

        // A broken kept file: read again.
        File.WriteAllText(Kept, "{ broken");
        Assert.Equal(3, ModKeys.Refresh(Settings("profile")));
    }
}
