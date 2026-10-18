using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Looking for Valheim outside the home too (GameLook.SearchOutsideHome, as the editor does): Steam's system-wide folders
/// are searched as well, and only then. The system folders are stood in with ones in the pretend home, and put back.
/// </summary>
[Collection("DataDir")]
public sealed class GameLookOutsideHomeTests : IDisposable
{
    private readonly GameHome _sandbox = new("vwe-outside-home-");
    private readonly IReadOnlyList<string> _systemRoots = GameLook.SystemSteamRoots;

    /// <summary>Puts the system folders, the environment, the search and the settings back, and deletes the pretend home.</summary>
    public void Dispose()
    {
        GameLook.SystemSteamRoots = _systemRoots;
        _sandbox.Dispose();
    }

    [Fact]
    public void TheSystemsSteamFoldersAreSearchedOnlyOutsideTheHome()
    {
        if (!OperatingSystem.IsLinux())
        {
            // The system-wide folders are Linux's; Windows has Program Files, with no stand-in.
            return;
        }
        // Valheim in a pretend system-wide Steam, next to one that does not exist (as /opt/Steam mostly does not).
        string steam = Path.Combine(_sandbox.Folder, "opt", "Steam");
        string valheim = Path.Combine(steam, "steamapps", "common", "Valheim");
        Directory.CreateDirectory(Path.Combine(valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
        GameLook.SystemSteamRoots = [Path.Combine(_sandbox.Folder, "usr", "share", "steam"), steam];

        // Within the home only (as the sandbox sets it): not found.
        Assert.Null(GameLook.FindValheim(null));

        GameLook.SearchOutsideHome = true;
        Assert.Equal(valheim, GameLook.FindValheim(null));
    }
}
