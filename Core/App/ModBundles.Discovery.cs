using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// Finding the mods' bundles in the plugins folders, one of each, named so that no two copies share a file name, and
/// which bundles a single mod file is or holds.
/// </summary>
public static partial class ModBundles
{
    /// <summary>
    /// The mods' bundles in every plugins folder; when the same one is in several (the game and mod manager profiles:
    /// the same file name and resource, exactly), the newest. Different ones whose copies would have the same name
    /// ("Mod A.bundle" and "Mod_A.bundle", "Foo" and "foo") each get a name of their own.
    /// </summary>
    internal static List<Source> Find(AppSettings settings)
    {
        var found = new Dictionary<(string File, string? Resource), Source>();
        foreach (var (dir, _) in LocalGame.BepInExFolders(settings))
        {
            string plugins = Path.Combine(dir, "plugins");
            if (!Directory.Exists(plugins))
            {
                continue;
            }
            IEnumerable<string> files;
            try
            {
                files = [.. Directory.EnumerateFiles(plugins, "*", SearchOption.AllDirectories)];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Caught(e, "listing a plugins folder for the mods' bundles (passed over)");
                continue;
            }
            foreach (string file in files)
            {
                foreach (var s in In(file))
                {
                    var key = (Path.GetFileName(s.Path), s.Resource);
                    if (!found.TryGetValue(key, out var had) || had.Time < s.Time)
                    {
                        found[key] = s;
                    }
                }
            }
        }
        // Copies whose names would be the same file (on Windows, letter case aside): each named after what it is too.
        var named = found.Values.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => g.Count() == 1 ? g : g.Select(s => s with { Name = $"{s.Name}-{StableHash.Of($"{Path.GetFileName(s.Path)}\n{s.Resource}"):x8}" }));
        return [.. named.OrderBy(s => s.Name, StringComparer.Ordinal)];
    }
    /// <summary>A file name made of the name's letters, digits, dots, dashes and underscores (the rest as _).</summary>
    private static string Safe(string name) => new([.. name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')]);

    /// <summary>
    /// The bundles a file is or holds: a bundle file itself, or a DLL's embedded ones; none for anything else or a file
    /// that cannot be read.
    /// </summary>
    internal static IEnumerable<Source> In(string file)
    {
        var list = new List<Source>();
        try
        {
            var fi = new FileInfo(file);
            if (fi.Length < Magic.Length || fi.Length > MaxFile)
            {
                return list;
            }
            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string dll = Path.GetFileNameWithoutExtension(file);
                foreach (string resource in EmbeddedBundles(file))
                {
                    list.Add(new Source(Safe($"{dll}.{resource}"), file, resource, fi.Length, fi.LastWriteTimeUtc.Ticks));
                }
            }
            else if (StartsWithMagic(file))
            {
                list.Add(new Source(Safe(Path.GetFileName(file)), file, null, fi.Length, fi.LastWriteTimeUtc.Ticks));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            // Not a .NET DLL, or not readable: nothing in it.
            Log.CaughtBrief(e, "looking for bundles in a mod's file (nothing in it)");
        }
        return list;
    }

    /// <summary>Whether a file starts as a bundle does.</summary>
    private static bool StartsWithMagic(string file)
    {
        using var fs = File.OpenRead(file);
        Span<byte> head = stackalloc byte[7];
        return fs.ReadAtLeast(head, head.Length, false) == head.Length && head.SequenceEqual(Magic);
    }
}
