namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The folder a visual test saves its pictures in: a new one under the temporary folder, named for the test, deleted when
/// the test is disposed.
/// </summary>
public abstract class VisualPicturesTestBase : IDisposable
{
    /// <summary>
    /// The folder the test's pictures are saved in (not made until the test makes it).
    /// </summary>
    protected string Dir { get; }

    /// <summary>
    /// A folder of its own for a test's pictures.
    /// </summary>
    /// <param name="name">Names the folder after the test's pictures (in its name under the temporary folder).</param>
    protected VisualPicturesTestBase(string name)
    {
        Dir = Path.Combine(Path.GetTempPath(), "vwe-visual-" + name + "-" + Guid.NewGuid().ToString("N")[..8]);
    }

    /// <summary>
    /// Deletes the folder with the pictures, if the test made it.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, true);
        }
        GC.SuppressFinalize(this);
    }
}
