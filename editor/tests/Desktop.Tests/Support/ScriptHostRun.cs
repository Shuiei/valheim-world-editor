using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Compiles a script and runs it on a snapshot of a test area, the way the Script tool does, for the Script tests.
/// </summary>
internal static class ScriptHostRun
{
    /// <summary>
    /// Stopped by the token given, else with the test (when the run is cancelled).
    /// </summary>
    internal static ScriptHost.Changes Run(EditSession s, string code, CancellationToken? cancel = null)
    {
        var (image, pdb, errors) = ScriptHost.Compile(code);
        Assert.True(image != null, string.Join("\n", errors));
        return ScriptHost.Run(image!, pdb, ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf), cancel ?? TestContext.Current.CancellationToken);
    }
}
