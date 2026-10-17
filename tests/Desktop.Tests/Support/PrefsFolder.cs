namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A test's own folder for its prefs file, made when the test starts and deleted when it is done.
/// </summary>
public abstract class PrefsFolder : IDisposable
{
    /// <summary>
    /// The folder the test's files are kept in.
    /// </summary>
    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-prefs-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// The prefs file in the test's folder.
    /// </summary>
    protected string File1 => Path.Combine(Dir, "prefs.json");

    /// <summary>
    /// Makes the test's folder.
    /// </summary>
    protected PrefsFolder() => Directory.CreateDirectory(Dir);

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, true);
        }
    }
}
