namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Points the saved selections' store at a test's own file, and puts the old path back when the test is done.
/// </summary>
public abstract class SavedSelectionsFolder : IDisposable
{
    private readonly string? _old = SavedSelections.PathOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-selections-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Makes the test's folder and points the store at its file.
    /// </summary>
    protected SavedSelectionsFolder()
    {
        Directory.CreateDirectory(_dir);
        SavedSelections.PathOverride = Path.Combine(_dir, "selections.json");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        SavedSelections.PathOverride = _old;
        Directory.Delete(_dir, true);
    }
}
