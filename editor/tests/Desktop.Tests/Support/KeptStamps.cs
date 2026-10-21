namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The kept stamps' file in a folder of its own for one test: Dispose gives the stamps their file back and removes the
/// folder.
/// </summary>
internal sealed class KeptStamps : IDisposable
{
    private readonly string? _was = Stamps.PathOverride;

    /// <summary>
    /// Points the kept stamps at <c>stamps.json</c> in <paramref name="sub"/> of a new temporary folder (the folders
    /// are not made).
    /// </summary>
    internal KeptStamps(string sub = "")
    {
        FilePath = Path.Combine(Dir, sub, "stamps.json");
        Stamps.PathOverride = FilePath;
    }

    /// <summary>The temporary folder.</summary>
    internal string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-stamps-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The kept stamps' file.</summary>
    internal string FilePath { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stamps.PathOverride = _was;
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }
}
