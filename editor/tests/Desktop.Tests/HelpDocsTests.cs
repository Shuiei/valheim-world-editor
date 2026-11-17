using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The documentation carried in the app: every page of docs/ is in it, links lead where GitHub takes them, and no link
/// or picture of any page leads nowhere (the documentation's link check), the translations' pages included.
/// </summary>
public class HelpDocsTests
{
    /// <summary>The repository, above editor/tests/fixtures.</summary>
    private static readonly string Repo = Path.GetFullPath(Path.Combine(Fixtures.Root, "..", "..", ".."));

    /// <summary>docs/ on disk.</summary>
    private static readonly string Docs = Path.Combine(Repo, "docs");

    /// <summary>The language folders on disk: docs/'s folders with a README.md of their own (docs/fr/).</summary>
    private static readonly string[] LanguagesOnDisk = [.. Directory.EnumerateDirectories(Docs)
        .Where(d => File.Exists(Path.Combine(d, HelpDocs.Home)))
        .Select(Path.GetFileName)
        .OfType<string>()
        .Order(StringComparer.Ordinal)];

    /// <summary>Every page carried, English and translated: its English page and the file shown for it (the same for English).</summary>
    private static IEnumerable<(string Page, string File)> EveryFile() =>
        [.. HelpDocs.Pages.Select(p => (p, p)), .. HelpDocs.Languages.SelectMany(l => HelpDocs.Translations(l).Select(p => (p, l + "/" + p)))];

    /// <summary>
    /// The heading ids that lead somewhere on a page shown from a file: the file's own, and the English ones a translation
    /// with the same headings answers to (HelpDocs.AnchorAliases).
    /// </summary>
    private static HashSet<string> Ids(string page, string file)
    {
        var ids = HelpDocs.Read(file).Descendants<HeadingBlock>().Select(HelpText.Id).OfType<string>().ToHashSet();
        ids.UnionWith(HelpDocs.AnchorAliases(page, file).Keys);
        return ids;
    }

    [Fact]
    public void EveryPageOfDocsIsCarriedButTheReleaseNotesAndTheDevelopersPages()
    {
        // Not carried: the developers' pages, and the pointer pages kept so that links from outside to a tool page that
        // was merged into another still lead somewhere (shape.md and mountain.md, now landform.md). The translations are
        // not English pages (EveryTranslationIsCarried).
        string[] notCarried = ["architecture.md", "development.md", "shape.md", "mountain.md"];
        var onDisk = Directory.EnumerateFiles(Docs, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Docs, f).Replace('\\', '/'))
            .Where(p => !p.StartsWith("releases/", StringComparison.Ordinal) && !p.StartsWith("dev/", StringComparison.Ordinal) && !notCarried.Contains(p))
            .Where(p => !LanguagesOnDisk.Any(l => p.StartsWith(l + "/", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal);
        Assert.Equal(onDisk, HelpDocs.Pages);
        Assert.Contains(HelpDocs.Home, HelpDocs.Pages);
        Assert.Equal(File.ReadAllText(Path.Combine(Docs, "README.md")), HelpDocs.Text(HelpDocs.Home));
    }

    [Fact]
    public void EveryTranslationIsCarriedAndTranslatesAnEnglishPage()
    {
        Assert.Equal(LanguagesOnDisk, HelpDocs.Languages);
        foreach (string language in HelpDocs.Languages)
        {
            string folder = Path.Combine(Docs, language);
            var onDisk = Directory.EnumerateFiles(folder, "*.md", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(folder, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal);
            Assert.Equal(onDisk, HelpDocs.Translations(language));
            Assert.Empty(HelpDocs.Translations(language).Except(HelpDocs.Pages));
        }
    }

    [Fact]
    public void AnUpToDateTranslationHasTheHeadingsOfItsEnglishPage()
    {
        // Its source mark is the English page's now: it was made from it, so its headings are the same ones (their ids
        // are the English ones too then, for the topics and the English pages' links: HelpDocs.AnchorAliases).
        var differ = EveryFile()
            .Where(f => f.File != f.Page && !HelpDocs.IsOutdated(f.Page, f.File))
            .Where(f => !HelpDocs.SameHeadings([.. HelpDocs.Read(f.Page).Descendants<HeadingBlock>()], [.. HelpDocs.Read(f.File).Descendants<HeadingBlock>()]))
            .Select(f => f.File);
        Assert.Empty(differ);
    }

    [Fact]
    public void APageOrPictureNotCarriedIsNotFound()
    {
        Assert.False(HelpDocs.Has("nowhere.md"));
        Assert.Null(HelpDocs.Open("images/nowhere.jpg"));
        Assert.Throws<FileNotFoundException>(() => HelpDocs.Text("nowhere.md"));
        using var picture = HelpDocs.Open("images/overview.jpg");
        Assert.NotNull(picture);
    }

    [Theory]
    [InlineData("map.md", "#search-the-world", "map.md", "search-the-world")]
    [InlineData("map.md", "select.md", "select.md", null)]
    [InlineData("map.md", "./select.md#selecting", "select.md", "selecting")]
    [InlineData("map.md", "select.md#", "select.md", null)]
    public void ALinkToAPageCarriedStaysInTheHelp(string page, string href, string target, string? anchor)
    {
        var link = HelpDocs.Resolve(page, href);
        Assert.Equal(new HelpTarget(target, anchor), link?.Target);
        Assert.Null(link?.Web);
    }

    [Theory]
    [InlineData("https://thunderstore.io/c/valheim/", "https://thunderstore.io/c/valheim/")]
    [InlineData("mailto:someone@example.com", "mailto:someone@example.com")]
    [InlineData("../README.md#installation", HelpDocs.Repository + "README.md#installation")]
    [InlineData("worldeditor.schema.json", HelpDocs.Repository + "docs/worldeditor.schema.json")]
    [InlineData("../editor/Desktop/ScriptApi.csx", HelpDocs.Repository + "editor/Desktop/ScriptApi.csx")]
    public void ALinkOutOfThePagesCarriedOpensInTheBrowser(string href, string address)
    {
        var link = HelpDocs.Resolve("map.md", href);
        Assert.Equal(new Uri(address), link?.Web);
        Assert.Null(link?.Target);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AnEmptyLinkLeadsNowhere(string href) => Assert.Null(HelpDocs.Resolve("map.md", href));

    [Theory]
    [InlineData("map.md", "images/map.jpg", "images/map.jpg")]
    [InlineData("guides/first.md", "../images/map.jpg", "images/map.jpg")]
    [InlineData("guides/first.md", "./more/x.md", "guides/more/x.md")]
    [InlineData("map.md", "../../x.md", "../../x.md")]
    public void APathIsTakenFromThePagesFolder(string page, string relative, string path) =>
        Assert.Equal(path, HelpDocs.Combine(page, relative));

    [Fact]
    public void APlaceOnGitHubIsItsPageAndHeadingThere()
    {
        Assert.Equal(new Uri(HelpDocs.Repository + "docs/map.md#controls"), HelpDocs.OnGitHub(new HelpTarget("map.md", "controls")));
        Assert.Equal("map.md#controls", new HelpTarget("map.md", "controls").ToString());
        Assert.Equal(HelpDocs.Home, HelpTarget.Home.ToString());
    }

    [Fact]
    public void APageWithoutAHeadingIsCalledAfterItsFile()
    {
        var doc = Markdig.Markdown.Parse("Only text.", HelpDocs.Pipeline);
        Assert.Equal("notes", HelpDocs.Title(doc, "guides/notes.md"));
    }

    [Fact]
    public void EveryLinkOfTheDocumentationLeadsToAHeadingOrAFileThatExists()
    {
        var broken = new List<string>();
        foreach (var (page, file) in EveryFile())
        {
            // A translation's link to a page is shown in its language when the page has a translation there.
            string? language = file == page ? null : file[..^(page.Length + 1)];
            foreach (var link in HelpDocs.Read(file).Descendants<LinkInline>().Where(l => !l.IsImage && l.Url != null))
            {
                switch (HelpDocs.Resolve(file, link.Url!))
                {
                    case { Target: { } t } when t.Anchor != null
                        && !Ids(t.Page, language != null && HelpDocs.Has(language + "/" + t.Page) ? language + "/" + t.Page : t.Page).Contains(t.Anchor):
                        broken.Add($"{file}: {link.Url} (no such heading)");
                        break;
                    case { Web: { } web } when web.ToString().StartsWith(HelpDocs.Repository, StringComparison.Ordinal):
                        string inRepo = Uri.UnescapeDataString(web.AbsolutePath[new Uri(HelpDocs.Repository).AbsolutePath.Length..]);
                        if (!File.Exists(Path.Combine(Repo, inRepo)) && !Directory.Exists(Path.Combine(Repo, inRepo)))
                        {
                            broken.Add($"{file}: {link.Url} (no such file in the repository)");
                        }
                        break;
                    default:
                        // A heading that exists, or a web address.
                        break;
                }
            }
        }
        Assert.Empty(broken);
    }

    [Fact]
    public void EveryPictureOfTheDocumentationIsCarriedWithADescription()
    {
        var missing = new List<string>();
        foreach (var (_, file) in EveryFile())
        {
            foreach (var pic in HelpDocs.Read(file).Descendants<LinkInline>().Where(l => l.IsImage))
            {
                // A translation's picture not carried yet is the English one (HelpDocs.Picture).
                using var s = HelpDocs.Open(HelpDocs.Picture(file, pic.Url ?? ""));
                if (s == null || HelpText.Of(pic).Length == 0)
                {
                    missing.Add($"{file}: {pic.Url}");
                }
            }
        }
        Assert.Empty(missing);
    }
}
