using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor started with --world and --zone: straight into the 3D view of a copy of the test world.
/// </summary>
public sealed class DirectEditorProcess() : EditorProcess(Start())
{
    private static string? _copy;

    /// <summary>
    /// The command line: a fresh copy of the test world, its zone 0, 0, 2 zones across.
    /// </summary>
    private static string[] Start()
    {
        _copy = Path.Combine(Path.GetTempPath(), "vwe-visual-direct-" + Guid.NewGuid().ToString("N")[..8]);
        string world = Path.Combine(_copy, "CITest");
        Directory.CreateDirectory(world);
        foreach (string f in Directory.GetFiles(Path.Combine(Fixtures.Root, "CITest")))
        {
            File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
        }
        return ["--world", world, "--zone", "0,0", "--size", "2"];
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_copy))
        {
            Directory.Delete(_copy!, true);
        }
    }
}
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
