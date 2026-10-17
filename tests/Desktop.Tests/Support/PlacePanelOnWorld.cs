using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A Place panel on a test world's scene, as the Place panel tests open it: the window, the scene, the input with a Place
/// tool on that scene, and a panel with the default names and an empty memory.
/// </summary>
/// <param name="Window">The main window, not loaded with a project.</param>
/// <param name="Scene">The world's scene.</param>
/// <param name="Input">The input the panel's Place tool is in.</param>
/// <param name="Panel">The Place panel.</param>
internal sealed record PlacePanelOnWorld(MainWindow Window, WorldScene Scene, PlaceInput Input, PlacePanel Panel)
{
    /// <summary>
    /// Opens a main window and the Place panel on the scene of the world at this path.
    /// </summary>
    /// <param name="world">The folder of the world.</param>
    internal static PlacePanelOnWorld Open(string world)
    {
        var w = new MainWindow(load: false);
        var scene = WorldScene.Load(world, 0, 0, 1);
        var input = new PlaceInput(w.View, new PlaceTool { Scene = () => scene });
        var panel = new PlacePanel(input, () => scene, PrefabCatalog.NameOf, new PlaceMemory());
        return new(w, scene, input, panel);
    }
}
