using System.Net;
using System.Net.Sockets;

namespace TerrainEditor.App;

/// <summary>
/// Where things are on this computer, shared by the web app and the native app: the game's world
/// folders, and a free local port.
/// </summary>
public static class Places
{
    /// <summary>
    /// The folders in a folder; none when it cannot be read (a drive's "System Volume Information", a
    /// root-owned lost+found, another user's folder).
    /// </summary>
    public static string[] Subfolders(string dir)
    {
        try
        {
            return Directory.GetDirectories(dir);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    /// <summary>A folder holding a world (_main.&lt;n&gt;.chunks); false when it cannot be read.</summary>
    public static bool HasWorld(string dir)
    {
        try
        {
            return Directory.GetFiles(dir, "_main.*.chunks").Length > 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// The folders where Valheim keeps worlds on this system (worlds_local, then the old worlds folder),
    /// with Proton's prefixes on Linux. They may not exist.
    /// </summary>
    public static IEnumerable<string> WorldRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(home, "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");
            yield return Path.Combine(home, "AppData", "LocalLow", "IronGate", "Valheim", "worlds");
        }
        else
        {
            yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds_local");
            yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds");
            // Valheim through Proton (Steam Play) keeps its Windows-style folder inside the prefix.
            foreach (string steam in new[] { Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam") })
            {
                yield return Path.Combine(steam, "steamapps", "compatdata", GameLook.ValheimAppId.ToString(), "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");
            }
        }
    }

    /// <summary>
    /// The first local (loopback) port from <paramref name="from"/> that can be listened on, trying 50; else
    /// one the system picks.
    /// </summary>
    public static int FreePort(int from)
    {
        for (int p = from; p < from + 50; p++)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, p);
                l.Start();
                l.Stop();
                return p;
            }
            catch (SocketException)
            {
            }
        }
        var any = new TcpListener(IPAddress.Loopback, 0);
        any.Start();
        int port = ((IPEndPoint)any.LocalEndpoint).Port;
        any.Stop();
        return port;
    }
}
