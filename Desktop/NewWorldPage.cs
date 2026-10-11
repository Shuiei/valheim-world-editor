using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// A new world (the start page's "A new world"): a name and a seed, the seed's world seen from above before it exists
/// (SeedPreview: the game's own generator, with where it will put the start, the bosses' altars and the traders), a
/// search through random seeds for one that suits (the start on a large landmass, the first Swamp or Mountain close,
/// Haldor near...), and Create: a normal Valheim world in the game's world folder. The game lays out its locations when
/// it first loads it, and generates its zones as players explore them; the editor edits what is generated.
/// </summary>
public sealed class NewWorldPage : IDisposable
{
    /// <summary>
    /// The page.
    /// </summary>
    public Control View { get; }

    /// <summary>
    /// The Start button: back to the start page (what runs is stopped first).
    /// </summary>
    public event Action? BackRequested;

    /// <summary>
    /// Create, once checked: the world's folder (inside a world folder of the game), its name and seed.
    /// </summary>
    public event Action<string, string, string>? CreateRequested;

    /// <summary>
    /// The world's name; it must be a valid file name.
    /// </summary>
    internal TextBox NameBox { get; } = new() { Text = "", PlaceholderText = "My world", FontSize = 13 };
    /// <summary>
    /// The seed, up to 10 characters; its world is shown after a short typing pause.
    /// </summary>
    internal TextBox SeedBox { get; } = new() { FontSize = 13, MaxLength = 10, PlaceholderText = "Up to 10 letters or digits" };
    /// <summary>
    /// Puts a random seed in the seed box.
    /// </summary>
    internal Button Roll { get; } = new() { Content = "Roll", FontSize = 12 };
    /// <summary>
    /// Which of the game's world folders to create the world in; its <c>Tag</c> holds the full paths.
    /// </summary>
    internal ComboBox Where { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
    /// <summary>
    /// Checks the name and seed, then asks the window to create the world.
    /// </summary>
    internal Button Create { get; } = new Button { Content = "Create world", FontSize = 13 }.Classed("primary");
    /// <summary>
    /// Why Create refused, or empty.
    /// </summary>
    internal TextBlock Problem { get; } = new() { Foreground = new SolidColorBrush(Avalonia.Media.Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    /// <summary>
    /// The seed's map, the landmarks over it, and which kinds show (switches over the map's corner, as the editor's
    /// View panel).
    /// </summary>
    internal SeedMap Map { get; } = new();
    /// <summary>
    /// The landmarks over the map.
    /// </summary>
    internal Canvas Marks { get; } = new() { ClipToBounds = true };
    /// <summary>
    /// Show the start.
    /// </summary>
    internal CheckBox ShowStart { get; } = Toggle("start", "", "The start");
    /// <summary>
    /// Show the bosses' altars.
    /// </summary>
    internal CheckBox ShowBosses { get; } = Toggle("boss", "", "Bosses' altars");
    /// <summary>
    /// Show Haldor's spots.
    /// </summary>
    internal CheckBox ShowHaldor { get; } = Toggle("trader", "Haldor", "Haldor");
    /// <summary>
    /// Show the Bog Witch's spots.
    /// </summary>
    internal CheckBox ShowBogWitch { get; } = Toggle("trader", "The Bog Witch", "Bog Witch");
    /// <summary>
    /// Show Hildir's spots.
    /// </summary>
    internal CheckBox ShowHildir { get; } = Toggle("trader", "Hildir", "Hildir");
    /// <summary>
    /// The landmarks placed over the map, with their controls, so they can be moved when the view does.
    /// </summary>
    private readonly List<(SeedPreview.Landmark Mark, Control Control)> _marks = [];
    /// <summary>
    /// One switch a kind of dungeon, by name (off at first: they are looked for only when one is on, a few seconds).
    /// </summary>
    internal Dictionary<string, CheckBox> ShowDungeons { get; } = SeedPreview.Dungeons.ToDictionary(d => d.Name, d => DungeonToggle(d.Name));
    /// <summary>
    /// The shown seed's dungeons (null: not looked for yet).
    /// </summary>
    internal List<SeedPreview.Landmark>? DungeonMarks { get; private set; }
    /// <summary>
    /// The dungeons of the seeds seen, by seed (cleared past 20).
    /// </summary>
    private readonly Dictionary<string, List<SeedPreview.Landmark>> _dungeonsSeen = [];
    /// <summary>
    /// The Dungeons list beside the map.
    /// </summary>
    private readonly StackPanel _dungeonList = new() { Spacing = 2 };
    /// <summary>
    /// The world point under the mouse, at the map's bottom left.
    /// </summary>
    internal TextBlock Readout { get; } = new() { FontSize = 11, Foreground = Ui.Muted, Margin = new Thickness(8, 0, 0, 6), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    /// <summary>
    /// Shown over the map while a seed's world is being looked at: what is being done.
    /// </summary>
    internal TextBlock LoadingText { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
    /// <summary>
    /// The loading sign over the map.
    /// </summary>
    internal Border Loading { get; }
    /// <summary>
    /// What the seed's world holds, as lists.
    /// </summary>
    internal StackPanel Info { get; } = new() { Spacing = 14 };
    // Finding a seed.
    /// <summary>
    /// Wish: the start on a large landmass.
    /// </summary>
    internal CheckBox BigLand { get; } = new() { Content = "The start on a large landmass", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Wish: land all around the start.
    /// </summary>
    internal CheckBox LandAround { get; } = new() { Content = "Land all around the start", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Wish: each biome reachable on foot from the start.
    /// </summary>
    internal CheckBox OnFoot { get; } = new() { Content = "Each biome reachable on foot from the start", FontSize = 12 };
    /// <summary>
    /// Wish: a Swamp within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Swamp { get; } = Near(1500);
    /// <summary>
    /// Wish: a Mountain within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Mountain { get; } = Near(2000);
    /// <summary>
    /// Wish: Plains within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Plains { get; } = Near(0);
    /// <summary>
    /// Wish: one of Haldor's spots within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Haldor { get; } = Near(0);
    /// <summary>
    /// Wish: one of the Bog Witch's spots within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown BogWitch { get; } = Near(0);
    /// <summary>
    /// Wish: one of Hildir's spots within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Hildir { get; } = Near(0);
    /// <summary>
    /// Wish: the first five bosses' altars within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Bosses { get; } = Near(0);
    /// <summary>
    /// How many random seeds to look at: 60, 150 or 400.
    /// </summary>
    internal ComboBox Count { get; } = new() { ItemsSource = new[] { "60 seeds", "150 seeds", "400 seeds" }, SelectedIndex = 1, FontSize = 12 };
    /// <summary>
    /// Starts the search.
    /// </summary>
    internal Button Find { get; } = new Button { Content = "Find seeds", FontSize = 12 }.Classed("primary");
    /// <summary>
    /// Stops the search; the best so far are shown.
    /// </summary>
    internal Button Stop { get; } = new Button { Content = "Stop", FontSize = 12, IsVisible = false }.Classed("primary");
    /// <summary>
    /// The search's progress, 0..1.
    /// </summary>
    internal ProgressBar Progress { get; } = new() { Minimum = 0, Maximum = 1, Height = 6, MinHeight = 6, IsVisible = false };
    /// <summary>
    /// The search's results, as pictures to click.
    /// </summary>
    internal WrapPanel Found { get; } = new() { ItemSpacing = 6, LineSpacing = 6 };

    /// <summary>
    /// The preview shown (null while it is made).
    /// </summary>
    internal SeedPreview.Preview? Shown { get; private set; }
    /// <summary>
    /// The search's results, best first, with their scores.
    /// </summary>
    internal List<(SeedPreview.Preview Preview, float Score)> Results { get; private set; } = [];
    /// <summary>
    /// The latest preview or search started, for tests to wait on.
    /// </summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Cancel the search, the seed preview, and the dungeon finding.
    /// </summary>
    private CancellationTokenSource? _search, _preview, _dungeons;
    /// <summary>
    /// A dungeon kind was switched on while they are found: the loading sign stays until they are.
    /// </summary>
    private bool _dungeonWait;
    /// <summary>
    /// Why finding the dungeons failed, or null.
    /// </summary>
    private string? _dungeonError;
    /// <summary>
    /// The typing pause before a typed seed is looked at.
    /// </summary>
    private readonly DispatcherTimer _wait = new() { Interval = TimeSpan.FromMilliseconds(350) };
    /// <summary>
    /// The seed's map, in cells on a side.
    /// </summary>
    private readonly int _previewSize;
    /// <summary>
    /// The seed last asked for (shown or being made).
    /// </summary>
    private string? _asked;
    /// <summary>
    /// The latest seed preview started (done once it is shown, failed or dropped).
    /// </summary>
    private Task _making = Task.CompletedTask;

    /// <summary>
    /// A distance box: 0..8000 m in steps of 250.
    /// </summary>
    private static NumericUpDown Near(double metres) => new()
    {
        Minimum = 0,
        Maximum = 8000,
        Increment = 250,
        Value = (decimal)metres,
        FormatString = "0",
        Width = 110,
        FontSize = 12,
    };

    /// <summary>
    /// The game's world folders (that exist, the local ones first), for Where.
    /// </summary>
    internal static List<string> Roots() => [.. Places.WorldRoots().Where(Directory.Exists).Distinct()];

    /// <summary>
    /// Builds the page and starts looking at a random seed.
    /// </summary>
    /// <param name="roots">The world folders to offer (tests); null for the game's own. With none, the first the game
    /// would use.</param>
    /// <param name="previewSize">The seed's map, in cells on a side (tests: smaller is quicker).</param>
    public NewWorldPage(IReadOnlyList<string>? roots = null, int previewSize = 512)
    {
        _previewSize = previewSize;
        Loading = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            IsHitTestVisible = false,
            Child = new StackPanel { Spacing = 8, Children = { LoadingText, new ProgressBar { IsIndeterminate = true, Width = 220, Height = 4, MinHeight = 4 } } },
        };
        var where = (roots ?? Roots()).ToList();
        if (where.Count == 0)
        {
            where.Add(Places.WorldRoots().First());
        }
        Where.ItemsSource = where.Select(Ui.Tilde).ToList();
        Where.Tag = where;
        Where.SelectedIndex = 0;
        SeedBox.Text = SeedPreview.RandomSeed();
        var back = new Button { Content = Icons.With("back", "Start"), FontSize = 12 }.Classed("ghost").Tip("newWorld.back");
        back.Click += (_, _) => { StopAll(); BackRequested?.Invoke(); };
        NameBox.Tip("newWorld.name");
        SeedBox.Tip("newWorld.seed");
        Roll.Tip("newWorld.roll");
        Where.Tip("newWorld.where");
        Create.Tip("newWorld.create");
        BigLand.Tip("newWorld.bigLand");
        LandAround.Tip("newWorld.landAround");
        OnFoot.Tip("newWorld.onFoot");
        Swamp.Tip("newWorld.near");
        Mountain.Tip("newWorld.near");
        Plains.Tip("newWorld.near");
        Haldor.Tip("newWorld.trader");
        BogWitch.Tip("newWorld.trader");
        Hildir.Tip("newWorld.trader");
        Bosses.Tip("newWorld.bosses");
        ShowStart.Tip("newWorld.showStart");
        ShowBosses.Tip("newWorld.showBosses");
        foreach (var t in new[] { ShowHaldor, ShowBogWitch, ShowHildir })
        {
            t.Tip("newWorld.showTrader");
        }
        foreach (var t in ShowDungeons.Values)
        {
            t.Tip("newWorld.showDungeons");
            t.IsCheckedChanged += (_, _) =>
            {
                if (t.IsChecked == true && DungeonMarks == null && Shown != null)
                {
                    // Already being looked for (since the seed was shown): only waited for.
                    if (_dungeons == null)
                    {
                        FindDungeons(Shown);
                    }
                    WaitForDungeons(Shown);
                }
                PlaceDots();
            };
        }
        foreach (var t in new[] { ShowStart, ShowBosses, ShowHaldor, ShowBogWitch, ShowHildir })
        {
            t.IsCheckedChanged += (_, _) => PlaceMarks();
        }
        Map.ViewChanged += MoveMarks;
        Map.Hovered += (x, z) => Readout.Text = $"{x:0}, {z:0}";
        Find.Tip("newWorld.find");
        Roll.Click += (_, _) => SeedBox.Text = SeedPreview.RandomSeed();
        SeedBox.TextChanged += (_, _) =>
        {
            // The box told of a seed already asked for (the text box tells of a change after it is made: the first
            // seed, a search result clicked): not looked at again.
            if ((SeedBox.Text?.Trim() ?? "") == _asked)
            {
                // Typed back to it before the pause ended: the wait dropped (else it looks at it again), and the sign
                // as it was for that seed.
                if (_wait.IsEnabled)
                {
                    _wait.Stop();
                    if (Shown?.Seed == _asked)
                    {
                        SetLoading(null);
                        MarkFound(_asked);
                    }
                    else if (_making.IsCompleted)
                    {
                        // Its preview failed (or was dropped): nothing left to take the sign away, so made again.
                        ShowSeed(_asked);
                    }
                    else
                    {
                        SetLoading(_asked);
                    }
                }
                return;
            }
            _wait.Stop();
            _wait.Start();
            // At once, while the typing pause is waited for: the change is seen.
            SetLoading(SeedBox.Text?.Trim() ?? "");
        };
        _wait.Tick += (_, _) => { _wait.Stop(); ShowSeed(SeedBox.Text?.Trim() ?? ""); };
        Create.Click += (_, _) => AskCreate();
        Find.Click += (_, _) => StartSearch();
        Stop.Click += (_, _) => _search?.Cancel();
        static Control Row(string label, Control c) => new DockPanel { Children = { new TextBlock { Text = label, Width = 90, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, c } };
        static Control Metres(string label, NumericUpDown n) => new DockPanel { Children = { new TextBlock { Text = label, Width = 120, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { n, new TextBlock { Text = "m", VerticalAlignment = VerticalAlignment.Center } } } } };
        var seedRow = new DockPanel { Children = { new TextBlock { Text = "Seed", Width = 90, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Roll, SeedBox } };
        DockPanel.SetDock(Roll, Dock.Right);
        Roll.Margin = new Thickness(6, 0, 0, 0);
        var world = Ui.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "The world", FontSize = 15, FontWeight = FontWeight.SemiBold },
                Row("Name", NameBox),
                seedRow,
                Row("Saved in", Where),
                Ui.Hint("A normal Valheim world, as the game makes them: players need no mod, console players included. The game lays out its start, traders and dungeons the first time it loads it, and makes each place as players reach it. Play it (or walk around) before reshaping an area in the editor: only the places the game has made can be edited."),
                Create,
                Problem,
            },
        });
        var findRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Count, Find, Stop } };
        var search = Ui.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Find a seed", FontSize = 15, FontWeight = FontWeight.SemiBold },
                Ui.Hint("Random seeds are looked at with the game's own generator, and the best kept. 0: no wish."),
                BigLand, LandAround, OnFoot,
                Metres("Swamp within", Swamp),
                Metres("Mountain within", Mountain),
                Metres("Plains within", Plains),
                Metres("Haldor within", Haldor),
                Metres("Bog Witch within", BogWitch),
                Metres("Hildir within", Hildir),
                Metres("Bosses 1–5 within", Bosses),
                findRow,
                Progress,
                new ScrollViewer { MaxHeight = 330, Content = Found },
            },
        });
        var left = new ScrollViewer { Content = new StackPanel { Spacing = 10, Children = { world, search } }, Padding = new Thickness(0, 0, 10, 0) };
        static Button ZoomButton(string text, string tip) => new Button { Content = text, FontSize = 13, Width = 30, Height = 30, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center }.Tip(tip);
        var zoomIn = ZoomButton("+", "newWorld.zoomIn");
        var zoomOut = ZoomButton("−", "newWorld.zoomOut");
        var fit = ZoomButton("⤢", "newWorld.fit");
        zoomIn.Click += (_, _) => Map.Zoom(1 / 1.6);
        zoomOut.Click += (_, _) => Map.Zoom(1.6);
        fit.Click += (_, _) => Map.Fit();
        var toggles = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "On the map", FontSize = 12, FontWeight = FontWeight.SemiBold },
                    ShowStart, ShowBosses, ShowHaldor, ShowBogWitch, ShowHildir,
                    new TextBlock { Text = "Dungeons", FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) },
                },
            },
        };
        foreach (var t in ShowDungeons.Values)
        {
            ((StackPanel)toggles.Child!).Children.Add(t);
        }
        var zoom = new StackPanel { Spacing = 4, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Children = { zoomIn, zoomOut, fit } };
        var map = new Border
        {
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = new Panel { Children = { Map, Marks, Readout, toggles, zoom, Loading } },
        };
        var info = new ScrollViewer { Content = Info, Padding = new Thickness(0, 0, 8, 0) };
        var right = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,320") };
        right.Children.Add(map);
        Grid.SetColumn(info, 2);
        right.Children.Add(info);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("440,14,*") };
        body.Children.Add(left);
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(20), RowSpacing = 12 };
        page.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { back, new TextBlock { Text = "A new world", FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } });
        Grid.SetRow(body, 1);
        page.Children.Add(body);
        View = page;
        ShowSeed(SeedBox.Text);
    }

    /// <summary>
    /// Everything running stopped, and the page left as it is now (no loading sign left waiting for work that will not
    /// finish).
    /// </summary>
    internal void StopAll()
    {
        _search?.Cancel();
        _preview?.Cancel();
        StopDungeons();
        Loading.IsVisible = false;
        FillDungeons();
    }

    /// <summary>
    /// Leaving the page: what runs is stopped.
    /// </summary>
    public void Dispose()
    {
        StopAll();
        _wait.Stop();
        Map.Dispose();
        _search?.Dispose();
        _preview?.Dispose();
        _dungeons?.Dispose();
    }

    /// <summary>
    /// The seed's preview, made away from the window's thread (only the newest is shown).
    /// </summary>
    internal void ShowSeed(string seed)
    {
        _asked = seed;
        _preview?.Cancel();
        Problem.Text = "";
        if (seed.Length is 0 or > 10)
        {
            Note("The seed is 1 to 10 characters, as in the game.");
            SetLoading(null);
            Map.Clear();
            Marks.Children.Clear();
            Shown = null;
            return;
        }
        var cancel = _preview = new CancellationTokenSource();
        StopDungeons();
        SetLoading(seed);
        if (Shown == null)
        {
            Note("Looking at the world…");
        }
        // The game's own location rules first (read once, the first time in a few seconds).
        var rules = GameLocations.UseGameRules(GameLook.Bundles);
        Pending = _making = Task.Run(async () =>
        {
            await rules.ConfigureAwait(false);
            return SeedPreview.Make(seed, _previewSize, parallel: true, landmarks: true, cancel.Token);
        }, cancel.Token).ContinueWith(t =>
        {
            // A newer seed asked for since: that one's answer will come.
            if (cancel.IsCancellationRequested)
            {
                return;
            }
            SetLoading(null);
            if (t.Status != TaskStatus.RanToCompletion)
            {
                Note($"The seed could not be looked at: {t.Exception?.GetBaseException().Message}");
                Console.WriteLine($"New world: seed {seed} could not be looked at: {t.Exception?.GetBaseException()}");
                return;
            }
            Show(t.Result);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// The loading sign over the map (null: none), and the search result for that seed marked.
    /// </summary>
    private void SetLoading(string? seed)
    {
        bool valid = seed is { Length: > 0 and <= 10 };
        Loading.IsVisible = valid;
        if (valid)
        {
            LoadingText.Text = $"Looking at {seed}'s world…";
            MarkFound(seed);
        }
    }

    /// <summary>
    /// The search result for this seed framed (the one chosen or shown), the others not.
    /// </summary>
    private void MarkFound(string? seed)
    {
        foreach (var b in Found.Children.OfType<Button>())
        {
            bool chosen = Equals(b.Tag, seed);
            b.BorderBrush = chosen ? Ui.Accent : null;
            b.BorderThickness = new Thickness(chosen ? 2 : 0);
        }
    }

    /// <summary>
    /// Shows a made preview: the map (its view kept for the same seed), the lists and the marks; its dungeons are
    /// looked for unless known.
    /// </summary>
    private void Show(SeedPreview.Preview p)
    {
        bool same = Shown?.Seed == p.Seed;
        Shown = p;
        MarkFound(p.Seed);
        Map.Show(p, keepView: same);
        DungeonMarks = _dungeonsSeen.GetValueOrDefault(p.Seed);
        ShowInfo(p);
        PlaceMarks();
        PlaceDots();
        // Looked for at once, out of sight (so a switch turned on later shows them at once, or sooner),
        // with the loading sign when a kind is switched on already.
        if (DungeonMarks == null)
        {
            FindDungeons(p);
            if (ShowDungeons.Values.Any(t => t.IsChecked == true))
            {
                WaitForDungeons(p);
            }
        }
    }

    /// <summary>
    /// A dungeon kind switched on before they are found: the loading sign until they are.
    /// </summary>
    private void WaitForDungeons(SeedPreview.Preview p)
    {
        if (_dungeons == null)
        {
            return;
        }
        _dungeonWait = true;
        Loading.IsVisible = true;
        LoadingText.Text = $"Finding {p.Seed}'s dungeons…";
    }

    /// <summary>
    /// The seed's dungeons looked for (a few seconds, on one core: the game's smaller locations come first in its
    /// order; what the preview laid out is gone on from), kept for the seed.
    /// </summary>
    private void FindDungeons(SeedPreview.Preview p)
    {
        StopDungeons();
        _dungeonError = null;
        if (p.Layout is not { } layout || p.Landmarks == null)
        {
            return;
        }
        var cancel = _dungeons = new CancellationTokenSource();
        FillDungeons();
        Task.Run(() => SeedPreview.FindDungeons(layout, cancel.Token), cancel.Token).ContinueWith(t =>
        {
            if (cancel.IsCancellationRequested || Shown != p)
            {
                return;
            }
            cancel.Dispose();
            _dungeons = null;
            if (_dungeonWait)
            {
                _dungeonWait = false;
                Loading.IsVisible = false;
            }
            if (t.Status != TaskStatus.RanToCompletion)
            {
                // Said in the list, not kept: switching a kind on again tries again.
                _dungeonError = t.Exception?.GetBaseException().Message ?? "stopped";
                Console.WriteLine($"The dungeons of {p.Seed} could not be found: {t.Exception?.GetBaseException()}");
                FillDungeons();
                return;
            }
            _dungeonError = null;
            DungeonMarks = t.Result;
            Console.WriteLine($"New world: {DungeonMarks.Count} dungeons found for {p.Seed}.");
            if (_dungeonsSeen.Count >= 20)
            {
                _dungeonsSeen.Clear();
            }
            _dungeonsSeen[p.Seed] = DungeonMarks;
            FillDungeons();
            PlaceDots();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Stops looking for dungeons.
    /// </summary>
    private void StopDungeons()
    {
        _dungeonWait = false;
        _dungeons?.Cancel();
        _dungeons?.Dispose();
        _dungeons = null;
    }

    /// <summary>
    /// The dungeons the switches show, as dots on the map.
    /// </summary>
    private void PlaceDots() => Map.Dots = DungeonMarks == null
        ? Array.Empty<(float, float, IBrush)>()
        : DungeonMarks.Where(m => ShowDungeons[m.Name].IsChecked == true).Select(m => (m.X, m.Z, (IBrush)DungeonFill(m.Name))).ToList();

    /// <summary>
    /// The Dungeons list: each kind's nearest, how many in the world and near the start.
    /// </summary>
    private void FillDungeons()
    {
        _dungeonList.Children.Clear();
        _dungeonList.Children.Add(new TextBlock { Text = "Dungeons (the nearest)", FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        if (Shown?.Landmarks == null)
        {
            return;
        }
        if (DungeonMarks == null && _dungeons == null)
        {
            if (_dungeonError != null)
            {
                _dungeonList.Children.Add(Ui.Hint($"The dungeons could not be found ({_dungeonError}). Switch a kind on to try again."));
            }
            return;
        }
        if (DungeonMarks == null)
        {
            _dungeonList.Children.Add(new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(6, 2, 0, 0),
                Children = { Ui.Hint("Finding the dungeons (a few seconds)…"), new ProgressBar { IsIndeterminate = true, Height = 4, MinHeight = 4, Width = 200, HorizontalAlignment = HorizontalAlignment.Left } },
            });
            return;
        }
        var (x, z) = Shown.Stats.Start;
        float Far(SeedPreview.Landmark m) => MathF.Sqrt((m.X - x) * (m.X - x) + (m.Z - z) * (m.Z - z));
        foreach (var (name, biome, _) in SeedPreview.Dungeons)
        {
            var all = DungeonMarks.Where(m => m.Name == name).ToList();
            var near = all.OrderBy(Far).FirstOrDefault();
            int close = all.Count(m => Far(m) <= 2000);
            string where = biome == Heightmap.Biome.None ? "" : $"{Name(biome)} · ";
            _dungeonList.Children.Add(Item(DungeonMark(name), name, near == null ? "none" : Km(Far(near)), $"{where}{all.Count} in the world, {close} within 2 km",
                near == null ? null : () =>
                {
                    ShowDungeons[name].IsChecked = true;
                    Map.LookAt(near.X, near.Z);
                }));
        }
    }

    /// <summary>
    /// Each dungeon kind's colour (squares, apart from the round traders and the bosses' diamonds).
    /// </summary>
    private static SolidColorBrush DungeonFill(string name) => new(name switch
    {
        "Burial chambers" => Avalonia.Media.Color.FromRgb(226, 206, 156),
        "Troll caves" => Avalonia.Media.Color.FromRgb(64, 170, 150),
        "Sunken crypts" => Avalonia.Media.Color.FromRgb(160, 116, 78),
        "Frost caves" => Avalonia.Media.Color.FromRgb(132, 210, 255),
        "Infested mines" => Avalonia.Media.Color.FromRgb(104, 120, 255),
        "Charred fortresses" => Avalonia.Media.Color.FromRgb(255, 126, 44),
        _ => Avalonia.Media.Color.FromRgb(255, 176, 214),
    });

    /// <summary>
    /// A dungeon kind's square mark.
    /// </summary>
    private static Border DungeonMark(string name) => new() { Width = 8, Height = 8, Background = DungeonFill(name), BorderBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(20, 20, 24)), BorderThickness = new Thickness(1) };

    /// <summary>
    /// A switch for one dungeon kind, off at first.
    /// </summary>
    private static CheckBox DungeonToggle(string name) => new CheckBox
    {
        IsChecked = false,
        FontSize = 12.5,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { DungeonMark(name), new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center } } },
    }.Classed("switch");

    /// <summary>
    /// Replaces the lists beside the map with one line.
    /// </summary>
    private void Note(string text)
    {
        Info.Children.Clear();
        Info.Children.Add(Ui.Hint(text));
    }

    /// <summary>
    /// The lists beside the map: the world, its biomes, the traders, the bosses. A trader's or boss's row shows the
    /// nearest on the map.
    /// </summary>
    private void ShowInfo(SeedPreview.Preview p)
    {
        var s = p.Stats;
        Info.Children.Clear();
        Info.Children.Add(Section("The world",
            Item(null, "Land", $"{s.Land * 100:0} % of the world"),
            Item(null, "The start's landmass", $"{s.StartContinent * 100:0} % of the land"),
            Item(null, "The largest landmass", $"{s.MainContinent * 100:0} % of the land"),
            Item(null, "Land around the start", $"{s.LandNearStart * 100:0} %"),
            Item(Mark("start"), "The start", $"{s.Start.X:0}, {s.Start.Z:0}", s.TempleStart ? "where the game will put its start temple" : "likely: the game puts it in Meadows close to the middle", () => Map.LookAt(s.Start.X, s.Start.Z))));
        var biomes = SeedPreview.Biomes.Where(b => s.Shares[b] > 0.0005f || b == Heightmap.Biome.Meadows).Select(b =>
        {
            var (r, g, bl) = MapData.PixelColor(b);
            var swatch = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(r, g, bl)) };
            string nearest = b == Heightmap.Biome.Meadows ? "" : Km(s.Distance[b]) + (s.SameLand[b] || float.IsPositiveInfinity(s.Distance[b]) ? "" : " by sea");
            return Item(swatch, Name(b), nearest, $"{s.Shares[b] * 100:0} % of the land");
        }).ToArray();
        Info.Children.Add(Section("Biomes (the nearest from the start)", biomes));
        if (p.Landmarks is not { } marks)
        {
            Info.Children.Add(Ui.Hint("The bosses and traders could not be read from the game's files (the log says why): they are not shown, and the search cannot look for them."));
            return;
        }
        Control Of(string kind, string name)
        {
            var all = marks.Where(m => m.Name == name).ToList();
            var near = all.OrderBy(m => (m.X - s.Start.X) * (m.X - s.Start.X) + (m.Z - s.Start.Z) * (m.Z - s.Start.Z)).FirstOrDefault();
            string value = near == null ? "none" : Km(MathF.Sqrt((near.X - s.Start.X) * (near.X - s.Start.X) + (near.Z - s.Start.Z) * (near.Z - s.Start.Z)));
            string detail = all.Count == 0 ? "" : kind == "trader" ? $"{all.Count} spots" : all.Count == 1 ? "1 altar" : $"{all.Count} altars";
            return Item(Mark(kind, name), name, value, detail, near == null ? null : () => Map.LookAt(near.X, near.Z));
        }
        Info.Children.Add(Section("Traders (the nearest spot)", [.. SeedPreview.Landmarks.Where(l => l.Kind == "trader").Select(l => Of("trader", l.Name))]));
        Info.Children.Add(Ui.Hint("Each trader has several spots: the first one players come near becomes the trader's camp, the others vanish."));
        Info.Children.Add(Section("Bosses (the nearest altar)", [.. SeedPreview.Landmarks.Where(l => l.Kind == "boss").Select(l => Of("boss", l.Name))]));
        (_dungeonList.Parent as Panel)?.Children.Remove(_dungeonList);
        Info.Children.Add(_dungeonList);
    }

    /// <summary>
    /// A titled list of rows.
    /// </summary>
    private static StackPanel Section(string title, params Control[] items)
    {
        var list = new StackPanel { Spacing = 2 };
        list.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var c in items)
        {
            list.Children.Add(c);
        }
        return list;
    }

    /// <summary>
    /// One row: a mark, the name, the value, a detail under it; show: what clicking it does.
    /// </summary>
    private static Border Item(Control? mark, string name, string value, string detail = "", Action? show = null)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*,Auto") };
        if (mark != null)
        {
            mark.VerticalAlignment = VerticalAlignment.Center;
            mark.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(mark);
        }
        var label = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = name, FontSize = 12.5 } } };
        if (detail.Length > 0)
        {
            label.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
        }
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        var v = new TextBlock { Text = value, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(v, 2);
        row.Children.Add(v);
        var item = new Border { Child = row, Padding = new Thickness(6, 4), CornerRadius = new CornerRadius(5), Background = Brushes.Transparent };
        if (show != null)
        {
            item.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(item, "Show it on the map");
            item.PointerEntered += (_, _) => item.Background = Ui.Panel2;
            item.PointerExited += (_, _) => item.Background = Brushes.Transparent;
            item.PointerPressed += (_, _) => show();
        }
        return item;
    }

    /// <summary>
    /// A switch for one kind of mark, with its mark beside the name.
    /// </summary>
    private static CheckBox Toggle(string kind, string name, string text) => new CheckBox
    {
        IsChecked = true,
        FontSize = 12.5,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Mark(kind, name), new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } } },
    }.Classed("switch");

    /// <summary>
    /// The landmark kinds' looks: the start red, bosses purple diamonds, each trader a colour of its own.
    /// </summary>
    private static SolidColorBrush Fill(string kind, string name = "") => new(kind switch
    {
        "start" => Avalonia.Media.Color.FromRgb(230, 40, 40),
        "boss" => Avalonia.Media.Color.FromRgb(176, 96, 232),
        _ => name switch
        {
            "The Bog Witch" => Avalonia.Media.Color.FromRgb(120, 206, 92),
            "Hildir" => Avalonia.Media.Color.FromRgb(244, 120, 176),
            _ => Avalonia.Media.Color.FromRgb(244, 196, 64),
        },
    });

    /// <summary>
    /// A landmark's mark: a diamond for a boss, a dot otherwise (the start's larger, outlined white).
    /// </summary>
    private static Control Mark(string kind, string name = "")
    {
        var outline = new SolidColorBrush(Avalonia.Media.Color.FromRgb(20, 20, 24));
        return kind == "boss"
            ? new Border { Width = 9, Height = 9, Background = Fill(kind), BorderBrush = outline, BorderThickness = new Thickness(1), RenderTransform = new RotateTransform(45) }
            : new Avalonia.Controls.Shapes.Ellipse { Width = kind == "start" ? 11 : 9, Height = kind == "start" ? 11 : 9, Fill = Fill(kind, name), Stroke = kind == "start" ? Brushes.White : outline, StrokeThickness = kind == "start" ? 2 : 1 };
    }

    /// <summary>
    /// The shown seed's landmarks over the map (the kinds switched on), each with what it is and how far: made when the
    /// seed or the switches change, only moved when the view does.
    /// </summary>
    internal void PlaceMarks()
    {
        Marks.Children.Clear();
        _marks.Clear();
        if (Shown?.Landmarks is not { } marks)
        {
            return;
        }
        var (x, z) = Shown.Stats.Start;
        bool Showing(SeedPreview.Landmark m) => (m.Kind switch
        {
            "start" => ShowStart,
            "boss" => ShowBosses,
            _ => m.Name switch { "The Bog Witch" => ShowBogWitch, "Hildir" => ShowHildir, _ => ShowHaldor },
        }).IsChecked == true;
        // The start last: on top.
        foreach (var m in marks.OrderBy(m => m.Kind == "start").Where(Showing))
        {
            var c = Mark(m.Kind, m.Name);
            float d = MathF.Sqrt((m.X - x) * (m.X - x) + (m.Z - z) * (m.Z - z));
            string what = m.Kind == "boss" ? $"{m.Name}'s altar" : m.Name;
            string tip = m.Kind == "start" ? $"The start: {m.X:0}, {m.Z:0}" : $"{what}: {m.X:0}, {m.Z:0}, {Km(d)} from the start";
            if (m.OneOf)
            {
                tip += $"\nOne of {marks.Count(o => o.Name == m.Name)} spots: the first one players come near becomes the camp, the others vanish.";
            }
            ToolTip.SetTip(c, tip);
            Marks.Children.Add(c);
            _marks.Add((m, c));
        }
        MoveMarks();
    }

    /// <summary>
    /// The marks where the view puts them (those out of it hidden).
    /// </summary>
    private void MoveMarks()
    {
        foreach (var (m, c) in _marks)
        {
            var at = Map.ScreenOf(m.X, m.Z);
            bool inView = at.X >= -10 && at.Y >= -10 && at.X <= Map.Bounds.Width + 10 && at.Y <= Map.Bounds.Height + 10;
            c.IsVisible = inView;
            if (inView)
            {
                Canvas.SetLeft(c, at.X - c.Width / 2);
                Canvas.SetTop(c, at.Y - c.Height / 2);
            }
        }
    }

    /// <summary>
    /// A distance for people: hundreds of metres under 1 km, else km to one decimal; "none" for infinity.
    /// </summary>
    internal static string Km(float m) => float.IsPositiveInfinity(m) ? "none" : m < 1000 ? $"{m / 100:0}00 m" : $"{m / 1000:0.0} km";

    /// <summary>
    /// A seed's stats as text, for a search result's tooltip.
    /// </summary>
    internal static string Describe(SeedPreview.Stats s)
    {
        var lines = new List<string>
        {
            $"Land: {s.Land * 100:0} % of the world · the start's landmass: {s.StartContinent * 100:0} % of the land (largest: {s.MainContinent * 100:0} %) · land around the start: {s.LandNearStart * 100:0} %",
            "Biomes: " + string.Join(", ", SeedPreview.Biomes.Where(b => s.Shares[b] > 0.005f).Select(b => $"{Name(b)} {s.Shares[b] * 100:0} %")),
            "Nearest to the start: " + string.Join(", ", SeedPreview.Biomes.Skip(1).Where(b => b is not (Heightmap.Biome.AshLands or Heightmap.Biome.DeepNorth))
                .Select(b => $"{Name(b)} {Km(s.Distance[b])}{(s.SameLand[b] || float.IsPositiveInfinity(s.Distance[b]) ? "" : " (by sea)")}")),
            s.TempleStart
                ? $"The start (red dot): {s.Start.X:0}, {s.Start.Z:0}, where the game will put its start temple."
                : $"The start (red dot) is likely near {s.Start.X:0}, {s.Start.Z:0}: the game puts it in Meadows close to the middle.",
        };
        if (s.Nearest is { } n)
        {
            string Of(string name) => Km(n.TryGetValue(name, out float d) ? d : float.PositiveInfinity);
            lines.Add($"Traders (the nearest of their spots): Haldor {Of("Haldor")}, the Bog Witch {Of("The Bog Witch")}, Hildir {Of("Hildir")}. Each has several spots: the first one players come near becomes the trader's camp, the others vanish.");
            lines.Add("Bosses (the nearest altar): " + string.Join(", ", SeedPreview.Landmarks.Where(l => l.Kind == "boss").Select(l => $"{l.Name} {Of(l.Name)}")));
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// A biome's name as the game shows it.
    /// </summary>
    internal static string Name(Heightmap.Biome b) => b switch
    {
        Heightmap.Biome.BlackForest => "Black Forest",
        Heightmap.Biome.AshLands => "Ashlands",
        Heightmap.Biome.DeepNorth => "Deep North",
        _ => b.ToString(),
    };

    /// <summary>
    /// The preview as a small picture of px pixels (a search's results): the map's colours (SeedMap.Shade), the start
    /// in red, north up.
    /// </summary>
    /// <param name="p">The preview.</param>
    /// <param name="px">The picture's side, in pixels.</param>
    /// <param name="startDot">Mark the start with a red square.</param>
    internal static WriteableBitmap Draw(SeedPreview.Preview p, int px, bool startDot = true)
    {
        var bmp = new WriteableBitmap(new PixelSize(px, px), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        byte[] row = new byte[px * 4];
        int startI = (int)((p.Stats.Start.X + SeedPreview.Radius) / p.Cell), startJ = (int)((p.Stats.Start.Z + SeedPreview.Radius) / p.Cell);
        for (int y = 0; y < px; y++)
        {
            int j = p.Size - 1 - y * p.Size / px;
            for (int x = 0; x < px; x++)
            {
                int i = x * p.Size / px, k = j * p.Size + i;
                var (r, g, b) = SeedMap.Shade(p.Biome[k], p.Height[k]);
                if (startDot && Math.Abs(i - startI) <= Math.Max(1, p.Size / 100) && Math.Abs(j - startJ) <= Math.Max(1, p.Size / 100))
                {
                    (r, g, b) = (230, 40, 40);
                }
                row[x * 4] = b;
                row[x * 4 + 1] = g;
                row[x * 4 + 2] = r;
                row[x * 4 + 3] = 255;
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
        }
        return bmp;
    }

    /// <summary>
    /// The search's wishes as the boxes set them.
    /// </summary>
    internal SeedPreview.Wishes Wishes() => new(
        BigStartLand: BigLand.IsChecked == true, LandAroundStart: LandAround.IsChecked == true,
        MaxSwampDistance: (float)(Swamp.Value ?? 0), MaxMountainDistance: (float)(Mountain.Value ?? 0), MaxPlainsDistance: (float)(Plains.Value ?? 0),
        BiomesOnStartLand: OnFoot.IsChecked == true,
        MaxHaldorDistance: (float)(Haldor.Value ?? 0), MaxBogWitchDistance: (float)(BogWitch.Value ?? 0), MaxHildirDistance: (float)(Hildir.Value ?? 0),
        MaxBossDistance: (float)(Bosses.Value ?? 0));

    /// <summary>
    /// Random seeds looked at in the background; the best shown when done (or stopped).
    /// </summary>
    internal void StartSearch()
    {
        _search?.Cancel();
        var cancel = _search = new CancellationTokenSource();
        int count = Count.SelectedIndex switch { 0 => 60, 2 => 400, _ => 150 };
        var wishes = Wishes();
        Find.IsEnabled = false;
        Stop.IsVisible = Progress.IsVisible = true;
        Progress.Value = 0;
        ClearFound();
        var rules = GameLocations.UseGameRules(GameLook.Bundles);
        Pending = Task.Run(async () =>
        {
            await rules.ConfigureAwait(false);
            return SeedPreview.Search(wishes, count, 12, 96, (done, total) => Dispatcher.UIThread.Post(() => Progress.Value = (double)done / total), cancel.Token);
        })
            .ContinueWith(t =>
            {
                Find.IsEnabled = true;
                Stop.IsVisible = Progress.IsVisible = false;
                if (t.Status != TaskStatus.RanToCompletion)
                {
                    ClearFound();
                    Found.Children.Add(Ui.Hint($"The search failed: {t.Exception?.GetBaseException().Message}"));
                    Console.WriteLine($"New world: the seed search failed: {t.Exception?.GetBaseException()}");
                    return;
                }
                Results = t.Result;
                Console.WriteLine($"New world: searched {count} seeds{(cancel.IsCancellationRequested ? " (stopped)" : "")}, best {(Results.Count > 0 ? $"{Results[0].Preview.Seed} (score {Results[0].Score:0.0})" : "none")}.");
                ShowResults();
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// The search results as pictures to click, each with its seed and score.
    /// </summary>
    private void ShowResults()
    {
        ClearFound();
        if (Results.Count == 0)
        {
            Found.Children.Add(Ui.Hint("No seed looked at yet."));
            return;
        }
        foreach (var (p, score) in Results)
        {
            var b = new Button
            {
                Padding = new Thickness(3),
                Tag = p.Seed,
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new Image { Source = Draw(p, 120), Width = 120, Height = 120 },
                        new TextBlock { Text = p.Seed, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = $"score {score:0.0}", FontSize = 10, Foreground = Ui.Muted, HorizontalAlignment = HorizontalAlignment.Center },
                    },
                },
            };
            ToolTip.SetTip(b, Describe(p.Stats));
            b.Click += (_, _) =>
            {
                // Looked at at once (no typing pause to wait for).
                SeedBox.Text = p.Seed;
                _wait.Stop();
                ShowSeed(p.Seed);
            };
            Found.Children.Add(b);
        }
        MarkFound(Shown?.Seed);
    }

    /// <summary>
    /// The search results taken away, their pictures freed.
    /// </summary>
    private void ClearFound()
    {
        foreach (var image in Found.GetVisualDescendants().OfType<Image>())
        {
            (image.Source as IDisposable)?.Dispose();
            image.Source = null;
        }
        Found.Children.Clear();
    }

    /// <summary>
    /// Create: checked here, made by the window (CreateRequested).
    /// </summary>
    internal void AskCreate()
    {
        string name = (NameBox.Text ?? "").Trim(), seed = (SeedBox.Text ?? "").Trim();
        var roots = (List<string>)Where.Tag!;
        string root = roots[Math.Max(0, Where.SelectedIndex)];
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Problem.Text = "Give the world a name that can be a file name (no / \\ : * ? \" < > |).";
            return;
        }
        if (seed.Length is 0 or > 10)
        {
            Problem.Text = "The seed is 1 to 10 characters, as in the game.";
            return;
        }
        string folder = Path.Combine(root, name);
        if (Directory.Exists(folder) || File.Exists(Path.Combine(root, name + ".fwl")))
        {
            Problem.Text = $"There is a world called {name} already in {Ui.Tilde(root)}: choose another name.";
            return;
        }
        Problem.Text = "";
        StopAll();
        CreateRequested?.Invoke(folder, name, seed);
    }
}
