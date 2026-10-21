using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A main window on its own copy of the test world, with the copy's folder deleted when disposed. The windows that need
/// their questions and messages collected add those to the class that derives from this one.
/// </summary>
internal abstract class CopiedWorldWindow : IDisposable
{
    public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
    public string Dir { get; } = Fixtures.CopyFixture();

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        string folder = Path.GetDirectoryName(Dir)!;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
