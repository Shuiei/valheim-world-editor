using System.Reflection;
using Markdig;
using Markdig.Syntax;

namespace TerrainEditor.Desktop;

/// <summary>A place in the documentation: a page (its path under docs/, "map.md") and a heading on it (null: the top).</summary>
/// <param name="Page">The page's path under docs/, with forward slashes.</param>
/// <param name="Anchor">The heading's id, as GitHub makes it ("walk-and-fly"); null for the top of the page.</param>
public sealed record HelpTarget(string Page, string? Anchor = null)
{
    /// <summary>The documentation's first page: what it holds, and where to start.</summary>
    public static HelpTarget Home { get; } = new(HelpDocs.Home);

    /// <summary>"page.md#anchor" (or "page.md"), as a link in the documentation is written.</summary>
    public override string ToString() => Anchor == null ? Page : Page + "#" + Anchor;
}

/// <summary>Where a link leads: a place in the documentation carried, or else a web address.</summary>
/// <param name="Target">The place in the documentation; null for an address.</param>
/// <param name="Web">The address; null for a place in the documentation.</param>
public sealed record HelpLink(HelpTarget? Target, Uri? Web);

/// <summary>
/// The documentation inside the app: docs/'s pages and pictures, carried in the program (ValheimWorldEditor.Desktop.csproj,
/// "Docs/..." resources), so the Help window works offline and always shows the version it came with. The same Markdown
/// GitHub shows: one text for both. Links are read as GitHub reads them; a link out of the pages carried (the code, a mod
/// author's file) goes to GitHub.
/// </summary>
public static class HelpDocs
{
    /// <summary>The first page, which also lists every page in the order the Help window's contents show them.</summary>
    public const string Home = "README.md";

    /// <summary>The repository on GitHub, where a link to anything not carried goes.</summary>
    public const string Repository = "https://github.com/Shuiei/valheim-world-editor/blob/main/";

    /// <summary>The resources' prefix ("Docs/map.md").</summary>
    private const string Prefix = "Docs/";

    /// <summary>The program, which carries the documentation.</summary>
    private static readonly Assembly Carrier = typeof(HelpDocs).Assembly;

    /// <summary>
    /// How the pages are read: GitHub's tables, alerts ("> [!TIP]") and heading ids, so a link's "#anchor" finds the
    /// heading it finds on GitHub.
    /// </summary>
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAlertBlocks()
        .UseAutoIdentifiers(Markdig.Extensions.AutoIdentifiers.AutoIdentifierOptions.GitHub)
        .UseAutoLinks()
        .Build();

    /// <summary>Every page carried, by its path under docs/ ("map.md"), in alphabetical order.</summary>
    public static IReadOnlyList<string> Pages { get; } = [.. Carrier.GetManifestResourceNames()
        .Select(Normalise)
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
        .Select(n => n[Prefix.Length..])
        .Order(StringComparer.Ordinal)];

    /// <summary>The resources' names by their path with forward slashes (Windows builds name them with backslashes).</summary>
    private static readonly Dictionary<string, string> Resources = Carrier.GetManifestResourceNames().ToDictionary(Normalise, n => n);

    /// <summary>A resource's name with forward slashes.</summary>
    private static string Normalise(string name) => name.Replace('\\', '/');

    /// <summary>Whether a page is carried.</summary>
    /// <param name="page">Its path under docs/.</param>
    public static bool Has(string page) => Resources.ContainsKey(Prefix + page);

    /// <summary>A file of the documentation (a page, a picture), opened for reading; null when it is not carried.</summary>
    /// <param name="path">Its path under docs/ ("images/map.jpg").</param>
    public static Stream? Open(string path) => Resources.TryGetValue(Prefix + path, out string? name) ? Carrier.GetManifestResourceStream(name) : null;

    /// <summary>A page's Markdown; throws <c>FileNotFoundException</c> when it is not carried.</summary>
    /// <param name="page">Its path under docs/.</param>
    public static string Text(string page)
    {
        using var s = Open(page) ?? throw new FileNotFoundException("No documentation page " + page, page);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>A page, read (Pipeline).</summary>
    /// <param name="page">Its path under docs/.</param>
    public static MarkdownDocument Read(string page) => Markdown.Parse(Text(page), Pipeline);

    /// <summary>A page's title: its first heading's text, or its file name when it has none.</summary>
    /// <param name="doc">The page, read.</param>
    /// <param name="page">Its path under docs/.</param>
    public static string Title(MarkdownDocument doc, string page) =>
        doc.Descendants<HeadingBlock>().FirstOrDefault() is { } h ? HelpText.Of(h.Inline) : Path.GetFileNameWithoutExtension(page);

    /// <summary>
    /// Where a link on a page leads: a place in the documentation carried, or else a web address (the link itself when it
    /// is one; GitHub's copy of a file of the repository otherwise).
    /// </summary>
    /// <param name="page">The page the link is on, under docs/.</param>
    /// <param name="href">The link as written ("map.md#search-the-world", "#walk-and-fly", "../README.md", "https://...").</param>
    /// <returns>Where it leads; null for a link that leads nowhere (empty).</returns>
    public static HelpLink? Resolve(string page, string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }
        if (Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
        {
            return new HelpLink(null, uri);
        }
        int hash = href.IndexOf('#', StringComparison.Ordinal);
        string path = hash < 0 ? href : href[..hash];
        string? anchor = hash < 0 || hash == href.Length - 1 ? null : href[(hash + 1)..];
        string target = path.Length == 0 ? page : Combine(page, Uri.UnescapeDataString(path));
        if (Has(target))
        {
            return new HelpLink(new HelpTarget(target, anchor), null);
        }
        // Not carried: the repository's copy (a link above docs/ climbs out of it).
        string inRepo = target.StartsWith("../", StringComparison.Ordinal) ? target[3..] : "docs/" + target;
        return new HelpLink(null, new Uri(Repository + inRepo + (anchor == null ? "" : "#" + anchor)));
    }

    /// <summary>A path relative to a page's folder, made a path under docs/ ("../x" stays above it as "../x").</summary>
    /// <param name="page">The page, under docs/.</param>
    /// <param name="relative">The path relative to its folder.</param>
    public static string Combine(string page, string relative)
    {
        var parts = new List<string>(page.Split('/')[..^1]);
        int above = 0;
        foreach (string p in relative.Split('/'))
        {
            if (p == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
                else
                {
                    above++;
                }
            }
            else if (p is not ("." or ""))
            {
                parts.Add(p);
            }
        }
        return string.Concat(Enumerable.Repeat("../", above)) + string.Join('/', parts);
    }

    /// <summary>The address of a place in the documentation on GitHub (Open in browser).</summary>
    /// <param name="target">The place.</param>
    public static Uri OnGitHub(HelpTarget target) => new(Repository + "docs/" + target);
}
