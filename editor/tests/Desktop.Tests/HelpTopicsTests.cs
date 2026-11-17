using Markdig.Syntax;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Where F1 and the panels' help buttons lead: every place is a page and heading the app carries, in English and in
/// every up-to-date translation.
/// </summary>
public class HelpTopicsTests
{
    [Fact]
    public void EveryTopicLeadsToAPageAndHeadingTheAppCarries()
    {
        var broken = HelpTopics.All.Distinct()
            .Where(t => !HelpDocs.Has(t.Page)
                || (t.Anchor != null && !HelpDocs.Read(t.Page).Descendants<HeadingBlock>().Any(h => HelpText.Id(h) == t.Anchor)))
            .Select(t => t.ToString());
        Assert.Empty(broken);
    }

    [Fact]
    public void EveryTopicFindsItsHeadingInEveryUpToDateTranslation()
    {
        // An up-to-date translation has its English page's headings, which answer to the English ids (HelpDocs.AnchorAliases).
        var broken = HelpDocs.Languages
            .SelectMany(l => HelpTopics.All.Distinct().Where(t => t.Anchor != null).Select(t => (Topic: t, File: l + "/" + t.Page)))
            .Where(x => HelpDocs.Has(x.File) && !HelpDocs.IsOutdated(x.Topic.Page, x.File))
            .Where(x => !HelpDocs.Read(x.File).Descendants<HeadingBlock>().Any(h => HelpText.Id(h) == x.Topic.Anchor)
                && !HelpDocs.AnchorAliases(x.Topic.Page, x.File).ContainsKey(x.Topic.Anchor!))
            .Select(x => x.File + "#" + x.Topic.Anchor);
        Assert.Empty(broken);
    }

    [Fact]
    public void AToolsTopicIsItsPageAndABrushsItsPartOfTheBrushesPage()
    {
        Assert.Equal(new HelpTarget("select.md"), HelpTopics.Tool(ToolMode.Select));
        Assert.Equal(new HelpTarget("terrain.md", "sculpt-brushes"), HelpTopics.Tool(ToolMode.Brush, BrushTool.Raise));
        Assert.Equal(new HelpTarget("terrain.md", "paint-brush"), HelpTopics.Tool(ToolMode.Brush, BrushTool.PaintPaved));
        Assert.Equal(new HelpTarget("terrain.md", "sculpt-brushes"), HelpTopics.Tool(ToolMode.Brush));
        Assert.Equal(new HelpTarget("area.md", "copy-and-paste"), HelpTopics.Tool(ToolMode.Paste));
        Assert.Equal(HelpTopics.Blueprints, HelpTopics.Tool(ToolMode.Blueprints));
        Assert.Equal(new HelpTarget("editor-basics.md", "camera"), HelpTopics.Tool(ToolMode.View));
        // Each tool its own place.
        Assert.Equal(Enum.GetValues<ToolMode>().Length, Enum.GetValues<ToolMode>().Select(m => HelpTopics.Tool(m)).Distinct().Count());
    }
}
