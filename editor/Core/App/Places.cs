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
            Log.CaughtBrief(ex, "listing a folder's subfolders (none)");
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
                Log.CaughtBrief(ex, "following a folder's link (the path as written)");
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
            Log.CaughtBrief(ex, "looking for a world in a folder (taken as none)");
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
            foreach (string steamapps in SteamApps(home))
            {
                yield return PrefixWorldRoot(steamapps);
            }
        }
    }

    /// <summary>The steamapps folders of Steam's own folders under a home on Linux (Flatpak's Steam keeps its own in its sandbox).</summary>
    private static string[] SteamApps(string home) =>
    [
        Path.Combine(home, ".steam", "steam", "steamapps"),
        Path.Combine(home, ".local", "share", "Steam", "steamapps"),
        Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam", "steamapps"),
    ];

    /// <summary>The game's Proton prefix under a Steam library's steamapps folder (it exists once the game ran through Proton).</summary>
    private static string Prefix(string steamapps) => Path.Combine(steamapps, "compatdata", GameLook.ValheimAppId.ToString(), "pfx");

    /// <summary>The worlds folder inside the Proton prefix of a Steam library's steamapps folder.</summary>
    private static string PrefixWorldRoot(string steamapps) =>
        Path.Combine(Prefix(steamapps), "drive_c", "users", "steamuser", "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");

    /// <summary>
    /// The worlds folder the installed game saves to, where a new world must go for the game to list it. On Linux, a
    /// Windows build of the game run through Proton (valheim.exe in its folder and no Linux build) saves inside its
    /// Steam library's prefix, the native build under ~/.config; elsewhere, and without a game, the first of
    /// <see cref="WorldRoots"/>. The folder may not exist yet: the game makes it on its first save.
    /// </summary>
    /// <param name="valheim">The game's folder (<see cref="GameLook.FindValheim"/>); null for none found.</param>
    public static string GameWorldRoot(string? valheim)
    {
        if (OperatingSystem.IsLinux() && valheim != null && File.Exists(Path.Combine(valheim, "valheim.exe")) && !File.Exists(Path.Combine(valheim, "valheim.x86_64")))
        {
            // <library>/steamapps/common/Valheim (links followed): the prefix is beside common, under compatdata. A
            // Windows build kept elsewhere: the prefix of a Steam folder on this computer, when the game has one there
            // (its worlds folder need not exist yet).
            string real = FolderKey(valheim);
            string? common = Path.GetDirectoryName(real), steamapps = common == null ? null : Path.GetDirectoryName(common);
            if (steamapps != null && Path.GetFileName(common) == "common" && Path.GetFileName(steamapps) == "steamapps")
            {
                return PrefixWorldRoot(steamapps);
            }
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (SteamApps(home).FirstOrDefault(s => Directory.Exists(Prefix(s))) is string withPrefix)
            {
                return PrefixWorldRoot(withPrefix);
            }
        }
        return WorldRoots().First();
    }

    /// <summary>The same, for the game the settings name (or the one found in Steam's libraries).</summary>
    /// <param name="settings">The editor's settings (the Valheim folder).</param>
    public static string GameWorldRoot(AppSettings settings) => GameWorldRoot(GameLook.FindValheim(settings.ValheimPath));

    /// <summary>
    /// The world folders to offer for a new world: the installed game's first, whether it exists yet or not, then the
    /// other folders the game may keep worlds in that exist, each once.
    /// </summary>
    /// <param name="settings">The editor's settings (the Valheim folder).</param>
    public static List<string> WorldRootsForNew(AppSettings settings)
    {
        string game = GameWorldRoot(settings);
        List<string> roots = [game];
        // Each folder once whichever way it is reached (~/.steam/steam is a link to ~/.local/share/Steam).
        HashSet<string> seen = [FolderKey(game)];
        roots.AddRange(WorldRoots().Where(r => Directory.Exists(r) && seen.Add(FolderKey(r))));
        return roots;
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
            catch (SocketException e)
            {
                Log.CaughtBrief(e, $"trying the local port {p} (in use: the next one is tried)");
            }
        }
        var any = new TcpListener(IPAddress.Loopback, 0);
        any.Start();
        int port = ((IPEndPoint)any.LocalEndpoint).Port;
        any.Stop();
        return port;
    }
}
