namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A test's own temporary folder for the worlds it makes or opens, made under the prefix and a random suffix, and
/// deleted when the test is done.
/// </summary>
public abstract class WorldsFolder : IDisposable
{
    /// <summary>
    /// The temporary folder the test's worlds are kept in.
    /// </summary>
    protected string Dir { get; }

    /// <summary>
    /// Names the test's folder under the temporary folder, from the prefix and a random suffix.
    /// </summary>
    /// <param name="prefix">The start of the folder's name.</param>
    protected WorldsFolder(string prefix) => Dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, true);
        }
    }
}
