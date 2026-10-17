using System.Diagnostics;
using System.Runtime.Versioning;
using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The pretend home shared by the HomeFolder test classes: HOME and the XDG folders point at a temporary folder, put
/// back afterwards. Nothing here reads the real home, ~/.ssh or the real settings.
/// </summary>
public abstract class HomeFolderTestBase : IDisposable
{
    private static readonly string[] Vars = ["HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA"];
    private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
    private readonly string? _settings = AppSettings.PathOverride, _servers = ServerConfig.PathOverride, _dataDir = AppSettings.DataDirOverride;
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
    protected string Home { get; } = Path.Combine(Path.GetTempPath(), "vwe-home-" + Guid.NewGuid().ToString("N")[..8]);

    protected HomeFolderTestBase()
    {
        Directory.CreateDirectory(Home);
        Environment.SetEnvironmentVariable("HOME", Home);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(Home, ".config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(Home, ".local", "share"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", null);
        AppSettings.PathOverride = null;
        AppSettings.DataDirOverride = null;
        ServerConfig.PathOverride = Path.Combine(Home, "servers.cfg");
        // Only the folders a test sets up (Steam's own folders outside the home are never looked at).
        LocalGame.SearchDefaultPlaces = false;
        TestKey = MakeKey(Local("testkey"), "");
        // Protected by the passphrase "secret".
        LockedKey = MakeKey(Local("lockedkey"), "secret");
    }

    /// <summary>
    /// OpenSSH's ssh-keygen where the systems keep it (by its full path, as LocalSshd finds it: not whatever the PATH
    /// names first).
    /// </summary>
    private static readonly string KeyGen = new[]
    {
        "/usr/bin/ssh-keygen", "/usr/sbin/ssh-keygen", "/bin/ssh-keygen",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh-keygen.exe"),
    }.FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("OpenSSH's ssh-keygen was not found: the SSH key tests need it.");

    /// <summary>
    /// Makes a throwaway ed25519 key at <paramref name="file"/> with ssh-keygen (as LocalSshd does) and returns its text.
    /// </summary>
    /// <exception cref="InvalidOperationException">ssh-keygen failed (said with its exit code).</exception>
    protected static string MakeKey(string file, string passphrase)
    {
        var start = new ProcessStartInfo(KeyGen, ["-q", "-t", "ed25519", "-N", passphrase, "-C", "vwe-test", "-f", file]) { UseShellExecute = false };
        // Not a command injection (the scanners' rule is wrong here): ssh-keygen by its full path, its arguments passed
        // one by one with no shell, all made by the test (a name in its own temporary folder and a fixed passphrase).
        using var p = Process.Start(start) ?? throw new InvalidOperationException("ssh-keygen did not start"); // nosemgrep
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"ssh-keygen failed with exit code {p.ExitCode}");
        }
        return File.ReadAllText(file);
    }

    /// <summary>
    /// A folder that looks like the game's (for the tests that look in the default places).
    /// </summary>
    protected string FakeValheim()
    {
        string valheim = Local("Steam", "Valheim");
        Directory.CreateDirectory(Path.Combine(valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
        return valheim;
    }

    /// <summary>
    /// Puts the environment variables, overridden paths and default-places search back, and deletes the pretend home.
    /// </summary>
    public void Dispose()
    {
        foreach (var (k, v) in _old)
        {
            Environment.SetEnvironmentVariable(k, v);
        }
        AppSettings.PathOverride = _settings;
        AppSettings.DataDirOverride = _dataDir;
        ServerConfig.PathOverride = _servers;
        LocalGame.SearchDefaultPlaces = _defaults;
        if (Directory.Exists(Home))
        {
            Directory.Delete(Home, true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A path inside the pretend home.
    /// </summary>
    protected string Local(params string[] parts) => Path.Combine([Home, .. parts]);

    /// <summary>
    /// Copies the test world into this folder.
    /// </summary>
    protected static void CopyWorld(string to)
    {
        using var w = new TempWorld();
        TempWorld.CopyDir(w.Dir, to);
    }

    // Throwaway keys, made with ssh-keygen when a test starts, so no private key is kept in the repo.
    protected string TestKey { get; }
    protected string LockedKey { get; }

    /// <summary>
    /// Puts the WorldEditorBridge plugin in a BepInEx folder, with its config (port and token) when a port is given.
    /// </summary>
    protected static void Plugin(string bepInEx, int? port = null, string? token = null)
    {
        Directory.CreateDirectory(Path.Combine(bepInEx, "plugins", "Tie-WorldEditorBridge"));
        File.WriteAllText(Path.Combine(bepInEx, "plugins", "Tie-WorldEditorBridge", "WorldEditorBridge.dll"), "");
        if (port != null)
        {
            Directory.CreateDirectory(Path.Combine(bepInEx, "config"));
            File.WriteAllText(Path.Combine(bepInEx, "config", LocalGame.ConfigName), $"[Http]\nPort = {port}\nToken = {token}\n");
        }
    }

    /// <summary>
    /// Gives the owner back read, write and search access to a folder the test locked.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    protected static void RestoreAccess(string path) => File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
