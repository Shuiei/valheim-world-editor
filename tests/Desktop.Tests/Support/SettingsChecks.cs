using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Checks of the settings the Settings dialog keeps on disk.
/// </summary>
internal static class SettingsChecks
{
    /// <summary>
    /// Asserts the settings saved last: the game's folder, and the one BepInEx and the one worlds folder given.
    /// </summary>
    /// <param name="game">The Valheim folder.</param>
    /// <param name="bep">The BepInEx folder.</param>
    /// <param name="worlds">The worlds folder.</param>
    internal static void AssertKept(string game, string bep, string worlds)
    {
        var kept = AppSettings.Load();
        Assert.Equal(game, kept.ValheimPath);
        Assert.Equal(new[] { bep }, kept.BepInExFolders);
        Assert.Equal(new[] { worlds }, kept.WorldFolders);
    }
}
