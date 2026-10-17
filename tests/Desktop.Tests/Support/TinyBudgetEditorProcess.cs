using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Started straight in the 3D editor with a graphics-card budget of 1 byte: every area opened lets the models go and
/// reads them again.
/// </summary>
public sealed class TinyBudgetEditorProcess() : EditorProcess(Start(), new Dictionary<string, string> { ["VWE_GPU_BUDGET"] = "1" })
{
    private static string? _copy;

    /// <summary>
    /// The command line: a fresh copy of the test world, its zone 0, 0, 2 zones across.
    /// </summary>
    private static string[] Start()
    {
        _copy = Path.Combine(Path.GetTempPath(), "vwe-visual-budget-" + Guid.NewGuid().ToString("N")[..8]);
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
