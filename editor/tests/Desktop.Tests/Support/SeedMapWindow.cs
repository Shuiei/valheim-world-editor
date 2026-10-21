using Avalonia.Controls;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A map in its window; disposing it closes the window and stops the map's work (its timer, the close look being
/// sampled), so nothing of a test runs on into the next.
/// </summary>
internal sealed class SeedMapWindow(Window window, SeedMap map) : IDisposable
{
    /// <summary>
    /// The window and the map in it.
    /// </summary>
    public void Deconstruct(out Window w, out SeedMap m) => (w, m) = (window, map);

    /// <summary>
    /// Closes the window, then stops the map.
    /// </summary>
    public void Dispose()
    {
        window.Close();
        map.Dispose();
    }
}
