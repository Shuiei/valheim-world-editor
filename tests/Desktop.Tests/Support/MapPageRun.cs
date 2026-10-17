namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A main window on its own copy of the test world, with the map page's messages collected; deleted when disposed.
/// </summary>
internal sealed class MapPageRun : CopiedWorldWindow
{
    public List<string> Told { get; } = [];
    public MapPage M => W.MapPage!;

    /// <summary>
    /// Opens the world (its map page) and collects what the map tells.
    /// </summary>
    public async Task Open()
    {
        W.Show();
        await W.OpenWorld(() => Task.Run(() => WorldSession.Open(Dir)), "Opening…");
        M.Tell = t => { Told.Add(t); return Task.CompletedTask; };
    }
}
