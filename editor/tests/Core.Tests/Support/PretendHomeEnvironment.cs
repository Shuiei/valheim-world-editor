namespace WorldEditor.Tests;

/// <summary>
/// The environment variables of a pretend home, set for one test and put back when disposed: HOME and the XDG folders
/// point inside the home, and so does LOCALAPPDATA (the editor's data folder on Windows: AppData\Local in the home, so
/// nothing a test writes lands in the real profile there).
/// </summary>
public sealed class PretendHomeEnvironment : IDisposable
{
    private static readonly string[] Vars = ["HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA"];
    private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Makes the pretend home's folder and points the variables into it.
    /// </summary>
    /// <param name="home">The pretend home's folder (made).</param>
    public PretendHomeEnvironment(string home)
    {
        Directory.CreateDirectory(home);
        Environment.SetEnvironmentVariable("HOME", home);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(home, ".config"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(home, ".local", "share"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", Path.Combine(home, "AppData", "Local"));
    }

    /// <summary>
    /// Puts every variable back as it was before the home was pointed.
    /// </summary>
    public void Dispose()
    {
        foreach (var (k, v) in _old)
        {
            Environment.SetEnvironmentVariable(k, v);
        }
    }
}
