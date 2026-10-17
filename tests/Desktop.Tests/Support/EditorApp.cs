using System.Diagnostics;

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

    /// <summary>
    /// Makes an empty folder under the temporary folder, to be the program's home (its settings and data).
    /// </summary>
    /// <param name="prefix">The start of the folder's name; a random suffix follows it.</param>
    /// <returns>The new folder.</returns>
    public static string MakeHome(string prefix)
    {
        string home = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        return home;
    }

    /// <summary>
    /// Deletes a folder made by <see cref="MakeHome"/>, if it is still there.
    /// </summary>
    /// <param name="home">The folder to delete.</param>
    public static void RemoveHome(string home)
    {
        if (Directory.Exists(home))
        {
            Directory.Delete(home, true);
        }
    }

    /// <summary>
    /// Starts the program with these arguments, its standard input, output and error redirected, and its settings and
    /// data in the folder given (the caller's own, never the user's).
    /// </summary>
    /// <param name="home">The empty folder the program uses as its home, data and local application data.</param>
    /// <param name="args">The program's command-line arguments.</param>
    /// <returns>The running process.</returns>
    public static Process Start(string home, params string[] args)
    {
        var psi = new ProcessStartInfo(Dotnet) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(Path);
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }
        psi.Environment["XDG_DATA_HOME"] = home;
        psi.Environment["HOME"] = home;
        psi.Environment["LOCALAPPDATA"] = home;
        return Process.Start(psi)!;
    }
}
