using System.Text.Json;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The stand-in home's worlds, recent worlds, the user's home and the repository.
/// </summary>
public sealed partial class Editor
{
    /// <summary>
    /// Links the worlds into the stand-in Valheim's worlds_local folder, as a player has them (links to the read-only
    /// originals).
    /// </summary>
    /// <param name="local">The worlds_local folder.</param>
    /// <param name="worlds">World folders or files to link.</param>
    private static void LinkWorlds(string local, IReadOnlyList<string>? worlds)
    {
        foreach (string w in worlds ?? [])
        {
            Directory.CreateDirectory(local);
            string original = Path.GetFullPath(w);
            string link = Path.Combine(local, Path.GetFileName(original));
            if (Directory.Exists(original))
            {
                Directory.CreateSymbolicLink(link, original);
            }
            else
            {
                File.CreateSymbolicLink(link, original);
            }
        }
    }

    /// <summary>
    /// Writes the worlds opened before (the start page's Home) into the stand-in settings: the first a few hours ago,
    /// then a day apart.
    /// </summary>
    /// <param name="local">The worlds_local folder the worlds are linked in.</param>
    /// <param name="recent">The worlds, most recent first.</param>
    private void WriteRecent(string local, IReadOnlyList<string> recent)
    {
        var now = DateTime.Now;
        var opened = recent.Select((w, i) => new
        {
            Path = Path.Combine(local, Path.GetFileName(Path.GetFullPath(w))),
            Name = Path.GetFileName(Path.GetFullPath(w)),
            Opened = (now - TimeSpan.FromHours(3) - TimeSpan.FromDays(i)).ToString("yyyy-MM-ddTHH:mm:ss.ffffff"),
        });
        File.WriteAllText(Path.Combine(_data, "settings.json"), JsonSerializer.Serialize(new { Recent = opened }));
    }

    /// <summary>
    /// Copies the app into the stand-in home's ValheimWorldEditor folder, as a player unpacks it there: the program's
    /// files are copied, its subfolders (native libraries, translations) linked. The app then runs from there, so the
    /// folders it names next to its program are in the stand-in home ("~").
    /// </summary>
    /// <param name="app">The app's dll or executable; null for the Debug build in editor/Desktop/bin.</param>
    /// <returns>The copy to run.</returns>
    private string AppInHome(string? app)
    {
        app ??= Path.Combine(Repo, "editor", "Desktop", "bin", "Debug", "net10.0", "ValheimWorldEditor.dll");
        string from = Path.GetDirectoryName(Path.GetFullPath(app))!;
        string to = Path.Combine(_home, "ValheimWorldEditor");
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
        {
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        }
        foreach (string d in Directory.GetDirectories(from))
        {
            Directory.CreateSymbolicLink(Path.Combine(to, Path.GetFileName(d)), d);
        }
        return Path.Combine(to, Path.GetFileName(app));
    }

    /// <summary>The stand-in home's data folder: what the app keeps there (its settings.json) for a scene to read.</summary>
    public string DataFolder => Path.Combine(_home, ".local", "share", "ValheimWorldEditor");

    /// <summary>The real user's home (the stand-in one is only the app's).</summary>
    private static string UserHome => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Finds <c>Repo</c> from this program's folder upwards; throws when there is none.</summary>
    private static string FindRepo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "VERSION")) && Directory.Exists(Path.Combine(d.FullName, "editor", "Desktop")))
            {
                return d.FullName;
            }
        }
        throw new InvalidOperationException("the repository was not found above " + AppContext.BaseDirectory);
    }
}
