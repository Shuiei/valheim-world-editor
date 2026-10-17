using WorldEditor.Tests;

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
        _copy = Path.Combine(Path.GetTempPath(), "vwe-visual-folder-" + Guid.NewGuid().ToString("N")[..8]);
        string world = Path.Combine(_copy, "CITest");
        Directory.CreateDirectory(world);
        foreach (string f in Directory.GetFiles(Path.Combine(Fixtures.Root, "CITest")))
        {
            File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
        }
        return [world];
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        try
        {
            Directory.Delete(_copy!, true);
        }
        catch
        {
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }
}
