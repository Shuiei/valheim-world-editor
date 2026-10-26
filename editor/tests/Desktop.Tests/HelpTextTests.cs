using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>The documentation's plain text: marks left out, blocks spaced, pictures' descriptions only where asked.</summary>
public class HelpTextTests
{
    private static MarkdownDocument Read(string markdown) => Markdig.Markdown.Parse(markdown, HelpDocs.Pipeline);

    [Fact]
    public void AHeadingsTextLeavesItsMarksOutAndItsIdIsGitHubs()
    {
        var h = Read("## **Select** (`E`) and [more](x.md)").Descendants<HeadingBlock>().Single();
        Assert.Equal("Select (E) and more", HelpText.Of(h.Inline));
        Assert.Equal("select-e-and-more", HelpText.Id(h));
    }

    [Fact]
    public void ABlocksTextHoldsItsListsAndCellsAndCodeButNotItsPictures()
    {
        var doc = Read("- one\n- two\n\n| A | B |\n|---|---|\n| c | d |\n\n```\ncode line\n```\n\ntext ![a rock](r.jpg) after");
        string text = string.Join(' ', doc.Select(HelpText.Of));
        Assert.Equal("one two A B c d code line text after", string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void APicturesDescriptionIsItsTextAndABreakIsASpace()
    {
        var pic = Read("![A *rock* selected](r.jpg)").Descendants<LinkInline>().Single();
        Assert.Equal("A rock selected", HelpText.Of(pic));
        Assert.Equal("one two", HelpText.Of(Read("one\ntwo").Descendants<ParagraphBlock>().Single().Inline));
        Assert.Equal("", HelpText.Of((ContainerInline?)null));
    }
}
