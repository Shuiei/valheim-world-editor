using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The pretend home the game-data and game-look tests share: a temporary folder that HOME and the XDG and LocalAppData
/// folders point at, with the settings file in it, only that folder searched for Steam, and a pretend game made in it
/// when a test asks. Put back and deleted on Dispose, which the owning test class calls after its own clean-up.
/// </summary>
public sealed class GameHome : IDisposable
{
    private static readonly string[] Vars = ["HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA"];
    private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
    private readonly string? _settings = AppSettings.PathOverride, _dataDir = AppSettings.DataDirOverride;
    private readonly bool _outside = GameLook.SearchOutsideHome;

    /// <summary>
    /// Makes the pretend home under the temporary folder, points the environment and the settings at it, and turns
    /// off the search outside it.
    /// </summary>
    /// <param name="prefix">The start of the home's folder name, so a test class's homes can be told apart.</param>
    public GameHome(string prefix)
    {
        Folder = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Folder);
        Environment.SetEnvironmentVariable("HOME", Folder);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(Folder, ".config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(Folder, ".local", "share"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(Folder, "AppData", "Local"));
        AppSettings.PathOverride = Path.Combine(Folder, "settings.json");
        AppSettings.DataDirOverride = null;
        // Only the pretend home is searched for Steam.
        GameLook.SearchOutsideHome = false;
    }

    /// <summary>The pretend home's folder (made).</summary>
    public string Folder { get; }

    /// <summary>
    /// Puts the environment, the overridden paths and the search back, and deletes the pretend home.
    /// </summary>
    public void Dispose()
    {
        foreach (var (k, v) in _old)
        {
            Environment.SetEnvironmentVariable(k, v);
        }
        AppSettings.PathOverride = _settings;
        AppSettings.DataDirOverride = _dataDir;
        GameLook.SearchOutsideHome = _outside;
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, true);
        }
    }

    /// <summary>
    /// A pretend game folder in the home: its (empty) bundles folder, and with code, a .NET assembly where the game's
    /// code is (its strings are what the zdo-keys kind reads). With mac, laid out as on a Mac: its data folder inside
    /// valheim.app.
    /// </summary>
    public (string Valheim, GameBundles Bundles) Game(string name, bool code, bool mac = false)
    {
        string valheim = Path.Combine(Folder, name);
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
    public static string Copy(string kind) => File.ReadAllText(Path.Combine(Fixtures.Root, kind + ".json"));

    /// <summary>
    /// Writes each kind's cache for this game (the tests' copy as its data), but those left out.
    /// </summary>
    public static void Caches(string valheim, GameBundles bundles, params string[] leftOut)
    {
        string stamp = GameData.Stamp(valheim, bundles.Folder);
        foreach (string kind in GameData.KindNames.Except(leftOut))
        {
            string file = GameData.CacheFile(kind);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, kind + " " + stamp + "\n" + Copy(kind));
        }
    }
}
