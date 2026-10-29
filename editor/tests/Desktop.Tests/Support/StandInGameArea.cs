using TerrainEditor.Save;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The stand-in game opened live in a window, with the map's view stopped and an area of it edited.
/// </summary>
internal static class StandInGameArea
{
    /// <summary>
    /// Opens the stand-in game live in <paramref name="w"/>, stops its map and edits the area at zone (x, z),
    /// <paramref name="size"/> zones across.
    /// </summary>
    internal static async Task OpenLive(MainWindow w, FakeGame game, int x, int z, int size)
    {
        await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Opening…");
        w.MapPage?.Stop();
        await w.EditArea(x, z, size);
    }
}
