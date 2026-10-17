using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The set-up the mod tests share (ModBundlesTests, ModKeysTests): a pretend home with the environment variables pointed at
/// it and only the folders a test gives searched, the profiles' BepInEx folders made in it, and all of it put back and
/// deleted after.
/// </summary>
public abstract class PretendHomeTestBase : IDisposable
{
    private static readonly string[] Vars = ["HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA"];
    private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    /// <summary>The pretend home: a folder of its own under the temporary folder.</summary>
    protected string Home { get; } = Path.Combine(Path.GetTempPath(), "vwe-pretend-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Points the environment at the pretend home (made) and turns off the default places' search.
    /// </summary>
    protected PretendHomeTestBase()
    {
        Directory.CreateDirectory(Home);
        Environment.SetEnvironmentVariable("HOME", Home);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(Home, ".config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(Home, ".local", "share"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(Home, "AppData", "Local"));
        // Only the folders a test gives.
        LocalGame.SearchDefaultPlaces = false;
    }

    /// <summary>
    /// Forgets the mods, puts the environment and the default places' search back, and deletes the pretend home.
    /// </summary>
    public void Dispose()
    {
        ForgetMods();
        foreach (var (k, v) in _old)
        {
            Environment.SetEnvironmentVariable(k, v);
        }
        LocalGame.SearchDefaultPlaces = _defaults;
        if (Directory.Exists(Home))
        {
            Directory.Delete(Home, true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Forgets what the test's mods added to the editor (none here); a test's own override runs before the environment
    /// is put back.
    /// </summary>
    protected virtual void ForgetMods()
    {
    }

    /// <summary>
    /// A profile's BepInEx plugins folder in the pretend home (made).
    /// </summary>
    protected string Plugins(string profile)
    {
        string plugins = Path.Combine(Home, profile, "BepInEx", "plugins");
        Directory.CreateDirectory(plugins);
        return plugins;
    }

    /// <summary>
    /// Settings that name these profiles' BepInEx folders.
    /// </summary>
    protected AppSettings Settings(params string[] profiles) => new() { BepInExFolders = [.. profiles.Select(p => Path.Combine(Home, p, "BepInEx"))] };
}
