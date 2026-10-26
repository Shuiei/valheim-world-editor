// The Steam folders GameLook looks in, per operating system (see GameLook).

namespace TerrainEditor.App;

public static partial class GameLook
{
    /// <summary>
    /// Steam's system-wide folders on Linux (looked at only with <c>SearchOutsideHome</c>). Settable so the tests can
    /// stand them in with folders of their own; put back afterwards.
    /// </summary>
    internal static IReadOnlyList<string> SystemSteamRoots { get; set; } = ["/opt/Steam", "/usr/share/steam"];

    /// <summary>
    /// Steam's folders on this computer, before the libraries they list are added: the registry and default places on
    /// Windows, the home's and Flatpak's on Linux, the system ones on Linux when <c>SearchOutsideHome</c>.
    /// </summary>
    private static List<string> SteamRoots()
    {
        var roots = new List<string>();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string sp)
                {
                    roots.Add(sp.Replace('/', '\\'));
                }
            }
            catch (Exception e)
            {
                Log.CaughtBrief(e, "reading Steam's folder from the registry (the other places are still searched)");
            }
            if (SearchOutsideHome)
            {
                roots.Add(@"C:\Program Files (x86)\Steam");
                roots.Add(@"C:\Program Files\Steam");
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            roots.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
        }
        else
        {
            roots.AddRange(
            [
                Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam"), Path.Combine(home, ".steam", "root"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
            ]);
            if (SearchOutsideHome)
            {
                roots.AddRange(SystemSteamRoots);
            }
        }
        return roots;
    }
}
