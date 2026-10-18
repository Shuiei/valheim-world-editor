using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The set-up the AppSettings tests share (AppSettingsFileTests, SavedServersTests, SettingsDiscoveryTests): the app's data
/// folder pointed at a temporary one (XDG_DATA_HOME, LOCALAPPDATA, and their own servers file), put back and deleted after.
/// </summary>
public abstract class AppSettingsTestBase : IDisposable
{
    /// <summary>The temporary data folder the tests use.</summary>
    protected string Data { get; } = Path.Combine(Path.GetTempPath(), "vwe-data-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string? _old = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

    /// <summary>
    /// Points the data folder and the servers file at this test's own temporary folder.
    /// </summary>
    protected AppSettingsTestBase()
    {
        Directory.CreateDirectory(Data);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Data);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Data);
        // The native tests send saved servers to a test file: this test gets its own, in its folder.
        _oldServers = ServerConfig.PathOverride;
        ServerConfig.PathOverride = Path.Combine(Data, "servers.cfg");
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
        if (Directory.Exists(Data))
        {
            Directory.Delete(Data, true);
        }
        GC.SuppressFinalize(this);
    }
}
