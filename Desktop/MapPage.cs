using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Vector2 = System.Numerics.Vector2;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world map page, like the web editor's (index.html): the world drawn like the game's map, what is waiting to be
/// saved (Save, or Apply live, and Discard), the map's options, search across the world, zones picked by filter to
/// reset, the edited zones, and the spot picked with a click, opened in the 3D editor with the size chosen.
/// </summary>
public sealed class MapPage
{
    /// <summary>
    /// The page: the map with the side panel, the cursor readout and the status over it.
    /// </summary>
    public Control View { get; }
    /// <summary>
    /// The map itself.
    /// </summary>
    public MapView Map { get; } = new();
    /// <summary>
    /// The world shown, or null before one is.
    /// </summary>
    private WorldSession? _session;
    /// <summary>
    /// Where a newly shown world's map looks first (the place remembered from the last run; null: the whole world).
    /// </summary>
    internal Func<(float X, float Z, float Mpp)?> StartView { get; set; } = () => null;

    /// <summary>
    /// The Worlds button: back to the world list.
    /// </summary>
    public event Action? BackToWorlds;
    /// <summary>
    /// Edit in 3D: the zone and the size in zones (Target: an object found to select there).
    /// </summary>
    public event Action<int, int, int>? EditRequested;
    /// <summary>
    /// Save, apply live, discard, and reload from the game: the window does them (saving and discarding ask first).
    /// </summary>
    public event Action? SaveRequested, DiscardRequested, ReloadRequested;
    /// <summary>
    /// Asks the user a yes/no question; the window sets it (tests answer yes).
    /// </summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>
    /// Tells the user something; the window sets it.
    /// </summary>
    internal Func<string, Task> Tell { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// The world's name, seed, save number and object count.
    /// </summary>
    internal TextBlock Meta { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// What is waiting to be saved or applied.
    /// </summary>
    internal TextBlock Pending { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Save to world, or Apply live for a live world.
    /// </summary>
    internal Button SaveButton { get; } = new Button { Content = "Save to world…", FontSize = 12.5 }.Classed("primary");
    /// <summary>
    /// Throws away what is waiting.
    /// </summary>
    internal Button DiscardButton { get; } = new() { Content = "Discard", FontSize = 12 };
    /// <summary>
    /// Live: the players online, or that the game does not answer.
    /// </summary>
    internal TextBlock LiveText { get; } = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Live: reads the world from the game again.
    /// </summary>
    internal Button ReloadButton { get; } = new() { Content = Icons.With("reload", "Reload from the game"), FontSize = 12 };
    /// <summary>
    /// The live box (shown only for a live world) and the waiting-changes box.
    /// </summary>
    private readonly Control _liveBox, _changesBox;
    /// <summary>
    /// Show player-built pieces; its text gets their count once read.
    /// </summary>
    internal CheckBox BuildingsBox { get; } = new() { Content = "Show buildings (player-built pieces)", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Show the zone grid.
    /// </summary>
    internal CheckBox GridBox { get; } = new() { Content = "Zone grid", FontSize = 12 };
    /// <summary>
    /// Outline edited zones.
    /// </summary>
    internal CheckBox EditedBox { get; } = new() { Content = "Outline edited zones", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Show painted ground when zoomed in.
    /// </summary>
    internal CheckBox PaintBox { get; } = new() { Content = "Show painted ground (dirt, paved, fields) when zoomed in", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Show the game's map clouds.
    /// </summary>
    internal CheckBox CloudsBox { get; } = new() { Content = "Clouds (as in game)", FontSize = 12 };
    /// <summary>
    /// The picked zone (or search result) and its biome.
    /// </summary>
    internal TextBlock PickTitle { get; } = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Opens the picked spot in the 3D editor.
    /// </summary>
    internal Button EditButton { get; } = new Button { Content = "Edit in 3D", FontSize = 12.5 }.Classed("primary");
    /// <summary>
    /// The area to open: 3, 5 or 7 zones a side.
    /// </summary>
    internal ComboBox SizeBox { get; } = new() { ItemsSource = new[] { "3 × 3 zones (192 m)", "5 × 5 zones (320 m)", "7 × 7 zones (448 m)" }, SelectedIndex = 1, FontSize = 12 };
    /// <summary>
    /// The picked spot's box, hidden until a spot is picked.
    /// </summary>
    private readonly Control _pickBox;
    /// <summary>
    /// The world point, zone and biome under the mouse; its box hides while empty.
    /// </summary>
    internal TextBlock Cursor { get; } = new() { FontSize = 12, Foreground = Ui.Muted };
    /// <summary>
    /// The map's status line (shown while the map is being built).
    /// </summary>
    private readonly TextBlock _status = new() { FontSize = 13, Foreground = Brushes.White };
    /// <summary>
    /// The picked zone, or null when none is.
    /// </summary>
    public (int X, int Z)? Spot { get; private set; }
    /// <summary>
    /// An object found by the search: selected (and, for items and texts, inspected) once the area opens.
    /// </summary>
    public (int Id, bool Inspect)? Target { get; private set; }
    /// <summary>
    /// The area to open, in zones a side.
    /// </summary>
    public int Size => new[] { 3, 5, 7 }[Math.Max(0, SizeBox.SelectedIndex)];
    /// <summary>
    /// Live: asks the game for the players online every five seconds.
    /// </summary>
    private readonly DispatcherTimer _players = new() { Interval = TimeSpan.FromSeconds(5) };
    /// <summary>
    /// The players' name labels over the map.
    /// </summary>
    private readonly Canvas _labels = new() { IsHitTestVisible = false };

    // Search.
    /// <summary>
    /// What to search for.
    /// </summary>
    internal TextBox SearchBox { get; } = new() { PlaceholderText = "Beech, Wood, a sign's text…", FontSize = 12 };
    /// <summary>
    /// What to search: object kinds, items in containers, or texts.
    /// </summary>
    internal ComboBox SearchWhat { get; } = new() { ItemsSource = new[] { "Objects (by kind)", "Items in containers", "Texts (signs, portals, wards…)" }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>
    /// Runs the search.
    /// </summary>
    internal Button FindButton { get; } = new Button { Content = Icons.With("search", "Find"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Stretch }.Classed("primary");
    /// <summary>
    /// The search's counts, or what went wrong.
    /// </summary>
    internal TextBlock SearchInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The search results (the first 300); picking one goes there.
    /// </summary>
    internal ListBox Hits { get; } = new() { MaxHeight = 220, FontSize = 11 };
    /// <summary>
    /// The last search's results, pinned on the map.
    /// </summary>
    private List<WorldSearch.Hit> _hits = [];
    /// <summary>
    /// What the last search looked for: "kinds", "items" or "texts".
    /// </summary>
    private string _searchWhat = "kinds";

    // Zone filter.
    /// <summary>
    /// The zone filter's biome toggles, by biome value.
    /// </summary>
    internal readonly Dictionary<int, ToggleButton> BiomeButtons = [];
    /// <summary>
    /// Only zones with no buildings in them or within <c>DistBox</c> zones.
    /// </summary>
    internal CheckBox NoBuildBox { get; } = new() { Content = "No buildings in the zone or within", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// How many zones around a match must have no buildings, 0..50.
    /// </summary>
    internal NumericUpDown DistBox { get; } = new() { Value = 2, Minimum = 0, Maximum = 50, Increment = 1, FormatString = "0", FontSize = 12, MinWidth = 70 };
    /// <summary>
    /// Only zones without ground edits.
    /// </summary>
    internal CheckBox NoEditBox { get; } = new() { Content = "No ground edits", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Only zones the game generated.
    /// </summary>
    internal CheckBox OnlyGenBox { get; } = new() { Content = "Only zones the game generated", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Smallest distance from the world centre, in metres; empty for none.
    /// </summary>
    internal TextBox RMinBox { get; } = new() { PlaceholderText = "0", FontSize = 12, Width = 70 };
    /// <summary>
    /// Largest distance from the world centre, in metres; empty for none.
    /// </summary>
    internal TextBox RMaxBox { get; } = new() { PlaceholderText = "10500", FontSize = 12, Width = 70 };
    /// <summary>
    /// Resetting also undoes the zones' ground edits.
    /// </summary>
    internal CheckBox GroundBox { get; } = new() { Content = "Also undo their ground edits", FontSize = 12 };
    /// <summary>
    /// Shows the zones that match the filter on the map.
    /// </summary>
    internal Button ShowMatchButton { get; } = new() { Content = "Show matching", FontSize = 12 };
    /// <summary>
    /// Marks the matching zones for reset (after asking).
    /// </summary>
    internal Button MarkButton { get; } = new Button { Content = "Mark for reset…", FontSize = 12 }.Classed("primary");
    /// <summary>
    /// Unmarks every zone marked for reset.
    /// </summary>
    internal Button UnmarkButton { get; } = new() { Content = "Unmark all", FontSize = 12 };
    /// <summary>
    /// How many zones match, and how many are marked.
    /// </summary>
    internal TextBlock ZoneInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// Every zone's counts (<c>ZoneStats.Compute</c>), read when first needed; null after the world changes.
    /// </summary>
    private int[]? _stats;
    /// <summary>
    /// Whether matches are shown, so changing the filter shows them again at once.
    /// </summary>
    private bool _filtered;
    /// <summary>
    /// The zones that matched the filter last.
    /// </summary>
    internal List<(int X, int Z)> Matches { get; private set; } = [];

    // Zone detail and the edited zones.
    /// <summary>
    /// The edited zones, most edited first; picking one goes there.
    /// </summary>
    internal ListBox EditedList { get; } = new() { MaxHeight = 200, FontSize = 11 };
    /// <summary>
    /// The edited zones' section, its header carrying their count.
    /// </summary>
    private readonly Expander _editedSection;
    /// <summary>
    /// The zones listed in <c>EditedList</c>, in its order.
    /// </summary>
    private List<ZoneEdit> _edited = [];

    /// <summary>
    /// The side panel's background and border brushes.
    /// </summary>
    private static readonly IBrush PanelBg = Ui.Panel, Line = Ui.Line;

    /// <summary>
    /// A section heading.
    /// </summary>
    private static TextBlock H2(string t) => Ui.Heading(t, 4);

    /// <summary>
    /// Builds the page; a world is shown with <c>Show</c>.
    /// </summary>
    public MapPage()
    {
        var worlds = new Button { Content = Icons.With("back", "Worlds"), FontSize = 12.5 }.Classed("ghost");
        worlds.Tip("map.worlds");
        SaveButton.Tip("map.save");
        DiscardButton.Tip("map.discard");
        GridBox.Tip("map.grid");
        EditedBox.Tip("map.edited");
        PaintBox.Tip("map.paint");
        CloudsBox.Tip("map.clouds");
        EditButton.Tip("map.edit");
        SearchWhat.Tip("map.searchWhat");
        DistBox.Tip("map.dist");
        RMinBox.Tip("map.rmin");
        RMaxBox.Tip("map.rmax");
        worlds.Click += (_, _) => BackToWorlds?.Invoke();
        SaveButton.Click += (_, _) => SaveRequested?.Invoke();
        DiscardButton.Click += (_, _) => DiscardRequested?.Invoke();
        ReloadButton.Click += (_, _) => ReloadRequested?.Invoke();
        ReloadButton.Tip("map.reload");
        BuildingsBox.Tip("map.buildings");
        BuildingsBox.IsCheckedChanged += (_, _) => { Map.ShowBuildings = BuildingsBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        GridBox.IsCheckedChanged += (_, _) => { Map.ShowGrid = GridBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        EditedBox.IsCheckedChanged += (_, _) => { Map.ShowEdited = EditedBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        PaintBox.IsCheckedChanged += (_, _) => { Map.ShowPaint = PaintBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        CloudsBox.IsCheckedChanged += (_, _) => { Map.ShowClouds = CloudsBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        EditButton.Click += (_, _) => { if (Spot is var (x, z)) { EditRequested?.Invoke(x, z, Size); } };
        SizeBox.SelectionChanged += (_, _) => ShowChosen();
        SizeBox.Tip("map.size");
        Map.Picked += (x, z) => Pick((int)MathF.Floor((x + 32) / 64), (int)MathF.Floor((z + 32) / 64));
        Map.Hovered += Hover;
        Map.Status += t => _status.Text = t;
        Map.PiecesRead += () => BuildingsBox.Content = $"Show buildings ({Map.PieceCount:N0} player-built pieces)";
        Map.ViewChanged += PlaceLabels;
        Map.SizeChanged += (_, _) => PlaceLabels();
        _liveBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { LiveText, ReloadButton } };
        _changesBox = new StackPanel { Spacing = 6, Children = { Pending, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { SaveButton, DiscardButton } } } };

        // Search the world.
        SearchBox.Tip("map.search");
        FindButton.Tip("map.find");
        FindButton.Click += async (_, _) => await Search();
        SearchBox.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { await Search(); } };
        SearchWhat.SelectionChanged += async (_, _) => { if (!string.IsNullOrWhiteSpace(SearchBox.Text)) { await Search(); } };
        Hits.SelectionChanged += (_, _) => ShowHit(Hits.SelectedIndex);
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 6, RowSpacing = 4 };
        searchRow.Children.Add(SearchBox);
        Grid.SetRow(SearchWhat, 1);
        searchRow.Children.Add(SearchWhat);
        Grid.SetColumn(FindButton, 1);
        Grid.SetRowSpan(FindButton, 2);
        searchRow.Children.Add(FindButton);
        var search = Section("Search the world", true, searchRow, SearchInfo, Hits);

        // Reset zones across the world.
        var biomes = new WrapPanel();
        foreach (var (value, name) in ZoneFilter.BiomeChoices)
        {
            var b = new ToggleButton { Content = name, FontSize = 11, Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 4, 4) }.Tip("map.biome");
            b.IsCheckedChanged += (_, _) => Refilter();
            BiomeButtons[value] = b;
            biomes.Children.Add(b);
        }
        biomes.Tip("map.biome");
        NoBuildBox.Tip("map.noBuild");
        NoEditBox.Tip("map.noEdit");
        OnlyGenBox.Tip("map.onlyGen");
        GroundBox.Tip("map.ground");
        ShowMatchButton.Tip("map.show");
        MarkButton.Tip("map.mark");
        UnmarkButton.Tip("map.unmark");
        foreach (var c in new[] { NoBuildBox, NoEditBox, OnlyGenBox })
        {
            c.IsCheckedChanged += (_, _) => Refilter();
        }
        DistBox.ValueChanged += (_, _) => Refilter();
        RMinBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Refilter(); } };
        RMaxBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Refilter(); } };
        ShowMatchButton.Click += async (_, _) => await ShowMatching();
        MarkButton.Click += async (_, _) => await MarkMatching();
        UnmarkButton.Click += async (_, _) => await UnmarkAll();
        var zones = Section("Reset zones across the world", false,
            new TextBlock { Text = "Pick zones everywhere at once (like MCA Selector) and have the game generate them again: new trees, ore and dungeons, for example after a Valheim update. Buildings are never touched.", FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
            biomes,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { NoBuildBox, DistBox, new TextBlock { Text = "zone(s)", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } },
            NoEditBox, OnlyGenBox,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "From the centre", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, RMinBox, new TextBlock { Text = "to", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, RMaxBox, new TextBlock { Text = "m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } },
            GroundBox,
            new WrapPanel { Children = { Spaced(ShowMatchButton), Spaced(MarkButton), Spaced(UnmarkButton) } },
            ZoneInfo);

        // The spot picked, and its edits up close.
        _pickBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { PickTitle, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { EditButton, SizeBox } } } };
        EditedList.SelectionChanged += (_, _) =>
        {
            if (EditedList.SelectedIndex is int i and >= 0 && i < _edited.Count)
            {
                var e = _edited[i];
                Map.LookAt(e.ZoneX * 64, e.ZoneZ * 64, MathF.Min(Map.MetersPerPixel, 0.5f));
                Pick(e.ZoneX, e.ZoneZ);
            }
        };
        _editedSection = Section("Edited zones", false, EditedList);

        var side = new Border
        {
            Background = PanelBg,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            Margin = new Thickness(10),
            Width = 340,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Ui.BrandTitle(16, VerticalAlignment.Center), Col(worlds, 1) } },
                        Meta,
                        _liveBox,
                        _changesBox,
                        H2("Map"),
                        new StackPanel { Children = { BuildingsBox, EditedBox, PaintBox, GridBox, CloudsBox } },
                        _pickBox,
                        new TextBlock { Text = "Click the map to pick a spot, drag to move, wheel to zoom.", FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
                        search,
                        zones,
                        _editedSection,
                    },
                },
            },
        };
        var surface = new Border { Background = Brushes.Transparent };
        Map.Attach(surface);
        var cursor = new Border { Background = PanelBg, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = Ui.Pad, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Child = Cursor };
        var status = new Border
        {
            Background = PanelBg,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _status,
            IsVisible = false
        };
        Map.Status += t => status.IsVisible = !string.IsNullOrEmpty(t);
        cursor.IsVisible = false;
        Cursor.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) { cursor.IsVisible = !string.IsNullOrEmpty(Cursor.Text); } };
        View = new Grid { Background = new SolidColorBrush(Color.FromRgb(20, 23, 28)), Children = { Map, surface, _labels, side, cursor, status } };
        _players.Tick += async (_, _) => await PollPlayers();
    }

    /// <summary>
    /// A collapsible section of the side panel.
    /// </summary>
    /// <param name="title">Its header.</param>
    /// <param name="open">Whether it starts open.</param>
    /// <param name="children">Its contents, top to bottom.</param>
    private static Expander Section(string title, bool open, params Control[] children)
    {
        var panel = new StackPanel { Spacing = 6 };
        foreach (var c in children)
        {
            panel.Children.Add(c);
        }
        return new Expander { Header = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeight.SemiBold }, IsExpanded = open, Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    /// <summary>
    /// <paramref name="c"/> with a margin to its right and below, for a wrapping row of buttons.
    /// </summary>
    private static Control Spaced(Control c)
    {
        c.Margin = new Thickness(0, 0, 6, 4);
        return c;
    }

    /// <summary>
    /// <paramref name="c"/> set in a grid column; column 2 is right-aligned, others take <paramref name="align"/>.
    /// </summary>
    private static Control Col(Control c, int col, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        Grid.SetColumn(c, col);
        c.HorizontalAlignment = col == 2 ? HorizontalAlignment.Right : align;
        return c;
    }

    /// <summary>
    /// Shows a world: a new one starts afresh (pick, search and matches cleared, the view where it was last looked at);
    /// the same one again just refreshes. Live worlds poll their players.
    /// </summary>
    public void Show(WorldSession session)
    {
        bool same = _session == session;
        _session = session;
        if (!same)
        {
            Map.Show(session);
            Spot = null;
            Target = null;
            Map.Chosen = null;
            _pickBox.IsVisible = false;
            _hits = [];
            Hits.ItemsSource = null;
            SearchInfo.Text = "";
            Map.Pins = [];
            Map.PinChosen = -1;
            Matches = [];
            Map.Matches = Matches;
            _filtered = false;
            ZoneInfo.Text = "";
            // Where the map was last looked at (remembered), or the whole world.
            if (StartView() is var (vx, vz, vm))
            {
                Map.LookAt(vx, vz, vm);
            }
            else
            {
                Map.LookAt(0, 0, 10500 * 2 / 900f);
            }
        }
        else
        {
            Map.Refresh();
        }
        // Saved, discarded, reloaded or edited since: the zone counts are read again when next asked.
        _stats = null;
        var w = session.World;
        Meta.Text = session.IsLive
            ? $"{w.Name} · live ({session.Label}) · seed {w.SeedName} · {w.ObjectCount:N0} objects"
            : $"{w.Name} · seed {w.SeedName} · save #{w.SaveNumber} · {w.ObjectCount:N0} objects in {w.ChunkCount} chunks";
        _liveBox.IsVisible = session.IsLive;
        SaveButton.Content = session.IsLive ? "Apply live" : "Save to world…";
        SaveButton.Tip(session.IsLive ? "map.apply" : "map.save");
        UpdatePending();
        FillEdited();
        _players.Stop();
        Map.Players = [];
        PlaceLabels();
        if (session.IsLive)
        {
            _ = PollPlayers();
            _players.Start();
        }
    }

    /// <summary>
    /// Stops polling the live players (the page is left).
    /// </summary>
    public void Stop() => _players.Stop();

    /// <summary>
    /// Shows what is waiting to be saved or applied; Save and Discard are enabled only when something is.
    /// </summary>
    public void UpdatePending()
    {
        if (_session == null)
        {
            return;
        }
        var (z, d, a, r) = _session.Pending;
        var parts = new[] { z > 0 ? $"{z} zone{(z > 1 ? "s" : "")} of ground" : "", d > 0 ? $"{d} deleted" : "", a > 0 ? $"{a} added" : "", r > 0 ? $"{r} zone reset" : "" }.Where(p => p != "").ToList();
        Pending.Text = parts.Count == 0 ? (_session.IsLive ? "Everything is applied." : "Everything is saved.") : $"{(_session.IsLive ? "Not applied yet" : "Not saved yet")}: {string.Join(", ", parts)}.";
        SaveButton.IsEnabled = DiscardButton.IsEnabled = parts.Count > 0;
        Map.RequestNextFrameRendering();
    }

    /// <summary>
    /// Live: asks the game for the players online and puts them on the map.
    /// </summary>
    private async Task PollPlayers()
    {
        if (_session?.Live is not { } live)
        {
            return;
        }
        try
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(await live.Players())?.AsArray();
            var players = json?.Where(p => p?["name"] != null).Select(p => ((string)p!["name"]!, (float)(p["x"] ?? 0), (float)(p["z"] ?? 0))).ToList() ?? [];
            LiveText.Text = $"Live world · {players.Count} player(s) online{(players.Count > 0 ? ": " + string.Join(", ", players.Select(p => p.Item1)) : "")}";
            Map.Players = players;
            PlaceLabels();
            Map.RequestNextFrameRendering();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            LiveText.Text = "Live world · the game does not answer right now.";
        }
    }

    /// <summary>
    /// The players' names next to their dots.
    /// </summary>
    private void PlaceLabels()
    {
        _labels.Children.Clear();
        foreach (var (name, x, z) in Map.Players)
        {
            var at = Map.ScreenOf(x, z);
            var t = new TextBlock { Text = name, FontSize = 12, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold, Effect = new DropShadowEffect { BlurRadius = 3, OffsetX = 0, OffsetY = 0, Color = Colors.Black, Opacity = 1 } };
            Canvas.SetLeft(t, at.X + 10);
            Canvas.SetTop(t, at.Y - 8);
            _labels.Children.Add(t);
        }
    }

    /// <summary>
    /// A biome's name as the game shows it.
    /// </summary>
    private static string BiomeName(ValheimGen.Heightmap.Biome b) => b switch
    {
        ValheimGen.Heightmap.Biome.BlackForest => "Black Forest",
        ValheimGen.Heightmap.Biome.AshLands => "Ashlands",
        ValheimGen.Heightmap.Biome.DeepNorth => "Deep North",
        _ => b.ToString(),
    };

    /// <summary>
    /// A zone picked: its name, the area the 3D editor would open around it, and its edits.
    /// </summary>
    public void Pick(int zx, int zz)
    {
        Spot = (zx, zz);
        Target = null;
        string biome = _session != null ? BiomeName(_session.Terrain.BiomeAt(zx * 64f, zz * 64f)) : "";
        PickTitle.Text = $"Zone {zx}, {zz} · {biome}";
        _pickBox.IsVisible = true;
        ShowChosen();
    }

    /// <summary>
    /// Outlines the area the 3D editor would open around the picked zone.
    /// </summary>
    private void ShowChosen()
    {
        if (Spot is var (x, z))
        {
            Map.Chosen = (x, z, Size);
            Map.RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// The mouse is over (x, z): shows the point, its zone and its biome.
    /// </summary>
    private void Hover(float x, float z)
    {
        if (_session == null)
        {
            return;
        }
        int zx = (int)MathF.Floor((x + 32) / 64), zz = (int)MathF.Floor((z + 32) / 64);
        Cursor.Text = $"x {x:0}, z {z:0} · zone {zx}, {zz} · {BiomeName(_session.Terrain.BiomeAt(x, z))}";
    }

    // ---- Search: every object of the world by kind, items in containers, or texts.
    /// <summary>
    /// Searches the world for the search box's text and pins what is found; an empty box clears the results.
    /// </summary>
    internal async Task Search()
    {
        if (_session is not { } s)
        {
            return;
        }
        string q = SearchBox.Text?.Trim() ?? "";
        _searchWhat = SearchWhat.SelectedIndex switch { 1 => "items", 2 => "texts", _ => "kinds" };
        if (q.Length == 0)
        {
            _hits = [];
            Hits.ItemsSource = null;
            SearchInfo.Text = "";
            Map.Pins = [];
            Map.RequestNextFrameRendering();
            return;
        }
        SearchInfo.Text = "Searching…";
        string what = _searchWhat;
        WorldSearch.Result res;
        try
        {
            res = await Task.Run(() => WorldSearch.Search(s.World, s.Edits, q, what));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SearchInfo.Text = "Search failed: " + ex.Message;
            return;
        }
        _hits = res.Hits;
        Map.Pins = [.. _hits.Select(h => new Vector2(h.X, h.Z))];
        Map.PinChosen = -1;
        if (res.Total == 0)
        {
            SearchInfo.Text = "Nothing found.";
            Hits.ItemsSource = null;
        }
        else
        {
            var counts = res.Counts.OrderByDescending(c => c.Value).ToList();
            string top = string.Join(" · ", counts.Take(12).Select(c => $"{c.Value:N0} {c.Key}")) + (counts.Count > 12 ? $" · +{counts.Count - 12} more" : "");
            SearchInfo.Text = $"{top}\n{res.Total:N0} found{(what == "items" ? " in all" : "")}{(res.Truncated ? $", the first {res.Hits.Count:N0} pinned" : "")}. Click one to go there.";
            Hits.ItemsSource = _hits.Take(300).Select(h => $"{h.Name}   {h.X:0}, {h.Z:0}{(h.Match != null ? "\n   " + h.Match : "")}").ToList();
        }
        Map.RequestNextFrameRendering();
    }

    /// <summary>
    /// A result picked: the map goes there, and Edit in 3D selects it (items and texts: in the inspector).
    /// </summary>
    internal void ShowHit(int i)
    {
        if (i < 0 || i >= _hits.Count)
        {
            return;
        }
        var h = _hits[i];
        Map.PinChosen = i;
        Map.LookAt(h.X, h.Z, MathF.Min(Map.MetersPerPixel, 0.6f));
        int zx = (int)MathF.Floor((h.X + 32) / 64), zz = (int)MathF.Floor((h.Z + 32) / 64);
        Pick(zx, zz);
        Target = (h.Id, _searchWhat != "kinds");
        PickTitle.Text = $"{h.Name} at {h.X:0}, {h.Z:0} · zone {zx}, {zz}";
    }

    // ---- Zones across the world, picked by filter, to reset.
    /// <summary>
    /// The zone filter as the boxes set it.
    /// </summary>
    private ZoneFilter Filter()
    {
        static float? Num(TextBox b) => float.TryParse(b.Text?.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : null;
        return new ZoneFilter(BiomeButtons.Where(b => b.Value.IsChecked == true).Select(b => b.Key).ToHashSet(), NoBuildBox.IsChecked == true, (int)(DistBox.Value ?? 0),
            NoEditBox.IsChecked == true, OnlyGenBox.IsChecked == true, Num(RMinBox), Num(RMaxBox));
    }

    /// <summary>
    /// Every zone's counts, read once (in the background) until the world changes; null when no world is shown.
    /// </summary>
    private async Task<int[]?> Stats()
    {
        if (_session is not { } s)
        {
            return null;
        }
        if (_stats == null)
        {
            ZoneInfo.Text = "Reading every zone…";
            _stats = await Task.Run(() => ZoneStats.Compute(s.World, s.Edits, (x, z) => (int)s.Terrain.BiomeAt(x * 64f, z * 64f)));
        }
        return _stats;
    }

    /// <summary>
    /// The filter changed: matches shown are worked out again.
    /// </summary>
    private void Refilter()
    {
        if (_stats != null && _filtered)
        {
            ApplyFilter();
        }
    }

    /// <summary>
    /// Works out the matching zones and shows them on the map with their count.
    /// </summary>
    private void ApplyFilter()
    {
        if (_stats is not { } stats || _session is not { } s)
        {
            return;
        }
        _filtered = true;
        var (zones, objects) = Filter().Match(stats);
        Matches = zones;
        Map.Matches = zones;
        int marked = s.Edits.ResetCount;
        ZoneInfo.Text = $"{zones.Count:N0} of {stats.Length / ZoneStats.Stride:N0} zones match ({objects:N0} objects in them){(marked > 0 ? $" · {marked:N0} marked for reset" : "")}.";
        Map.RequestNextFrameRendering();
    }

    /// <summary>
    /// Show matching: reads the zones if needed, then shows the matches.
    /// </summary>
    internal async Task ShowMatching()
    {
        if (await Stats() != null)
        {
            ApplyFilter();
        }
    }

    /// <summary>
    /// Mark for reset: after asking, marks every matching zone for reset (with its ground edits when the box says so).
    /// </summary>
    internal async Task MarkMatching()
    {
        if (_session is not { } s || await Stats() == null)
        {
            return;
        }
        ApplyFilter();
        if (Matches.Count == 0)
        {
            await Tell("No zone matches the filter.");
            return;
        }
        bool live = s.IsLive, ground = GroundBox.IsChecked == true;
        if (!await Confirm($"Mark {Matches.Count:N0} zone(s) for reset?\n\n• Their trees, rocks, ore, ruins and dungeon entrances are removed{(ground ? ", and their ground edits undone" : "")}; players' tombstones stay.\n"
            + $"• Valheim generates them again the next time a player comes near{(live ? " (at once where players are, once applied)" : "")}.\n"
            + $"• {(live ? "Apply live" : "Save to world")} does it; until then it can be cancelled with Unmark all."))
        {
            return;
        }
        foreach (var (x, z) in Matches)
        {
            s.Edits.SetReset(new ZoneReset(x, z, true, ground), true);
        }
        ApplyFilter();
        UpdatePending();
    }

    /// <summary>
    /// Unmark all: drops every zone reset still waiting.
    /// </summary>
    internal async Task UnmarkAll()
    {
        if (_session is not { } s)
        {
            return;
        }
        var resets = s.Edits.Resets;
        if (resets.Count == 0)
        {
            await Tell("No zone is marked for reset.");
            return;
        }
        foreach (var r in resets)
        {
            s.Edits.SetReset(r, false);
        }
        if (_stats != null)
        {
            ApplyFilter();
        }
        UpdatePending();
    }

    // ---- The edited zones, most edited first, and the picked zone's edits up close.
    /// <summary>
    /// Lists the zones with ground or paint edits, most edited first, with their range of change.
    /// </summary>
    private void FillEdited()
    {
        if (_session is not { } s)
        {
            return;
        }
        _edited = [.. s.Edits.All().Where(e => e.HeightCount + e.PaintCount > 0).OrderByDescending(e => e.HeightCount + e.PaintCount)];
        EditedList.ItemsSource = _edited.Select(e =>
        {
            var (lo, hi) = Range(e);
            return $"{e.ZoneX}, {e.ZoneZ}   {e.HeightCount} h · {e.PaintCount} p   {lo:0.0} … {hi:0.0} m{(e.Changed ? "   (not saved)" : "")}";
        }).ToList();
        ((TextBlock)_editedSection.Header!).Text = $"Edited zones ({_edited.Count:N0})";
    }

    /// <summary>
    /// The lowest and highest height change in a zone's edited points, in metres; 0, 0 when none.
    /// </summary>
    private static (float Lo, float Hi) Range(ZoneEdit e)
    {
        float lo = 0, hi = 0;
        bool any = false;
        for (int i = 0; i < EditStore.Cells; i++)
        {
            if (e.Modified[i])
            {
                float d = e.Level[i] + e.Smooth[i];
                lo = any ? MathF.Min(lo, d) : d;
                hi = any ? MathF.Max(hi, d) : d;
                any = true;
            }
        }
        return (lo, hi);
    }
}
