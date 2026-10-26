namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor started with a world folder: that world's map.
/// </summary>
public sealed class FolderEditorProcess() : EditorProcess(Start())
{
    private static string? _copy;

    /// <summary>
    /// The command line: a fresh copy of the test world's folder.
    /// </summary>
    private static string[] Start()
    {
        _copy = TestWorldCopy.Make("vwe-visual-folder-");
        return [Path.Combine(_copy, "CITest")];
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        TestWorldCopy.Remove(_copy);
    }
}
