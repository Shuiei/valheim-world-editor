using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Help window: one for the whole app, Back and Forward, the contents with the page's parts, search, links (to the
/// browser when out of the help), enlarged pictures and its keys.
/// </summary>
public class HelpWindowTests
{
    /// <summary>Runs a test on the Help window opened at a place, then closes it (none is left open for the next test).</summary>
    private static void With(HelpTarget? target, Action<HelpWindow> test)
    {
        var w = HelpWindow.Open(target);
        Dispatcher.UIThread.RunJobs();
        try
        {
            test(w);
        }
        finally
        {
            w.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>The side's buttons' texts (a hit's or a page's first line).</summary>
    private static List<string> SideTexts(HelpWindow w) =>
        [.. w.Side.Children.OfType<Button>().Select(b => b.Content switch { TextBlock t => t.Text!, StackPanel s => ((TextBlock)s.Children[1]).Text!, _ => "" })];

    [AvaloniaFact]
    public void OneWindowShowsEveryPlaceAskedForAndClosingItLetsANewOneOpen()
    {
        With(null, w =>
        {
            Assert.Same(w, HelpWindow.Current);
            Assert.Equal(HelpTarget.Home, w.Shown);
            Assert.Equal("Valheim World Editor help", w.Title);
            Assert.Same(w, HelpWindow.Open(new HelpTarget("select.md")));
            Assert.Equal("select.md", w.Shown.Page);
            Assert.StartsWith("Select", w.Title);
        });
        Assert.Null(HelpWindow.Current);
    }

    [AvaloniaFact]
    public void BackAndForwardMoveAlongThePlacesVisited()
    {
        With(null, w =>
        {
            Assert.False(w.BackButton.IsEnabled);
            w.Go(new HelpTarget("map.md"));
            w.Go(new HelpTarget("map.md"));
            w.Go(new HelpTarget("select.md", "selecting"));
            w.BackButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new HelpTarget("map.md"), w.Shown);
            w.BackButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(HelpTarget.Home, w.Shown);
            Assert.False(w.BackButton.IsEnabled);
            w.Move(-1);
            Assert.Equal(HelpTarget.Home, w.Shown);
            w.ForwardButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new HelpTarget("map.md"), w.Shown);
            w.Go(new HelpTarget("area.md"));
            Assert.False(w.ForwardButton.IsEnabled);
            w.HomeButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(HelpTarget.Home, w.Shown);
        });
    }

    [AvaloniaFact]
    public void TheContentsMarkThePageShownWithItsPartsUnderIt()
    {
        With(new HelpTarget("select.md", "selecting"), w =>
        {
            var on = w.Side.Children.OfType<Button>().Where(b => b.Classes.Contains("on")).Select(b => ((TextBlock)b.Content!).Text).ToList();
            Assert.Equal(["Select", "Selecting"], on);
            var texts = SideTexts(w);
            Assert.Contains("How to use it", texts);
            Assert.Contains("Ground tools", texts);
            Assert.Equal(1, texts.Count(t => t == "Select"));
            w.Side.Children.OfType<Button>().First(b => b.Content is TextBlock { Text: "Ground tools" })
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new HelpTarget("terrain.md"), w.Shown);
        });
    }

    [AvaloniaFact]
    public void APageTheContentsDoNotListIsAtTheirTop()
    {
        With(null, w =>
        {
            string unlisted = HelpDocs.Pages.FirstOrDefault(p => p != HelpDocs.Home && HelpContents.Find(p) == null) ?? HelpDocs.Home;
            w.Go(new HelpTarget(unlisted));
            Assert.True(unlisted == HelpDocs.Home || ((Button)w.Side.Children[0]).Classes.Contains("on"));
        });
    }

    [AvaloniaFact]
    public void TypingSearchesEnterOpensTheFirstHitAndEscEmptiesTheSearch()
    {
        With(null, w =>
        {
            w.SearchBox.Text = "select box";
            Assert.NotEmpty(w.Hits);
            Assert.Equal(w.Hits.Count, w.Side.Children.Count);
            w.SearchBox.Focus();
            w.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Assert.Equal(w.Hits[0].Target, w.Shown);
            w.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Assert.Equal("", w.SearchBox.Text);
            Assert.Empty(w.Hits);
            Assert.Contains("Select", SideTexts(w));
            w.SearchBox.Text = "zzzqqq";
            Assert.Empty(w.Hits);
            Assert.Contains("zzzqqq", ((TextBlock)w.Side.Children[0]).Text);
            var before = w.Shown;
            w.OpenFirstHit();
            Assert.Equal(before, w.Shown);
        });
    }

    [AvaloniaFact]
    public void LinksStayInTheHelpOrOpenInTheBrowser()
    {
        With(new HelpTarget("map.md"), w =>
        {
            var opened = new List<Uri>();
            w.OpenUrl = u => { opened.Add(u); return Task.FromResult(true); };
            w.Follow("#controls");
            Assert.Equal(new HelpTarget("map.md", "controls"), w.Shown);
            w.Follow("select.md");
            Assert.Equal(new HelpTarget("select.md"), w.Shown);
            w.Follow("https://example.com/");
            w.Follow("");
            w.BrowserButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal([new Uri("https://example.com/"), HelpDocs.OnGitHub(new HelpTarget("select.md"))], opened);
        });
    }

    [AvaloniaFact]
    public void AHeadingAskedForIsScrolledToAndAnotherPlaceStartsAtTheTop()
    {
        With(new HelpTarget("terrain.md", "mountain"), w =>
        {
            Dispatcher.UIThread.RunJobs();
            Assert.True(w.Scroller.Offset.Y > 100);
            w.Go(new HelpTarget("terrain.md", "no-such-heading"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, w.Scroller.Offset.Y);
        });
    }

    [AvaloniaFact]
    public void APictureIsEnlargedAndAClickOrEscClosesIt()
    {
        With(null, w =>
        {
            w.Enlarge("images/nowhere.jpg");
            Assert.False(w.Enlarged.IsVisible);
            w.Enlarge("images/map.jpg");
            Assert.True(w.Enlarged.IsVisible);
            w.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Assert.False(w.Enlarged.IsVisible);
            w.Enlarge("images/map.jpg");
            w.MouseDown(new Avalonia.Point(400, 400), MouseButton.Left);
            w.MouseUp(new Avalonia.Point(400, 400), MouseButton.Left);
            Assert.False(w.Enlarged.IsVisible);
            w.Enlarge("images/map.jpg");
            w.Go(new HelpTarget("map.md"));
            Assert.False(w.Enlarged.IsVisible);
        });
    }

    [AvaloniaFact]
    public void AltArrowsGoBackAndForwardAndCtrlFSearches()
    {
        With(null, w =>
        {
            w.Go(new HelpTarget("map.md"));
            w.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Alt);
            Assert.Equal(HelpTarget.Home, w.Shown);
            w.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            Assert.Equal(new HelpTarget("map.md"), w.Shown);
            w.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Assert.Equal(new HelpTarget("map.md"), w.Shown);
            w.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control);
            Assert.True(w.SearchBox.IsFocused);
        });
    }
}
