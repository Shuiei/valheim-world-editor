using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page: a seed's world shown with its lists and landmarks (the game's location rules from the test
/// fixtures, as CI has no game), and the switches over the map and the rows that show a place.
/// </summary>
public class NewWorldPageTests
{
    /// <summary>
    /// A seed whose world the core's tests know: the start at the temple near the middle, three Eikthyr altars.
    /// </summary>
    internal const string Seed = "5DCcdIcuYJ";

    /// <summary>
    /// How long a seed's world may take to show, in milliseconds. About 40 s on CI, but the first seed a run shows also
    /// reads the location rules and compiles the generator, and main's runs measure coverage, which slows it all: that
    /// took the first test past 60 s on every main run.
    /// </summary>
    internal const int SeedWait = 180000;

    /// <summary>
    /// The posted work run, and the window laid out and drawn (so the pointer can hit what is in it).
    /// </summary>
    internal static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Runs this on the window's thread as the app does (posted, so the page's background work comes back to the
    /// window's thread, not to the test's), with the posted work it starts.
    /// </summary>
    internal static void OnUi(Action action)
    {
        Dispatcher.UIThread.Post(action);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Clicks the button (on the window's thread) and runs the posted work it starts.
    /// </summary>
    internal static void Click(Button b) => OnUi(() => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

    /// <summary>
    /// Every text block's text under the control, joined with " | ", for asserting on what it shows.
    /// </summary>
    internal static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    /// <summary>
    /// A button of the page by what it shows.
    /// </summary>
    internal static Button ButtonOf(NewWorldPageRun r, string text) =>
        r.P.View.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == text || (b.Content is Control c && Texts(c).Contains(text)));

    /// <summary>
    /// The row of a list beside the map whose name is this, clicked with the mouse.
    /// </summary>
    internal static void ClickRow(NewWorldPageRun r, Control list, string name)
    {
        var row = list.GetLogicalDescendants().OfType<Border>().First(b => b.Cursor != null && Texts(b).StartsWith(name, StringComparison.Ordinal));
        row.BringIntoView();
        Settle();
        var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), r.W)!.Value;
        r.W.MouseDown(at, MouseButton.Left);
        r.W.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task ASeedsWorldIsShownWithItsListsAndLandmarks()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        var p = r.P.Shown!;
        Assert.NotNull(p.Landmarks);
        string info = Texts(r.P.Info);
        Assert.Contains("The world", info);
        Assert.Contains("where the game will put its start temple", info);
        Assert.Contains("Biomes (the nearest from the start)", info);
        Assert.Contains("Meadows", info);
        Assert.Contains("Black Forest", info);
        Assert.Contains("Traders (the nearest spot)", info);
        Assert.Contains("Haldor", info);
        Assert.Contains("Bosses (the nearest altar)", info);
        // Eikthyr's nearest altar about 239 m from the temple, of three.
        Assert.Contains("Eikthyr | 3 altars | 200 m", info);
        // Every landmark over the map, the start last (on top).
        Assert.Equal(p.Landmarks!.Count, r.P.Marks.Children.Count);
        var start = r.P.Marks.Children[^1];
        Assert.StartsWith("The start: ", ToolTip.GetTip(start) as string);
        Assert.Contains(r.P.Marks.Children, c => (ToolTip.GetTip(c) as string ?? "").Contains("One of", StringComparison.Ordinal));
        Assert.Contains(r.P.Marks.Children, c => (ToolTip.GetTip(c) as string ?? "").StartsWith("Eikthyr's altar: ", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task TheSwitchesHideAndShowEachKindOfLandmark()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        var marks = r.P.Shown!.Landmarks!;
        int all = marks.Count;
        r.P.ShowBosses.IsChecked = false;
        Assert.Equal(all - marks.Count(m => m.Kind == "boss"), r.P.Marks.Children.Count);
        r.P.ShowStart.IsChecked = false;
        r.P.ShowHaldor.IsChecked = false;
        r.P.ShowBogWitch.IsChecked = false;
        r.P.ShowHildir.IsChecked = false;
        Assert.Empty(r.P.Marks.Children);
        r.P.ShowHildir.IsChecked = true;
        Assert.Equal(marks.Count(m => m.Name == "Hildir"), r.P.Marks.Children.Count);
        r.P.ShowBogWitch.IsChecked = true;
        Assert.Equal(marks.Count(m => m.Name is "Hildir" or "The Bog Witch"), r.P.Marks.Children.Count);
        // Marks out of the view are hidden: looking closely at the middle leaves far ones out.
        r.P.ShowStart.IsChecked = r.P.ShowHaldor.IsChecked = r.P.ShowBosses.IsChecked = true;
        r.P.Map.LookAt(0, 0, 0.5f);
        Assert.Contains(r.P.Marks.Children, c => !c.IsVisible);
        Assert.Contains(r.P.Marks.Children, c => c.IsVisible);
    }

    [AvaloniaFact]
    public async Task ARowShowsItsPlaceOnTheMapAndTheButtonsZoom()
    {
        using var r = new NewWorldPageRun();
        await r.Look(Seed);
        var s = r.P.Shown!.Stats;
        ClickRow(r, r.P.Info, "The start");
        Assert.Equal((s.Start.X, s.Start.Z, 4f), (r.P.Map.CenterX, r.P.Map.CenterZ, r.P.Map.MetersPerPixel));
        // A boss's row: its nearest altar.
        ClickRow(r, r.P.Info, "Eikthyr");
        var eikthyr = r.P.Shown.Landmarks!.Where(m => m.Name == "Eikthyr").MinBy(m => (m.X - s.Start.X) * (m.X - s.Start.X) + (m.Z - s.Start.Z) * (m.Z - s.Start.Z))!;
        Assert.Equal((eikthyr.X, eikthyr.Z), (r.P.Map.CenterX, r.P.Map.CenterZ));
        // The buttons over the map: closer, farther, all of it.
        Click(ButtonOf(r, "+"));
        Assert.Equal(4 / 1.6f, r.P.Map.MetersPerPixel, 3);
        Click(ButtonOf(r, "−"));
        Assert.Equal(4, r.P.Map.MetersPerPixel, 3);
        Click(ButtonOf(r, "⤢"));
        Assert.Equal((0f, 0f), (r.P.Map.CenterX, r.P.Map.CenterZ));
        // The mouse over the map: its world point at the map's corner.
        var mid = r.P.Map.TranslatePoint(new Point(r.P.Map.Bounds.Width / 3, r.P.Map.Bounds.Height / 2), r.W)!.Value;
        r.W.MouseMove(mid);
        Assert.Matches(@"^-?\d+, -?\d+$", r.P.Readout.Text);
    }
}
