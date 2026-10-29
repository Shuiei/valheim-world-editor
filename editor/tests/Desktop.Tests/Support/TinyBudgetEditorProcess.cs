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
        _copy = TestWorldCopy.Make("vwe-visual-budget-");
        return ["--world", Path.Combine(_copy, "CITest"), "--zone", "0,0", "--size", "2"];
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        TestWorldCopy.Remove(_copy);
    }
}
