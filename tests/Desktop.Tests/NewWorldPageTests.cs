using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;
using ValheimGen;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page: a seed's world shown with its lists and landmarks (the game's location rules from the test
/// fixtures, as CI has no game), the switches over the map, the rows that show a place, the dungeons found when a kind is
/// switched on, a seed typed or rolled, a search stopped part way and a result picked, the checks Create makes, and the
/// texts for people (distances, biomes, a result's tooltip).
/// </summary>
public class NewWorldPageTests
{
    /// <summary>
    /// A seed whose world the core's tests know: the start at the temple near the middle, three Eikthyr altars.
    /// </summary>
    private const string Seed = "5DCcdIcuYJ";

    /// <summary>
    /// A page in a shown window, with its own world folders (temporary, deleted when disposed), and the worlds it asked
    /// to create.
    /// </summary>
    private sealed class Run : IDisposable
    {
        public NewWorldPage P { get; }
        public Window W { get; }
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "vwe-newworld-" + Guid.NewGuid().ToString("N")[..8]);
        public List<(string Folder, string Name, string Seed)> Created { get; } = [];
        public int Back { get; private set; }

        public Run()
        {
            Fixtures.UseLocations();
            Directory.CreateDirectory(Path.Combine(Root, "local"));
            Directory.CreateDirectory(Path.Combine(Root, "cloud"));
            // Made on the window's thread, as the app makes it: the random seed it shows first comes back there.
            NewWorldPage? page = null;
            OnUi(() => page = new NewWorldPage([Path.Combine(Root, "local"), Path.Combine(Root, "cloud")], previewSize: 64));
            P = page!;
            P.CreateRequested += (folder, name, seed) => Created.Add((folder, name, seed));
            P.BackRequested += () => Back++;
            W = new Window { Width = 1600, Height = 1000, Content = P.View };
            W.Show();
            Settle();
        }

        /// <summary>
        /// Shows this seed at once and waits until its world is shown (the seed box left alone: typing in it would show
        /// the seed again after the typing pause).
        /// </summary>
        public async Task Look(string seed)
        {
            OnUi(() => P.ShowSeed(seed));
            await TestWindows.Until(() => P.Shown?.Seed == seed && !P.Loading.IsVisible, 60000);
            Settle();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            P.Dispose();
            try
            {
                Directory.Delete(Root, true);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// The posted work run, and the window laid out and drawn (so the pointer can hit what is in it).
    /// </summary>
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Runs this on the window's thread as the app does (posted, so the page's background work comes back to the
    /// window's thread, not to the test's), with the posted work it starts.
    /// </summary>
    private static void OnUi(Action action)
    {
        Dispatcher.UIThread.Post(action);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Clicks the button (on the window's thread) and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b) => OnUi(() => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

    /// <summary>
    /// Every text block's text under the control, joined with " | ", for asserting on what it shows.
    /// </summary>
    private static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    /// <summary>
    /// A button of the page by what it shows.
    /// </summary>
    private static Button ButtonOf(Run r, string text) =>
        r.P.View.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == text || (b.Content is Control c && Texts(c).Contains(text)));

    /// <summary>
    /// The row of a list beside the map whose name is this, clicked with the mouse.
    /// </summary>
    private static void ClickRow(Run r, Control list, string name)
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
        using var r = new Run();
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
        using var r = new Run();
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
        using var r = new Run();
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

    [AvaloniaFact]
    public async Task ADungeonKindSwitchedOnIsFoundListedAndDotted()
    {
        using var r = new Run();
        await r.Look(Seed);
        // Looked for at once, out of sight; the list says so until they are found.
        await TestWindows.Until(() => r.P.DungeonMarks != null, 120000);
        Assert.Empty(r.P.Map.Dots);
        var burial = r.P.ShowDungeons["Burial chambers"];
        burial.IsChecked = true;
        int burials = r.P.DungeonMarks!.Count(m => m.Name == "Burial chambers");
        Assert.True(burials > 0);
        Assert.Equal(burials, r.P.Map.Dots.Count);
        string list = Texts(r.P.Info);
        Assert.Contains("Dungeons (the nearest)", list);
        Assert.Contains($"{burials} in the world", list);
        burial.IsChecked = false;
        Assert.Empty(r.P.Map.Dots);
        // A dungeon's row switches its kind on and shows the nearest.
        ClickRow(r, r.P.Info, "Troll caves");
        Assert.True(r.P.ShowDungeons["Troll caves"].IsChecked);
        Assert.Equal(4, r.P.Map.MetersPerPixel);
        Assert.All(r.P.Map.Dots, d => Assert.Equal(((ISolidColorBrush)d.Fill).Color, Avalonia.Media.Color.FromRgb(64, 170, 150)));
    }

    [AvaloniaFact]
    public async Task ADungeonKindOnBeforehandWaitsForTheNextSeedsAndSeenSeedsAreKept()
    {
        using var r = new Run();
        await r.Look(Seed);
        await TestWindows.Until(() => r.P.DungeonMarks != null, 120000);
        var first = r.P.DungeonMarks;
        r.P.ShowDungeons["Frost caves"].IsChecked = true;
        // Another seed: the loading sign says the dungeons are being found, until they are.
        OnUi(() => r.P.ShowSeed("abc"));
        await TestWindows.Until(() => r.P.Shown?.Seed == "abc", 60000);
        Assert.Null(r.P.DungeonMarks);
        Assert.True(r.P.Loading.IsVisible);
        Assert.Equal("Finding abc's dungeons…", r.P.LoadingText.Text);
        Assert.Contains("Finding the dungeons (a few seconds)…", Texts(r.P.Info));
        await TestWindows.Until(() => r.P.DungeonMarks != null && !r.P.Loading.IsVisible, 120000);
        Assert.Equal(r.P.DungeonMarks!.Count(m => m.Name == "Frost caves"), r.P.Map.Dots.Count);
        // Back to the first seed: its dungeons are known at once.
        OnUi(() => r.P.ShowSeed(Seed));
        await TestWindows.Until(() => r.P.Shown?.Seed == Seed, 60000);
        Assert.Same(first, r.P.DungeonMarks);
    }

    [AvaloniaFact]
    public async Task ATypedSeedIsLookedAtAfterAPauseAndABadOneIsSaid()
    {
        using var r = new Run();
        // At once (once the text box has told of the change): the loading sign for the seed typed.
        r.P.SeedBox.Text = "abc";
        Dispatcher.UIThread.RunJobs();
        Assert.True(r.P.Loading.IsVisible);
        Assert.Equal("Looking at abc's world…", r.P.LoadingText.Text);
        await TestWindows.Until(() => r.P.Shown?.Seed == "abc" && !r.P.Loading.IsVisible, 60000);
        // Roll: another random seed of ten characters.
        Click(r.P.Roll);
        Assert.NotEqual("abc", r.P.SeedBox.Text);
        Assert.Equal(10, r.P.SeedBox.Text!.Length);
        // No seed, or one too long: said, nothing shown.
        foreach (string bad in new[] { "", "12345678901" })
        {
            r.P.ShowSeed(bad);
            Assert.Null(r.P.Shown);
            Assert.False(r.P.Loading.IsVisible);
            Assert.Empty(r.P.Marks.Children);
            Assert.Equal("The seed is 1 to 10 characters, as in the game.", Texts(r.P.Info));
        }
    }

    [AvaloniaFact]
    public async Task ASearchStoppedShowsTheBestSoFarAndAResultIsLookedAt()
    {
        using var r = new Run();
        await r.Look("abc");
        r.P.Count.SelectedIndex = 0;
        Click(r.P.Find);
        Assert.False(r.P.Find.IsEnabled);
        Assert.True(r.P.Stop.IsVisible && r.P.Progress.IsVisible);
        await TestWindows.Until(() => r.P.Progress.Value > 0, 120000);
        Click(r.P.Stop);
        await TestWindows.Until(() => r.P.Find.IsEnabled, 120000);
        Assert.False(r.P.Stop.IsVisible || r.P.Progress.IsVisible);
        Assert.NotEmpty(r.P.Results);
        var buttons = r.P.Found.Children.OfType<Button>().ToList();
        Assert.Equal(r.P.Results.Count, buttons.Count);
        var (best, score) = r.P.Results[0];
        Assert.Equal(best.Seed, buttons[0].Tag);
        Assert.Contains($"score {score:0.0}", Texts(buttons[0]));
        Assert.Equal(NewWorldPage.Describe(best.Stats), ToolTip.GetTip(buttons[0]));
        // A result clicked: its seed in the box, looked at at once, its picture framed.
        Click(buttons[0]);
        Assert.Equal(best.Seed, r.P.SeedBox.Text);
        await TestWindows.Until(() => r.P.Shown?.Seed == best.Seed && !r.P.Loading.IsVisible, 60000);
        Assert.Equal(new Thickness(2), buttons[0].BorderThickness);
        Assert.All(buttons.Skip(1), b => Assert.Equal(new Thickness(0), b.BorderThickness));
    }

    [AvaloniaFact]
    public void TheWishesAreTheBoxes()
    {
        using var r = new Run();
        r.P.BigLand.IsChecked = false;
        r.P.LandAround.IsChecked = true;
        r.P.OnFoot.IsChecked = true;
        r.P.Swamp.Value = 1000;
        r.P.Mountain.Value = 0;
        r.P.Plains.Value = 3000;
        r.P.Haldor.Value = 2500;
        r.P.BogWitch.Value = 4000;
        r.P.Hildir.Value = 5000;
        r.P.Bosses.Value = 6000;
        Assert.Equal(new SeedPreview.Wishes(BigStartLand: false, LandAroundStart: true, MaxSwampDistance: 1000, MaxMountainDistance: 0, MaxPlainsDistance: 3000,
            BiomesOnStartLand: true, MaxHaldorDistance: 2500, MaxBogWitchDistance: 4000, MaxHildirDistance: 5000, MaxBossDistance: 6000), r.P.Wishes());
    }

    [AvaloniaFact]
    public async Task ASeedTypedBackBeforeThePauseIsNotLookedAtAgain()
    {
        using var r = new Run();
        await r.Look(Seed);
        var shown = r.P.Shown;
        // Another seed typed: the sign tells of it at once, while the typing pause is waited for.
        OnUi(() => r.P.SeedBox.Text = "abc");
        Assert.True(r.P.Loading.IsVisible);
        // Back to the seed shown before the pause ends: the sign gone, and nothing looked at once the pause is over.
        OnUi(() => r.P.SeedBox.Text = Seed);
        Assert.False(r.P.Loading.IsVisible);
        await Task.Delay(800, TestContext.Current.CancellationToken);
        Settle();
        Assert.False(r.P.Loading.IsVisible);
        Assert.Same(shown, r.P.Shown);
    }

    [AvaloniaFact]
    public void CreateChecksTheNameTheSeedAndThatTheWorldIsNew()
    {
        using var r = new Run();
        string local = Path.Combine(r.Root, "local"), cloud = Path.Combine(r.Root, "cloud");
        r.P.SeedBox.Text = "abc";
        foreach (string bad in new[] { "", "   ", "a/b" })
        {
            r.P.NameBox.Text = bad;
            Click(r.P.Create);
            Assert.StartsWith("Give the world a name that can be a file name", r.P.Problem.Text);
        }
        r.P.NameBox.Text = "Home";
        r.P.SeedBox.Text = "";
        r.P.AskCreate();
        Assert.Equal("The seed is 1 to 10 characters, as in the game.", r.P.Problem.Text);
        // A world of that name already there, as a folder or as the game's .fwl.
        r.P.SeedBox.Text = " abc ";
        Directory.CreateDirectory(Path.Combine(local, "Home"));
        r.P.AskCreate();
        Assert.StartsWith("There is a world called Home already in ", r.P.Problem.Text);
        File.WriteAllText(Path.Combine(cloud, "Home.fwl"), "");
        r.P.Where.SelectedIndex = 1;
        r.P.AskCreate();
        Assert.StartsWith("There is a world called Home already in ", r.P.Problem.Text);
        Assert.Empty(r.Created);
        // A new name: asked for, trimmed, in the folder chosen.
        r.P.NameBox.Text = " New home ";
        r.P.AskCreate();
        Assert.Equal("", r.P.Problem.Text);
        Assert.Equal((Path.Combine(cloud, "New home"), "New home", "abc"), Assert.Single(r.Created));
    }

    [AvaloniaFact]
    public void TheStartButtonStopsWhatRunsAndGoesBack()
    {
        using var r = new Run();
        Click(r.P.Find);
        Assert.False(r.P.Find.IsEnabled);
        Click(ButtonOf(r, "Start"));
        Assert.Equal(1, r.Back);
        Assert.False(r.P.Loading.IsVisible);
        // The world folders offered: the ones given, ~ for the home folder.
        Assert.Equal(2, ((List<string>)r.P.Where.Tag!).Count);
    }

    [AvaloniaFact]
    public void WithoutWorldFoldersTheGamesFirstIsOffered()
    {
        using var page = new NewWorldPage([], previewSize: 32);
        var roots = (List<string>)page.Where.Tag!;
        Assert.Equal(Places.WorldRoots().First(), Assert.Single(roots));
        Assert.Equal(0, page.Where.SelectedIndex);
        Assert.Equal(10, page.SeedBox.Text!.Length);
    }

    [Fact]
    public void DistancesAndBiomesAreSaidForPeople()
    {
        Assert.Equal("none", NewWorldPage.Km(float.PositiveInfinity));
        Assert.Equal("400 m", NewWorldPage.Km(430));
        Assert.Equal("1.2 km", NewWorldPage.Km(1234));
        Assert.Equal("Black Forest", NewWorldPage.Name(Heightmap.Biome.BlackForest));
        Assert.Equal("Ashlands", NewWorldPage.Name(Heightmap.Biome.AshLands));
        Assert.Equal("Deep North", NewWorldPage.Name(Heightmap.Biome.DeepNorth));
        Assert.Equal("Swamp", NewWorldPage.Name(Heightmap.Biome.Swamp));
    }

    [Fact]
    public void AResultsTooltipDescribesItsWorld()
    {
        var shares = SeedPreview.Biomes.ToDictionary(b => b, _ => 0f);
        shares[Heightmap.Biome.Meadows] = 0.6f;
        shares[Heightmap.Biome.Swamp] = 0.4f;
        var distance = SeedPreview.Biomes.ToDictionary(b => b, _ => 800f);
        distance[Heightmap.Biome.Mountain] = float.PositiveInfinity;
        var same = SeedPreview.Biomes.ToDictionary(b => b, _ => true);
        same[Heightmap.Biome.Swamp] = false;
        var s = new SeedPreview.Stats(0.3f, shares, 0.5f, (120, -40), 0.7f, 0.9f, distance, same);
        string text = NewWorldPage.Describe(s);
        Assert.Contains("Land: 30 % of the world · the start's landmass: 90 % of the land (largest: 50 %) · land around the start: 70 %", text);
        Assert.Contains("Biomes: Meadows 60 %, Swamp 40 %", text);
        Assert.Contains("Swamp 800 m (by sea)", text);
        Assert.Contains("Mountain none", text);
        Assert.DoesNotContain("Ashlands", text.Split('\n')[2]);
        Assert.Contains("The start (red dot) is likely near 120, -40", text);
        Assert.DoesNotContain("Traders", text);
        // With the temple's start and the landmarks' distances.
        var nearest = new Dictionary<string, float> { ["Haldor"] = 2500, ["Eikthyr"] = 300 };
        text = NewWorldPage.Describe(s with { TempleStart = true, Nearest = nearest });
        Assert.Contains("where the game will put its start temple", text);
        Assert.Contains("Haldor 2.5 km, the Bog Witch none, Hildir none", text);
        Assert.Contains("Eikthyr 300 m", text);
    }

    [AvaloniaFact]
    public void APreviewIsDrawnSmallWithItsStart()
    {
        var p = SeedPreview.Make("abc", 64, cancel: TestContext.Current.CancellationToken);
        using var bmp = NewWorldPage.Draw(p, 40);
        Assert.Equal(new PixelSize(40, 40), bmp.PixelSize);
        using var plain = NewWorldPage.Draw(p, 16, startDot: false);
        Assert.Equal(new PixelSize(16, 16), plain.PixelSize);
    }
}
