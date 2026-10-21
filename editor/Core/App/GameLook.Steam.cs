// Finding Valheim in Steam's libraries: the configured folder first, then each Steam library (see GameLook).

using System.Text.RegularExpressions;

namespace TerrainEditor.App;

public static partial class GameLook
{
    /// <summary>
    /// The configured folder if it is valid, else Valheim in any Steam library on this computer.
    /// </summary>
    public static string? FindValheim(string? configured)
    {
        if (configured != null && BundlesDir(configured) != null)
        {
            return configured;
        }
        foreach (string lib in SteamLibraries())
        {
            string v = Path.Combine(lib, "steamapps", "common", "Valheim");
            if (BundlesDir(v) != null)
            {
                return v;
            }
        }
        return null;
    }

    /// <summary>
    /// Steam's folders on this computer, each followed by the other libraries its libraryfolders.vdf lists; each
    /// folder once.
    /// </summary>
    private static IEnumerable<string> SteamLibraries()
    {
        var seen = new HashSet<string>();
        foreach (string root in SteamRoots())
        {
            if (!Directory.Exists(root) || !seen.Add(Path.GetFullPath(root)))
            {
                continue;
            }
            yield return root;
            // Other libraries are listed in libraryfolders.vdf: "path"   "D:\\SteamLibrary"
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
            {
                continue;
            }
            // Being rewritten by Steam, or without access: that root's other libraries are not searched this time.
            string libraries;
            try
            {
                libraries = File.ReadAllText(vdf);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Caught(ex, "reading Steam's libraryfolders.vdf (the libraries it lists are not searched)");
                continue;
            }
            foreach (Match m in Regex.Matches(libraries, "\"path\"\\s+\"([^\"]+)\""))
            {
                string lib = m.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib)))
                {
                    yield return lib;
                }
            }
        }
    }
}
