namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Starts editing again the area a main window's view shows, for the tests that load an area while something else happens.
/// </summary>
internal static class ViewAreaReload
{
    /// <summary>
    /// Starts editing the area the window's view shows, without waiting for it: returns that scene and the load in progress.
    /// </summary>
    internal static (WorldScene Scene, Task Load) Start(WorldWindowRun r)
    {
        var scene = r.W.View.Scene!;
        var load = r.W.EditBlock(scene.X0, scene.Z0, scene.ZonesX, scene.ZonesZ);
        return (scene, load);
    }
}
