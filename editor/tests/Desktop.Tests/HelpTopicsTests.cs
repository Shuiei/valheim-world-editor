using Markdig.Syntax;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>Where F1 and the panels' help buttons lead: every place is a page and heading the app carries.</summary>
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
    public void AToolsTopicIsItsPageAndABrushsItsPartOfTheBrushesPage()
    {
        Assert.Equal(new HelpTarget("select.md"), HelpTopics.Tool(ToolMode.Select));
        Assert.Equal(new HelpTarget("terrain.md", "sculpt-brushes"), HelpTopics.Tool(ToolMode.Brush, BrushTool.Raise));
        Assert.Equal(new HelpTarget("terrain.md", "paint-brushes"), HelpTopics.Tool(ToolMode.Brush, BrushTool.PaintPaved));
        Assert.Equal(new HelpTarget("terrain.md", "sculpt-brushes"), HelpTopics.Tool(ToolMode.Brush));
        Assert.Equal(new HelpTarget("area.md", "copy-and-paste"), HelpTopics.Tool(ToolMode.Paste));
        Assert.Equal(HelpTopics.Blueprints, HelpTopics.Tool(ToolMode.Blueprints));
        Assert.Equal(new HelpTarget("editor-basics.md", "camera"), HelpTopics.Tool(ToolMode.View));
        // Each tool its own place.
        Assert.Equal(Enum.GetValues<ToolMode>().Length, Enum.GetValues<ToolMode>().Select(m => HelpTopics.Tool(m)).Distinct().Count());
    }
}
