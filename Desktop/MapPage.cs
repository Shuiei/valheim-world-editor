using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Vector2 = System.Numerics.Vector2;

namespace TerrainEditor.Desktop;

// The world map page, like the web editor's (index.html): the world drawn like the game's map, what is
// waiting to be saved (Save, or Apply live, and Discard), the map's options, search across the world,
// zones picked by filter to reset, the edited zones, and the spot picked with a click (its edits shown
// up close), opened in the 3D editor with the size chosen.
public sealed class MapPage
{
	public Control View { get; }
	public MapView Map { get; } = new();
	private WorldSession? _session;
	public event Action? BackToWorlds;
	// Edit in 3D: the zone and the size in zones (Target: an object found to select there).
	public event Action<int, int, int>? EditRequested;
	// Save, apply live, discard: the window does them (it asks first).
	public event Action? SaveRequested, DiscardRequested, ReloadRequested;
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	internal Func<string, Task> Tell { get; set; } = _ => Task.CompletedTask;

	internal TextBlock Meta { get; } = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	internal TextBlock Pending { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
	internal Button SaveButton { get; } = new() { Content = "Save to world…", FontSize = 12 };
	internal Button DiscardButton { get; } = new() { Content = "Discard", FontSize = 12 };
	internal TextBlock LiveText { get; } = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
	internal Button ReloadButton { get; } = new() { Content = "Reload from the game", FontSize = 12 };
	private readonly Control _liveBox, _changesBox;
	internal CheckBox BuildingsBox { get; } = new() { Content = "Show buildings (player-built pieces)", IsChecked = true, FontSize = 12 };
	internal CheckBox GridBox { get; } = new() { Content = "Zone grid", FontSize = 12 };
	internal CheckBox EditedBox { get; } = new() { Content = "Outline edited zones", IsChecked = true, FontSize = 12 };
	internal CheckBox PaintBox { get; } = new() { Content = "Show painted ground (dirt, paved, fields) when zoomed in", IsChecked = true, FontSize = 12 };
	internal CheckBox CloudsBox { get; } = new() { Content = "Clouds (as in game)", FontSize = 12 };
	internal TextBlock PickTitle { get; } = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
	internal Button EditButton { get; } = new() { Content = "Edit in 3D", FontSize = 13 };
	internal ComboBox SizeBox { get; } = new() { ItemsSource = new[] { "3 × 3 zones (192 m)", "5 × 5 zones (320 m)", "7 × 7 zones (448 m)" }, SelectedIndex = 1, FontSize = 12 };
	private readonly Control _pickBox;
	internal TextBlock Cursor { get; } = new() { FontSize = 12, Foreground = Brushes.LightGray };
	private readonly TextBlock _status = new() { FontSize = 13, Foreground = Brushes.White };
	public (int X, int Z)? Spot { get; private set; }
	// An object found by the search: selected (and, for items and texts, inspected) once the area opens.
	public (int Id, bool Inspect)? Target { get; private set; }
	public int Size => new[] { 3, 5, 7 }[Math.Max(0, SizeBox.SelectedIndex)];
	private readonly DispatcherTimer _players = new() { Interval = TimeSpan.FromSeconds(5) };
	private readonly Canvas _labels = new() { IsHitTestVisible = false };

	// Search.
	internal TextBox SearchBox { get; } = new() { Watermark = "Beech, Wood, a sign's text…", FontSize = 12 };
	internal ComboBox SearchWhat { get; } = new() { ItemsSource = new[] { "Objects (by kind)", "Items in containers", "Texts (signs, portals, wards…)" }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal Button FindButton { get; } = new() { Content = "Find", FontSize = 12, VerticalAlignment = VerticalAlignment.Stretch };
	internal TextBlock SearchInfo { get; } = new() { FontSize = 11, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	internal ListBox Hits { get; } = new() { MaxHeight = 220, FontSize = 11 };
	private List<WorldSearch.Hit> _hits = new();
	private string _searchWhat = "kinds";

	// Zone filter.
	internal readonly Dictionary<int, ToggleButton> BiomeButtons = new();
	internal CheckBox NoBuildBox { get; } = new() { Content = "No buildings in the zone or within", IsChecked = true, FontSize = 12 };
	internal NumericUpDown DistBox { get; } = new() { Value = 2, Minimum = 0, Maximum = 50, Increment = 1, FormatString = "0", FontSize = 12, MinWidth = 70 };
	internal CheckBox NoEditBox { get; } = new() { Content = "No ground edits", IsChecked = true, FontSize = 12 };
	internal CheckBox OnlyGenBox { get; } = new() { Content = "Only zones the game generated", IsChecked = true, FontSize = 12 };
	internal TextBox RMinBox { get; } = new() { Watermark = "0", FontSize = 12, Width = 70 };
	internal TextBox RMaxBox { get; } = new() { Watermark = "10500", FontSize = 12, Width = 70 };
	internal CheckBox GroundBox { get; } = new() { Content = "Also undo their ground edits", FontSize = 12 };
	internal Button ShowMatchButton { get; } = new() { Content = "Show matching", FontSize = 12 };
	internal Button MarkButton { get; } = new() { Content = "Mark for reset…", FontSize = 12 };
	internal Button UnmarkButton { get; } = new() { Content = "Unmark all", FontSize = 12 };
	internal TextBlock ZoneInfo { get; } = new() { FontSize = 11, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private int[]? _stats;
	private bool _filtered;
	internal List<(int X, int Z)> Matches { get; private set; } = new();

	// Zone detail and the edited zones.
	private readonly StackPanel _detail = new() { Spacing = 4, IsVisible = false };
	internal TextBlock DetailText { get; } = new() { FontSize = 11, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	internal TextBlock DetailStats { get; } = new() { FontSize = 11, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private readonly TextBlock _terrainLow = Small(""), _terrainHigh = Small("");
	internal Image TerrainImage { get; } = Picture();
	internal Image HeightsImage { get; } = Picture();
	internal Image PaintImage { get; } = Picture();
	internal ListBox EditedList { get; } = new() { MaxHeight = 200, FontSize = 11 };
	private readonly Expander _editedSection;
	private List<ZoneEdit> _edited = new();

	private static readonly IBrush PanelBg = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)), Line = new SolidColorBrush(Color.FromRgb(46, 53, 63));

	private static TextBlock Small(string t) => new() { Text = t, FontSize = 10, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };

	private static Image Picture()
	{
		var i = new Image { Width = 195, Height = 195, HorizontalAlignment = HorizontalAlignment.Left };
		RenderOptions.SetBitmapInterpolationMode(i, BitmapInterpolationMode.None);
		return i;
	}

	private static TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) };

	public MapPage()
	{
		var worlds = new Button { Content = "Worlds", FontSize = 12 };
		ToolTip.SetTip(worlds, "Back to the start page to open another world");
		worlds.Click += (_, _) => BackToWorlds?.Invoke();
		SaveButton.Click += (_, _) => SaveRequested?.Invoke();
		DiscardButton.Click += (_, _) => DiscardRequested?.Invoke();
		ReloadButton.Click += (_, _) => ReloadRequested?.Invoke();
		ToolTip.SetTip(ReloadButton, "Load the world again from the running game");
		ToolTip.SetTip(BuildingsBox, "Draw the footprint of every player-built piece on the map");
		BuildingsBox.IsCheckedChanged += (_, _) => { Map.ShowBuildings = BuildingsBox.IsChecked == true; Map.RequestNextFrameRendering(); };
		GridBox.IsCheckedChanged += (_, _) => { Map.ShowGrid = GridBox.IsChecked == true; Map.RequestNextFrameRendering(); };
		EditedBox.IsCheckedChanged += (_, _) => { Map.ShowEdited = EditedBox.IsChecked == true; Map.RequestNextFrameRendering(); };
		PaintBox.IsCheckedChanged += (_, _) => { Map.ShowPaint = PaintBox.IsChecked == true; Map.RequestNextFrameRendering(); };
		CloudsBox.IsCheckedChanged += (_, _) => { Map.ShowClouds = CloudsBox.IsChecked == true; Map.RequestNextFrameRendering(); };
		EditButton.Click += (_, _) => { if (Spot is var (x, z)) EditRequested?.Invoke(x, z, Size); };
		SizeBox.SelectionChanged += (_, _) => ShowChosen();
		ToolTip.SetTip(SizeBox, "How much ground the 3D editor loads around the spot (bigger is slower)");
		Map.Picked += (x, z) => Pick((int)MathF.Floor((x + 32) / 64), (int)MathF.Floor((z + 32) / 64));
		Map.Hovered += Hover;
		Map.Status += t => _status.Text = t;
		Map.PiecesRead += () => BuildingsBox.Content = $"Show buildings ({Map.PieceCount:N0} player-built pieces)";
		Map.ViewChanged += PlaceLabels;
		Map.SizeChanged += (_, _) => PlaceLabels();
		_liveBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { LiveText, ReloadButton } };
		_changesBox = new StackPanel { Spacing = 6, Children = { Pending, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { SaveButton, DiscardButton } } } };

		// Search the world.
		ToolTip.SetTip(SearchBox, "Part of a name or text; not case sensitive");
		ToolTip.SetTip(FindButton, "Search every object of the world (Enter)");
		FindButton.Click += async (_, _) => await Search();
		SearchBox.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) await Search(); };
		SearchWhat.SelectionChanged += async (_, _) => { if (!string.IsNullOrWhiteSpace(SearchBox.Text)) await Search(); };
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
			var b = new ToggleButton { Content = name, FontSize = 11, Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 4, 4) };
			b.IsCheckedChanged += (_, _) => Refilter();
			BiomeButtons[value] = b;
			biomes.Children.Add(b);
		}
		ToolTip.SetTip(biomes, "Only these biomes (none = every biome)");
		ToolTip.SetTip(NoBuildBox, "Leave out zones with player-built pieces, and zones close to them");
		ToolTip.SetTip(NoEditBox, "Leave out zones whose ground was edited (in game or in the editor)");
		ToolTip.SetTip(OnlyGenBox, "Only zones the game has generated already (visited by a player)");
		ToolTip.SetTip(GroundBox, "Also undo the ground edits of the zones (only matters when No ground edits is off)");
		ToolTip.SetTip(ShowMatchButton, "Show the matching zones on the map (blue)");
		ToolTip.SetTip(MarkButton, "Mark the matching zones for reset (red); Save or Apply live does it");
		ToolTip.SetTip(UnmarkButton, "Cancel every zone reset that is marked");
		foreach (var c in new[] { NoBuildBox, NoEditBox, OnlyGenBox })
		{
			c.IsCheckedChanged += (_, _) => Refilter();
		}
		DistBox.ValueChanged += (_, _) => Refilter();
		RMinBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refilter(); };
		RMaxBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refilter(); };
		ShowMatchButton.Click += async (_, _) => await ShowMatching();
		MarkButton.Click += async (_, _) => await MarkMatching();
		UnmarkButton.Click += async (_, _) => await UnmarkAll();
		var zones = Section("Reset zones across the world", false,
			new TextBlock { Text = "Pick zones everywhere at once (like MCA Selector) and have the game generate them again: new trees, ore and dungeons, for example after a Valheim update. Buildings are never touched.", FontSize = 11, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap },
			biomes,
			new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { NoBuildBox, DistBox, new TextBlock { Text = "zone(s)", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } },
			NoEditBox, OnlyGenBox,
			new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "From the centre", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, RMinBox, new TextBlock { Text = "to", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, RMaxBox, new TextBlock { Text = "m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } },
			GroundBox,
			new WrapPanel { Children = { Spaced(ShowMatchButton), Spaced(MarkButton), Spaced(UnmarkButton) } },
			ZoneInfo);

		// The spot picked, and its edits up close.
		_detail.Children.Add(DetailText);
		_detail.Children.Add(H2("Terrain (with your edits)"));
		_detail.Children.Add(TerrainImage);
		_detail.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Width = 195, HorizontalAlignment = HorizontalAlignment.Left, Children = { _terrainLow, Col(Small("relief · blue = water"), 1, HorizontalAlignment.Center), Col(_terrainHigh, 2) } });
		_detail.Children.Add(H2("Your height edits"));
		_detail.Children.Add(HeightsImage);
		_detail.Children.Add(Small("blue −8 m (dug) · grey 0 · red +8 m (raised)"));
		_detail.Children.Add(H2("Paint"));
		_detail.Children.Add(PaintImage);
		_detail.Children.Add(Small("red dirt · green cultivated · blue paved · darker = grass cleared · grey = untouched"));
		_detail.Children.Add(DetailStats);
		_pickBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { PickTitle, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { EditButton, SizeBox } }, _detail } };
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
					Margin = new Thickness(12, 10),
					Children =
					{
						new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Valheim World Editor", FontSize = 16, FontWeight = FontWeight.SemiBold }, Col(worlds, 1) } },
						Meta,
						_liveBox,
						_changesBox,
						H2("Map"),
						BuildingsBox, EditedBox, PaintBox, GridBox, CloudsBox,
						_pickBox,
						new TextBlock { Text = "Click the map to pick a spot, drag to move, wheel to zoom.", FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap },
						search,
						zones,
						_editedSection,
					},
				},
			},
		};
		var surface = new Border { Background = Brushes.Transparent };
		Map.Attach(surface);
		var cursor = new Border { Background = PanelBg, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4), Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Child = Cursor };
		var status = new Border { Background = PanelBg, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = _status };
		status.IsVisible = false;
		Map.Status += t => status.IsVisible = !string.IsNullOrEmpty(t);
		View = new Grid { Background = new SolidColorBrush(Color.FromRgb(20, 23, 28)), Children = { Map, surface, _labels, side, cursor, status } };
		_players.Tick += async (_, _) => await PollPlayers();
	}

	private static Expander Section(string title, bool open, params Control[] children)
	{
		var panel = new StackPanel { Spacing = 6 };
		foreach (var c in children)
		{
			panel.Children.Add(c);
		}
		return new Expander { Header = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeight.SemiBold }, IsExpanded = open, Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6) };
	}

	private static Control Spaced(Control c)
	{
		c.Margin = new Thickness(0, 0, 6, 4);
		return c;
	}

	private static Control Col(Control c, int col, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		Grid.SetColumn(c, col);
		c.HorizontalAlignment = col == 2 ? HorizontalAlignment.Right : align;
		return c;
	}

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
			_hits = new();
			Hits.ItemsSource = null;
			SearchInfo.Text = "";
			Map.Pins = Array.Empty<Vector2>();
			Map.PinChosen = -1;
			Matches = new();
			Map.Matches = Matches;
			_filtered = false;
			ZoneInfo.Text = "";
			// The whole world at first.
			Map.LookAt(0, 0, 10500 * 2 / 900f);
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
		ToolTip.SetTip(SaveButton, session.IsLive ? "Send every pending change to the running game (everyone sees it at once)" : "Write every pending change into the world files (a backup of the world folder is made first)");
		UpdatePending();
		FillEdited();
		if (Spot is var (sx, sz))
		{
			ShowDetail(sx, sz);
		}
		_players.Stop();
		Map.Players = Array.Empty<(string, float, float)>();
		PlaceLabels();
		if (session.IsLive)
		{
			_ = PollPlayers();
			_players.Start();
		}
	}

	public void Stop() => _players.Stop();

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

	private async Task PollPlayers()
	{
		if (_session?.Live is not { } live)
		{
			return;
		}
		try
		{
			var json = System.Text.Json.Nodes.JsonNode.Parse(await live.Players())?.AsArray();
			var players = json?.Where(p => p?["name"] != null).Select(p => ((string)p!["name"]!, (float)(p["x"] ?? 0), (float)(p["z"] ?? 0))).ToList() ?? new();
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

	// The players' names next to their dots.
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

	private static string BiomeName(ValheimGen.Heightmap.Biome b) => b switch
	{
		ValheimGen.Heightmap.Biome.BlackForest => "Black Forest",
		ValheimGen.Heightmap.Biome.AshLands => "Ashlands",
		ValheimGen.Heightmap.Biome.DeepNorth => "Deep North",
		_ => b.ToString(),
	};

	// A zone picked: its name, the area the 3D editor would open around it, and its edits.
	public void Pick(int zx, int zz)
	{
		Spot = (zx, zz);
		Target = null;
		string biome = _session != null ? BiomeName(_session.Terrain.BiomeAt(zx * 64f, zz * 64f)) : "";
		PickTitle.Text = $"Zone {zx}, {zz} · {biome}";
		_pickBox.IsVisible = true;
		ShowChosen();
		ShowDetail(zx, zz);
	}

	private void ShowChosen()
	{
		if (Spot is var (x, z))
		{
			Map.Chosen = (x, z, Size);
			Map.RequestNextFrameRendering();
		}
	}

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
			_hits = new();
			Hits.ItemsSource = null;
			SearchInfo.Text = "";
			Map.Pins = Array.Empty<Vector2>();
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
		Map.Pins = _hits.Select(h => new Vector2(h.X, h.Z)).ToList();
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

	// A result picked: the map goes there, and Edit in 3D selects it (items and texts: in the inspector).
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
	private ZoneFilter Filter()
	{
		static float? Num(TextBox b) => float.TryParse(b.Text?.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : null;
		return new ZoneFilter(BiomeButtons.Where(b => b.Value.IsChecked == true).Select(b => b.Key).ToHashSet(), NoBuildBox.IsChecked == true, (int)(DistBox.Value ?? 0),
			NoEditBox.IsChecked == true, OnlyGenBox.IsChecked == true, Num(RMinBox), Num(RMaxBox));
	}

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

	private void Refilter()
	{
		if (_stats != null && _filtered)
		{
			ApplyFilter();
		}
	}

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

	internal async Task ShowMatching()
	{
		if (await Stats() != null)
		{
			ApplyFilter();
		}
	}

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
	private void FillEdited()
	{
		if (_session is not { } s)
		{
			return;
		}
		_edited = s.Edits.All().Where(e => e.HeightCount + e.PaintCount > 0).OrderByDescending(e => e.HeightCount + e.PaintCount).ToList();
		EditedList.ItemsSource = _edited.Select(e =>
		{
			var (lo, hi) = Range(e);
			return $"{e.ZoneX}, {e.ZoneZ}   {e.HeightCount} h · {e.PaintCount} p   {lo:0.0} … {hi:0.0} m{(e.Changed ? "   (not saved)" : "")}";
		}).ToList();
		((TextBlock)_editedSection.Header!).Text = $"Edited zones ({_edited.Count:N0})";
	}

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

	private void ShowDetail(int zx, int zz)
	{
		if (_session is not { } s || s.Edits.Get(zx, zz) is not { } e || e.IsEmpty)
		{
			_detail.IsVisible = false;
			return;
		}
		float[] bases = s.Terrain.BaseZone(zx, zz);
		var pics = ZonePictures.Make(e, bases, ValheimGen.TerrainService.WaterLevel);
		TerrainImage.Source = Bitmap(pics.Terrain);
		HeightsImage.Source = Bitmap(pics.Heights);
		PaintImage.Source = Bitmap(pics.Paint);
		_terrainLow.Text = $"{pics.Low:0.0} m";
		_terrainHigh.Text = $"{pics.High:0.0} m";
		DetailText.Text = $"Ground {bases.Min():0.0}–{bases.Max():0.0} m originally, {pics.Low:0.0}–{pics.High:0.0} m now (sea level {ValheimGen.TerrainService.WaterLevel:0} m)";
		var (lo, hi) = Range(e);
		DetailStats.Text = $"{e.HeightCount} heights edited ({lo:0.00} to {hi:0.00} m), {e.PaintCount} points painted.";
		_detail.IsVisible = true;
	}

	private static WriteableBitmap Bitmap(byte[] rgba)
	{
		int w = EditStore.Grid;
		var bmp = new WriteableBitmap(new PixelSize(w, w), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Opaque);
		using var fb = bmp.Lock();
		for (int y = 0; y < w; y++)
		{
			Marshal.Copy(rgba, y * w * 4, fb.Address + y * fb.RowBytes, w * 4);
		}
		return bmp;
	}
}

// The zone detail's three pictures (index.html's selectZone), north up: the ground with the edits as
// shaded relief, the height edits (blue dug, red raised), and the paint.
public static class ZonePictures
{
	public sealed record Pictures(byte[] Terrain, byte[] Heights, byte[] Paint, float Low, float High);

	private static void Put(byte[] to, int i, float r, float g, float b)
	{
		const int w = EditStore.Grid;
		// Rows run along +Z; the picture's top row is the zone's north edge.
		int x = i % w, y = w - 1 - i / w, o = (y * w + x) * 4;
		to[o] = (byte)Math.Clamp(r, 0, 255);
		to[o + 1] = (byte)Math.Clamp(g, 0, 255);
		to[o + 2] = (byte)Math.Clamp(b, 0, 255);
		to[o + 3] = 255;
	}

	private static float Mix(float a, float b, float t) => a + (b - a) * t;

	public static Pictures Make(ZoneEdit e, float[] bases, float water)
	{
		const int w = EditStore.Grid;
		var hs = new float[EditStore.Cells];
		for (int i = 0; i < hs.Length; i++)
		{
			hs[i] = bases[i] + (e.Modified[i] ? e.Level[i] + e.Smooth[i] : 0);
		}
		float lo = hs.Min(), hi = hs.Max();
		byte[] terrain = new byte[EditStore.Cells * 4], heights = new byte[EditStore.Cells * 4], paint = new byte[EditStore.Cells * 4];
		for (int i = 0; i < hs.Length; i++)
		{
			float h = hs[i];
			if (h < water)
			{
				float t = MathF.Min(1, (water - h) / 4);
				Put(terrain, i, Mix(70, 30, t), Mix(120, 60, t), Mix(170, 110, t));
			}
			else
			{
				int x = i % w, y = i / w;
				float dx = hs[y * w + Math.Min(x + 1, w - 1)] - hs[y * w + Math.Max(x - 1, 0)];
				float dz = hs[Math.Min(y + 1, w - 1) * w + x] - hs[Math.Max(y - 1, 0) * w + x];
				float shade = Math.Clamp(1 - (dx - dz) * 0.25f, 0.45f, 1.4f), t = hi > lo ? (h - lo) / (hi - lo) : 0.5f;
				Put(terrain, i, Mix(96, 190, t) * shade, Mix(128, 180, t) * shade, Mix(70, 150, t) * shade);
			}
			if (!e.Modified[i])
			{
				Put(heights, i, 42, 47, 56);
			}
			else
			{
				float t = Math.Clamp((e.Level[i] + e.Smooth[i]) / 8, -1, 1);
				if (t >= 0) Put(heights, i, Mix(42, 224, t), Mix(47, 96, t), Mix(56, 75, t));
				else Put(heights, i, Mix(42, 75, -t), Mix(47, 143, -t), Mix(56, 224, -t));
			}
			// Paint channels: dirt, cultivated, paved, vegetation allowed (0 = grass cleared).
			if (!e.PaintModified[i])
			{
				Put(paint, i, 42, 47, 56);
			}
			else
			{
				float r = e.Paint[i * 4], g = e.Paint[i * 4 + 1], b = e.Paint[i * 4 + 2], k = e.Paint[i * 4 + 3] < 0.5f ? 0.45f : 1;
				if (r + g + b < 0.05f) Put(paint, i, 96 * k, 104 * k, 88 * k);
				else Put(paint, i, r * 255 * k, g * 255 * k, b * 255 * k);
			}
		}
		return new Pictures(terrain, heights, paint, lo, hi);
	}
}
