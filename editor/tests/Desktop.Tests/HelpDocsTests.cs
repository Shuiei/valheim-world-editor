using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The documentation carried in the app: every page of docs/ is in it, links lead where GitHub takes them, and no link
/// or picture of any page leads nowhere (the documentation's link check).
/// </summary>
public class HelpDocsTests
{
    /// <summary>The repository, above editor/tests/fixtures.</summary>
    private static readonly string Repo = Path.GetFullPath(Path.Combine(Fixtures.Root, "..", "..", ".."));

    [Fact]
    public void EveryPageOfDocsIsCarriedButTheReleaseNotes()
    {
        string docs = Path.Combine(Repo, "docs");
        var onDisk = Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(docs, f).Replace('\\', '/'))
            .Where(p => !p.StartsWith("releases/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        Assert.Equal(onDisk, HelpDocs.Pages);
        Assert.Contains(HelpDocs.Home, HelpDocs.Pages);
        Assert.Equal(File.ReadAllText(Path.Combine(docs, "README.md")), HelpDocs.Text(HelpDocs.Home));
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
        var ids = HelpDocs.Pages.ToDictionary(p => p, p => HelpDocs.Read(p).Descendants<HeadingBlock>().Select(HelpText.Id).ToHashSet());
        var broken = new List<string>();
        foreach (string page in HelpDocs.Pages)
        {
            foreach (var link in HelpDocs.Read(page).Descendants<LinkInline>().Where(l => !l.IsImage && l.Url != null))
            {
                switch (HelpDocs.Resolve(page, link.Url!))
                {
                    case { Target: { } t } when t.Anchor != null && !ids[t.Page].Contains(t.Anchor):
                        broken.Add($"{page}: {link.Url} (no such heading)");
                        break;
                    case { Web: { } web } when web.ToString().StartsWith(HelpDocs.Repository, StringComparison.Ordinal):
                        string file = Uri.UnescapeDataString(web.AbsolutePath[new Uri(HelpDocs.Repository).AbsolutePath.Length..]);
                        if (!File.Exists(Path.Combine(Repo, file)) && !Directory.Exists(Path.Combine(Repo, file)))
                        {
                            broken.Add($"{page}: {link.Url} (no such file in the repository)");
                        }
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
        foreach (string page in HelpDocs.Pages)
        {
            foreach (var pic in HelpDocs.Read(page).Descendants<LinkInline>().Where(l => l.IsImage))
            {
                using var s = HelpDocs.Open(HelpDocs.Combine(page, pic.Url ?? ""));
                if (s == null || HelpText.Of(pic).Length == 0)
                {
                    missing.Add($"{page}: {pic.Url}");
                }
            }
        }
        Assert.Empty(missing);
    }
}
