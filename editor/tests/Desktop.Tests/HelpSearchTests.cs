using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>The Help window's search: parts cut at headings, every word needed, headings first, a few words around.</summary>
public class HelpSearchTests
{
    private static HelpSearch Of(params (string Page, string Markdown)[] pages) =>
        new(pages.Select(p => (p.Page, Markdig.Markdown.Parse(p.Markdown, HelpDocs.Pipeline))));

    private static readonly HelpSearch Sample = Of(
        ("ground.md", "# Ground tools\n\nThe ground tools raise and paint.\n\n## Paint\n\nPaint dirt or paved stone.\n\n## Raise\n\nLifts the ground."),
        ("path.md", "# Path\n\nRoads, rivers. A road can be paved.\n\n### Paint along a path\n\nThe path is painted."));

    [Fact]
    public void APartMatchesWhenItHoldsTheStartOfEveryWord()
    {
        Assert.Equal([new HelpTarget("ground.md", "paint")], Sample.Find("PAVE pai").Select(h => h.Target));
        Assert.Equal([new HelpTarget("path.md")], Sample.Find("pav road").Select(h => h.Target));
        Assert.Empty(Sample.Find("paved lifts"));
        Assert.Empty(Sample.Find(" ·, "));
    }

    [Fact]
    public void APartWhoseHeadingHoldsTheWordComesFirst()
    {
        var hits = Sample.Find("paint");
        Assert.Equal(new HelpTarget("ground.md", "paint"), hits[0].Target);
        Assert.Equal(new HelpTarget("path.md", "paint-along-a-path"), hits[1].Target);
        Assert.Equal(("Ground tools", "Paint"), (hits[0].Page, hits[0].Heading));
        Assert.Contains(hits, h => h.Target == new HelpTarget("ground.md") && h.Heading == "Ground tools");
    }

    [Fact]
    public void ASearchListsAtMostMaxHits()
    {
        var many = Of([.. Enumerable.Range(0, HelpSearch.MaxHits + 5).Select(i => ($"p{i}.md", $"# Page {i}\n\nstone"))]);
        Assert.Equal(HelpSearch.MaxHits, many.Find("stone").Count);
    }

    [Fact]
    public void WordsAreLettersAndDigitsInLowerCase()
    {
        Assert.Equal(["8", "m", "limit"], HelpSearch.Words("±8 m — LIMIT, limit"));
        Assert.Empty(HelpSearch.Words("  ·  "));
    }

    [Fact]
    public void ASnippetShowsTheWordsAroundTheFirstMatchCutAtSpaces()
    {
        string text = string.Join(' ', Enumerable.Range(0, 60).Select(i => "word" + i)) + " target " + string.Join(' ', Enumerable.Range(0, 60).Select(i => "after" + i));
        string snippet = HelpSearch.Snippet(text, "targ");
        Assert.StartsWith("…word", snippet);
        Assert.EndsWith("…", snippet);
        Assert.Contains(" target ", snippet);
        Assert.DoesNotContain("word0 ", snippet);
        Assert.Equal("short text", HelpSearch.Snippet("short   text", "nothing"));
        Assert.Equal("a mid-word xtarget, then target", HelpSearch.Snippet("a mid-word xtarget, then target", "target"));
    }

    [Fact]
    public void TheCarriedSearchFindsTheToolsPages() =>
        Assert.Contains(HelpSearch.Carried.Find("select box"), h => h.Target.Page == "select.md");
}
