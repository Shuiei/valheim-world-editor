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
        _copy = TestWorldCopy.Make("vwe-visual-direct-");
        return ["--world", Path.Combine(_copy, "CITest"), "--zone", "0,0", "--size", "2"];
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        TestWorldCopy.Remove(_copy);
    }
}
