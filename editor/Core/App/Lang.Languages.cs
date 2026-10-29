namespace TerrainEditor.App;

/// <summary>
/// The languages there are catalogues of, and choosing the one in use.
/// </summary>
public static partial class Lang
{
    /// <summary>A language there is a catalogue of.</summary>
    /// <param name="Code">Its code: the catalogue's file name ("en", "pt-BR").</param>
    /// <param name="Name">Its own name for itself (<c>NameKey</c>), else the code.</param>
    public sealed record Language(string Code, string Name);

    /// <summary>The languages there are catalogues of, carried and the user's, English first, then by name.</summary>
    public static IReadOnlyList<Language> Available()
    {
        var codes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { Source };
        const string Prefix = "TerrainEditor.Lang.";
        foreach (string r in typeof(Lang).Assembly.GetManifestResourceNames().Where(r => r.StartsWith(Prefix, StringComparison.Ordinal) && r.EndsWith(".json", StringComparison.Ordinal)))
        {
            codes.Add(r[Prefix.Length..^".json".Length]);
        }
        if (Directory.Exists(Folder))
        {
            foreach (string f in Directory.GetFiles(Folder, "*.json"))
            {
                codes.Add(Path.GetFileNameWithoutExtension(f));
            }
        }
        static string NameOf(string code) => code == Source ? English[NameKey] : Load(code) is { } t && t.TryGetValue(NameKey, out string? n) ? n : code;
        return [.. codes.Select(c => new Language(c, NameOf(c))).OrderBy(l => l.Code != Source).ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
    /// <summary>
    /// The first of the computer's languages there is a catalogue of: the exact one ("pt-BR"), else its main language
    /// ("pt"), else a catalogue of the same main language ("pt-PT"); null when none fits.
    /// </summary>
    internal static string? Pick(IEnumerable<string> wanted, IEnumerable<string> available)
    {
        var have = available.ToList();
        foreach (string w in wanted)
        {
            string main = w.Split('-')[0];
            string? found = have.Find(c => c.Equals(w, StringComparison.OrdinalIgnoreCase))
                ?? have.Find(c => c.Equals(main, StringComparison.OrdinalIgnoreCase))
                ?? have.Find(c => c.Split('-')[0].Equals(main, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
