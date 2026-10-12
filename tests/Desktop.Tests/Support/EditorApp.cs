namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor's program for the tests that start it in a process of their own: the copy in the tests' folder (built
/// with them), not Desktop/bin's. Under coverage (CI on main) the collector measures that process too, and the app's
/// code it ran is added to what the test process ran only when both loaded the same file: an editor started from
/// Desktop/bin replaced the test process's coverage of the whole app with its own.
/// </summary>
public static class EditorApp
{
    /// <summary>The program's ValheimWorldEditor.dll, beside the tests'.</summary>
    public static string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "ValheimWorldEditor.dll");

    /// <summary>The dotnet that runs the tests, to run the program with.</summary>
    public static string Dotnet { get; } = System.IO.Path.GetFullPath(System.IO.Path.Combine(
        System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
}
