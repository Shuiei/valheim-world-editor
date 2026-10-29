namespace WorldEditor.Tests;

/// <summary>
/// The files and folders Core keeps, pointed at the tests' own in both test projects.
/// </summary>
public static class TestFiles
{
    /// <summary>
    /// Before any test runs: the user's settings, servers, game folders and data folder are never the tests'.
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void KeepTheUsersFiles()
    {
        TerrainEditor.App.AppSettings.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-settings-{Environment.ProcessId}.json");
        TerrainEditor.App.ServerConfig.PathOverride = Path.Combine(Path.GetTempPath(), $"vwe-servers-{Environment.ProcessId}.cfg");
        // "My game" looks only where a test says, never in the player's own Valheim or profiles.
        TerrainEditor.App.LocalGame.SearchDefaultPlaces = false;
        // Never the game's own Homestead folder (a test run once left blueprints in it).
        TerrainEditor.App.Homestead.FolderOverride ??= Path.Combine(Path.GetTempPath(), $"vwe-homestead-{Environment.ProcessId}");
        // Nor Steam outside the home (the computer's own Valheim, its plugin's settings).
        TerrainEditor.App.GameLook.SearchOutsideHome = false;
        // Blueprints, heightmaps, logs: a data folder of the tests', never the user's.
        TerrainEditor.App.AppSettings.DataDirOverride = Path.Combine(Path.GetTempPath(), $"vwe-data-{Environment.ProcessId}");
    }
}
