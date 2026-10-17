using System.Reflection;
using System.Runtime.InteropServices;

namespace TerrainEditor.Desktop;

// The command that starts this editor as a script's process.
public static partial class ScriptHost
{
    /// <summary>
    /// The program and arguments that start this editor as a script's process: the editor's own program when it runs
    /// as one (published), else the dotnet that runs it (as <c>dotnet ValheimWorldEditor.dll</c>, or inside the tests)
    /// with the editor's assembly.
    /// </summary>
    internal static (string File, string[] Arguments) ChildCommand()
    {
        string? exe = Environment.ProcessPath;
        bool dotnet = exe != null && Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        if (exe != null && !dotnet && Assembly.GetEntryAssembly() == typeof(ScriptHost).Assembly)
        {
            return (exe, ["--script-host"]);
        }
        string host = dotnet ? exe! : Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        // The assembly's file beside the app (Assembly.Location is empty in a single-file app, which the analyzer refuses
        // at publish even though that app takes the branch above).
        string assembly = Path.Combine(AppContext.BaseDirectory, typeof(ScriptHost).Assembly.GetName().Name + ".dll");
        return (host, [assembly, "--script-host"]);
    }
}
