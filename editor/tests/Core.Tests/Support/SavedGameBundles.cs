using TerrainEditor.App;
using WorldEditor.Tests.Unity;

namespace WorldEditor.Tests;

/// <summary>
/// Opens a test game's bundles the way the editor reads them, once the game's files are on disk.
/// </summary>
public static class SavedGameBundles
{
    /// <summary>
    /// Saves the game's files, then opens its bundles with no index file kept.
    /// </summary>
    /// <param name="game">The game whose bundles are read.</param>
    /// <returns>The game's bundles.</returns>
    public static GameBundles Of(TestGame game)
    {
        game.Save();
        return game.Bundles();
    }
}
