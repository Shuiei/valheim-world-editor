using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A documentation page drawn with the app's controls: headings kept by id, links followed on a click, pictures with
/// their captions (a click enlarges one), tables, tips and warnings, lists, quotes and code.
/// </summary>
public class HelpViewTests
{
    /// <summary>A page drawn from Markdown; the links and pictures clicked are noted in <paramref name="clicks"/>.</summary>
    private static (HelpView View, StackPanel Page) Draw(string markdown, List<string>? clicks = null, string page = "map.md")
    {
        var view = new HelpView(page, href => clicks?.Add("follow " + href), path => clicks?.Add("enlarge " + path));
        return (view, view.Build(Markdig.Markdown.Parse(markdown, HelpDocs.Pipeline)));
    }

    /// <summary>A page shown in a window (laid out, so text can be clicked); closed by the caller.</summary>
    private static Window Shown(Control page)
    {
        var w = new Window { Content = page, Width = 900, Height = 700 };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return w;
    }

    [AvaloniaFact]
    public void HeadingsAreKeptByIdAndTheSecondLevelHasALineUnder()
    {
        var (view, page) = Draw("# Title\n\n## Walk and fly\n\n### Brush settings\n\n#### Fine");
        Assert.Equal(["title", "walk-and-fly", "brush-settings", "fine"], view.Anchors.Keys);
        Assert.IsType<TextBlock>(view.Anchors["title"]);
        var h2 = Assert.IsType<Border>(view.Anchors["walk-and-fly"]);
        Assert.Equal(new Thickness(0, 0, 0, 1), h2.BorderThickness);
        Assert.True(((TextBlock)view.Anchors["title"]).FontSize > ((TextBlock)h2.Child!).FontSize);
        Assert.Equal(4, page.Children.Count);
    }

    [AvaloniaFact]
    public void ALinksCharactersLeadToItAndTheOthersToNothing()
    {
        var (_, page) = Draw("See the **[world map](map.md#controls)** and `code` then [the web](https://example.com) here.");
        var w = Shown(page);
        try
        {
            var text = (TextBlock)page.Children[0];
            string shown = "See the world map and code then the web here.";
            Assert.Equal(shown, text.Inlines!.Text);
            string? At(string word) => HelpView.LinkAt(text, text.TextLayout.HitTestTextPosition(shown.IndexOf(word, StringComparison.Ordinal) + 1).Center);
            Assert.Equal("map.md#controls", At("map"));
            Assert.Equal("https://example.com", At("web"));
            Assert.Null(At("See"));
            Assert.Null(At("code"));
            Assert.Null(HelpView.LinkAt(new TextBlock { Text = "no links" }, default));
        }
        finally
        {
            w.Close();
        }
    }

    [AvaloniaFact]
    public void APictureShowsWithItsCaptionAndAClickEnlargesIt()
    {
        var clicks = new List<string>();
        var (_, page) = Draw("![The map](images/map.jpg)\n*Its caption.*\n\n![Alone](images/map.jpg)\n\n*A caption apart.*\n\n*Not a caption.*", clicks);
        var withCaption = Assert.IsType<StackPanel>(page.Children[0]);
        var button = Assert.IsType<Button>(withCaption.Children[0]);
        Assert.Equal("The map", ToolTip.GetTip(button));
        Assert.IsType<Bitmap>(((Image)((Border)button.Content!).Child!).Source);
        Assert.Equal("Its caption.", ((TextBlock)withCaption.Children[1]).Inlines!.Text);
        Assert.Equal(Ui.Muted, ((TextBlock)withCaption.Children[1]).Foreground);
        Assert.IsType<Button>(page.Children[1]);
        Assert.Equal(Ui.Muted, ((TextBlock)page.Children[2]).Foreground);
        Assert.Equal(Ui.Text, ((TextBlock)page.Children[3]).Foreground);
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(["enlarge images/map.jpg"], clicks);
    }

    [AvaloniaFact]
    public void APictureNotCarriedShowsItsDescription()
    {
        var (_, page) = Draw("![A lost picture](images/lost.jpg)");
        Assert.Equal("[A lost picture]", ((TextBlock)page.Children[0]).Text);
    }

    [AvaloniaFact]
    public void ATableHasAColumnPerCellItsHeaderOnABandAndALineUnderEachRowButTheLast()
    {
        var (_, page) = Draw("| Key | What it does |\n|---|---|\n| `E` | Selects a very long list of things that goes on |\n| `M` | Measures |");
        var grid = Assert.IsType<Grid>(((Border)page.Children[0]).Child);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.True(grid.ColumnDefinitions[1].Width.Value > grid.ColumnDefinitions[0].Width.Value);
        var cells = grid.Children.Cast<Border>().ToList();
        Assert.Equal(6, cells.Count);
        Assert.Equal(Ui.Panel2, cells[0].Background);
        Assert.Null(cells[2].Background);
        Assert.Equal(1, cells[2].BorderThickness.Bottom);
        Assert.Equal(0, cells[5].BorderThickness.Bottom);
    }

    [AvaloniaTheory]
    [InlineData("TIP", "Tip")]
    [InlineData("NOTE", "Note")]
    [InlineData("IMPORTANT", "Important")]
    [InlineData("WARNING", "Warning")]
    [InlineData("CAUTION", "Caution")]
    public void ATipOrWarningIsABoxNamedAfterItsKind(string kind, string name)
    {
        var (_, page) = Draw($"> [!{kind}]\n> Mind the edge.");
        var body = Assert.IsType<StackPanel>(((Border)page.Children[0]).Child);
        Assert.Equal(name, ((TextBlock)body.Children[0]).Text);
        Assert.Equal("Mind the edge.", ((TextBlock)body.Children[1]).Inlines!.Text);
    }

    [AvaloniaFact]
    public void ListsQuotesCodeAndRulesAreDrawnAndHtmlIsLeftOut()
    {
        var (_, page) = Draw("3. three\n4. four\n   - inner\n\n> quoted\n\n```\nvar x = 1;\n```\n\n---\n\n<div>html</div>\n\n- dot");
        var ordered = Assert.IsType<StackPanel>(page.Children[0]);
        Assert.Equal(["3.", "4."], ordered.Children.Cast<Grid>().Select(g => ((TextBlock)g.Children[0]).Text));
        var inner = ((StackPanel)((Grid)ordered.Children[1]).Children[1]).Children[1];
        Assert.Equal("•", ((TextBlock)((Grid)((StackPanel)inner).Children[0]).Children[0]).Text);
        Assert.Equal(new Thickness(3, 0, 0, 0), ((Border)page.Children[1]).BorderThickness);
        var code = ((Border)page.Children[2]).GetSelfAndLogicalDescendants().OfType<SelectableTextBlock>().Single();
        Assert.Equal("var x = 1;", code.Text);
        Assert.Equal(1, ((Border)page.Children[3]).Height);
        Assert.Equal(5, page.Children.Count);
        Assert.Equal(HorizontalAlignment.Left, ((TextBlock)((Grid)((StackPanel)page.Children[4]).Children[0]).Children[0]).HorizontalAlignment);
    }

    [AvaloniaFact]
    public void TextKeepsItsMarksAndAPictureWithinItIsItsDescription()
    {
        var (_, page) = Draw("Plain **bold** *italic* `code` ![icon](images/map.jpg) <https://example.com> line  \nbreak");
        var text = (TextBlock)page.Children[0];
        Assert.Equal("Plain bold italic code icon https://example.com line\nbreak", text.Inlines!.Text);
    }
}
