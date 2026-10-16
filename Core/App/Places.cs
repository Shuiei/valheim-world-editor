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
    /// A path as typed, ready to use: trimmed, surrounding quotes dropped, and a leading ~ (alone or before a
    /// separator) turned into the home folder. Another user's ~name is left as it is.
    /// </summary>
    /// <param name="path">The typed path.</param>
    public static string ExpandHome(string path)
    {
        path = path.Trim().Trim('"');
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
        }
        return path;
    }

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

    /// <summary>
    /// A folder as one key whichever way it is reached: its full path without a separator at the end, its links
    /// followed (~/.steam/steam is a link to ~/.local/share/Steam), and on Windows in one letter case. As far as it can
    /// be when a link cannot be read.
    /// </summary>
    public static string FolderKey(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string root = Path.GetPathRoot(full) ?? "";
        string current = root;
        foreach (string part in full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            try
            {
                if (Directory.ResolveLinkTarget(current, returnFinalTarget: true) is { } target)
                {
                    current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A link that cannot be followed: the path as written from here on.
            }
        }
        return OperatingSystem.IsWindows() ? current.ToUpperInvariant() : current;
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
        else if (OperatingSystem.IsMacOS())
        {
            // Unity's data folder on a Mac.
            yield return Path.Combine(home, "Library", "Application Support", "IronGate", "Valheim", "worlds_local");
            yield return Path.Combine(home, "Library", "Application Support", "IronGate", "Valheim", "worlds");
        }
        else
        {
            yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds_local");
            yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds");
            // Valheim through Proton (Steam Play) keeps its Windows-style folder inside the prefix (Flatpak's Steam
            // keeps its own in its sandbox).
            foreach (string steam in new[] { Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam"), Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam") })
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
