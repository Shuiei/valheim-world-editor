using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>The Help window's contents, read from the first page's lists under its "##" headings.</summary>
public class HelpContentsTests
{
    [Fact]
    public void EachPartListsThePagesItsItemsLinkToFirst()
    {
        var doc = Markdig.Markdown.Parse("""
            # Help

            - [Not in a part](map.md)

            ## Start here

            - [The map](map.md): what it shows.
            - Text first, then [a link](select.md).
            - [On the web](https://example.com)
            - [A heading](select.md#selecting)

            ## Empty part

            Just text.

            ## Tools

            - [Select](select.md)
            """, HelpDocs.Pipeline);
        var sections = HelpContents.Of(doc, HelpDocs.Home);
        Assert.Equal(["Start here", "Tools"], sections.Select(s => s.Title));
        Assert.Equal(
            [new HelpEntry("The map", new HelpTarget("map.md")), new HelpEntry("A heading", new HelpTarget("select.md", "selecting"))],
            sections[0].Entries);
        Assert.Equal([new HelpEntry("Select", new HelpTarget("select.md"))], sections[1].Entries);
    }

    [Fact]
    public void TheCarriedContentsListEveryPageButTheFirst()
    {
        var listed = HelpContents.Sections.SelectMany(s => s.Entries).Select(e => e.Target.Page).ToHashSet();
        Assert.Equal(HelpDocs.Pages.Where(p => p != HelpDocs.Home).Order(), listed.Order());
        Assert.Equal("Select", HelpContents.Find("select.md")?.Title);
        Assert.Null(HelpContents.Find("nowhere.md"));
    }
}
