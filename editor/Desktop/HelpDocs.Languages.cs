using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The documentation's translations. A translation is a folder of docs/ named after its language's code, as the app's
/// catalogues are (docs/fr/, docs/pt-BR/), mirroring the English pages (docs/fr/guides/make-a-road.md) with its own
/// pictures (docs/fr/images/): a folder at the top of docs/ with a README.md of its own is one (the English folders,
/// guides/ and the others, have none). The Help window's places stay English pages (HelpTarget, the contents, the
/// topics); each is shown in the editor's language when it has a translation (ShownFile), its links and pictures read
/// from there. A translation says which English text it was made from (its source mark), so one the English has
/// changed since is still shown, with a note (IsOutdated).
/// </summary>
public static partial class HelpDocs
{
    /// <summary>
    /// The language folders the documentation has (carried, or among the tests' files), by their language's code
    /// ("fr"), in alphabetical order.
    /// </summary>
    public static IReadOnlyList<string> Languages => Overlay == null ? CarriedLanguages : LanguagesOf([.. CarriedFiles, .. Overlay.Keys]);

    /// <summary>
    /// The language the documentation is shown in: the editor's (<c>Lang.Current</c>) when the documentation has a
    /// translation of it; null for English. Asked each time (tests change the language).
    /// </summary>
    public static string? Language => Languages.Contains(Lang.Current) ? Lang.Current : null;

    /// <summary>
    /// The file shown for a page: its translation in the language shown ("fr/map.md") when there is one, else the page
    /// itself.
    /// </summary>
    /// <param name="page">The English page, under docs/ ("map.md").</param>
    public static string ShownFile(string page) => Language is { } lang && Has(lang + "/" + page) ? lang + "/" + page : page;

    /// <summary>
    /// The English path of a file of the documentation: a file in a language folder without that folder
    /// ("fr/guides/x.md" is "guides/x.md", "fr/images/map.jpg" is "images/map.jpg"); any other path as it is.
    /// </summary>
    /// <param name="path">The file's path under docs/.</param>
    public static string EnglishPath(string path) => LanguageOf(path, Languages) is { } lang ? path[(lang.Length + 1)..] : path;

    /// <summary>
    /// The translated pages of a language folder, each by its English page's path ("map.md" for docs/fr/map.md), in
    /// alphabetical order.
    /// </summary>
    /// <param name="language">The language folder ("fr").</param>
    public static IReadOnlyList<string> Translations(string language)
    {
        string folder = language + "/";
        var files = Overlay == null ? CarriedFiles : CarriedFiles.Union(Overlay.Keys);
        return [.. files
            .Where(f => f.StartsWith(folder, StringComparison.Ordinal) && f.EndsWith(".md", StringComparison.Ordinal))
            .Select(f => f[folder.Length..])
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The path under docs/ of a picture of a file: its path from the file's folder; for a translation's picture that is
    /// not carried, the English picture of the same name ("fr/images/map.jpg" is "images/map.jpg"), so a translation can
    /// come before its pictures.
    /// </summary>
    /// <param name="file">The file the picture is on, under docs/ (a page, or its translation).</param>
    /// <param name="url">The picture's path as written ("images/map.jpg").</param>
    public static string Picture(string file, string url)
    {
        string path = Combine(file, url);
        return Has(path) ? path : EnglishPath(path);
    }

    /// <summary>
    /// The heading ids of a page that its translation's headings answer to as well: each English heading's id, by the
    /// id of the translated heading in the same place ("history" for "historique"), so a place named in English (the
    /// topics, the links of English pages) finds its heading in the translation. Only when both have the same headings
    /// (SameHeadings); none otherwise, or when the file is the page itself.
    /// </summary>
    /// <param name="page">The English page, under docs/.</param>
    /// <param name="file">The file shown for it (ShownFile).</param>
    /// <returns>The translated heading's id by the English one's (only where they differ).</returns>
    public static IReadOnlyDictionary<string, string> AnchorAliases(string page, string file)
    {
        var aliases = new Dictionary<string, string>();
        if (file == page)
        {
            return aliases;
        }
        var english = Read(page).Descendants<HeadingBlock>().ToList();
        var translated = Read(file).Descendants<HeadingBlock>().ToList();
        if (!SameHeadings(english, translated))
        {
            return aliases;
        }
        foreach (var (en, tr) in english.Zip(translated))
        {
            if (HelpText.Id(en) is { } from && HelpText.Id(tr) is { } to && from != to)
            {
                aliases.TryAdd(from, to);
            }
        }
        return aliases;
    }

    /// <summary>Whether two pages have the same headings: as many, of the same levels, in the same order.</summary>
    /// <param name="english">The English page's headings, in order.</param>
    /// <param name="translated">Its translation's headings, in order.</param>
    public static bool SameHeadings(IReadOnlyList<HeadingBlock> english, IReadOnlyList<HeadingBlock> translated) =>
        english.Count == translated.Count && english.Zip(translated).All(p => p.First.Level == p.Second.Level);

    /// <summary>
    /// A translation's source mark: the start of the English text's SHA-256 (its line ends made "\n", in UTF-8), 8
    /// characters in lower case, as <c>sha256sum docs/map.md | cut -c1-8</c> gives it. The translation carries the mark
    /// of the English text it was made from as <c>&lt;!-- source: 1a2b3c4d --&gt;</c> (docs/translating.md).
    /// </summary>
    /// <param name="english">The English page's text.</param>
    public static string SourceMark(string english) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(english.Replace("\r\n", "\n", StringComparison.Ordinal))))[..8];

    /// <summary>
    /// Whether a translation was made from another English text than the page's now: its source mark is missing or is
    /// not the page's (SourceMark). Never for the page itself.
    /// </summary>
    /// <param name="page">The English page, under docs/.</param>
    /// <param name="file">The file shown for it (ShownFile).</param>
    public static bool IsOutdated(string page, string file) =>
        file != page && SourceComment().Match(Text(file)) is var m && (!m.Success || m.Groups[1].Value != SourceMark(Text(page)));

    /// <summary>A translation's source mark, as written in it: <c>&lt;!-- source: 1a2b3c4d --&gt;</c>.</summary>
    [GeneratedRegex(@"<!--\s*source:\s*([0-9a-f]{8})\s*-->")]
    private static partial Regex SourceComment();

    /// <summary>The language folders among files of the documentation: the folders at the top of docs/ with a README.md of their own.</summary>
    /// <param name="files">The files, by their path under docs/.</param>
    private static string[] LanguagesOf(IEnumerable<string> files) => [.. files
        .Where(f => f.EndsWith("/" + Home, StringComparison.Ordinal) && f.IndexOf('/', StringComparison.Ordinal) == f.Length - Home.Length - 1)
        .Select(f => f[..^(Home.Length + 1)])
        .Distinct()
        .Order(StringComparer.Ordinal)];

    /// <summary>The language folder a file is in; null for an English file.</summary>
    /// <param name="path">The file's path under docs/.</param>
    /// <param name="languages">The language folders.</param>
    private static string? LanguageOf(string path, IReadOnlyList<string> languages)
    {
        int slash = path.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 && languages.Contains(path[..slash]) ? path[..slash] : null;
    }
}

/// <summary>
/// A value made from the files shown (the contents, the search), made once and made again when they change: another
/// language shown, or the tests' files (HelpDocs.Overlay).
/// </summary>
/// <typeparam name="T">The value.</typeparam>
/// <param name="make">Makes it from the files shown.</param>
internal sealed class ShownDocs<T>(Func<T> make) where T : class
{
    /// <summary>Keeps two threads from making it at once.</summary>
    private readonly Lock _lock = new();

    /// <summary>The language and the tests' files it was made for.</summary>
    private (string? Language, object? Overlay) _madeFor;

    /// <summary>The value for the files shown now (made when first asked for, and again once they change).</summary>
    public T Value
    {
        get
        {
            lock (_lock)
            {
                var now = (HelpDocs.Language, (object?)HelpDocs.Overlay);
                if (field == null || _madeFor != now)
                {
                    field = make();
                    _madeFor = now;
                }
                return field;
            }
        }
    }
}
