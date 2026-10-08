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

// The world map page, like the web editor's (index.html): the world drawn like the game's map, what is
// waiting to be saved (Save, or Apply live, and Discard), the map's options, search across the world,
// zones picked by filter to reset, the edited zones, and the spot picked with a click, opened in the
// 3D editor with the size chosen.
public sealed class MapPage
{
	public Control View { get; }
	public MapView Map { get; } = new();
	private WorldSession? _session;
	// Where a newly shown world's map looks first (the place remembered from the last run; null: the whole world).
	internal Func<(float X, float Z, float Mpp)?> StartView { get; set; } = () => null;

	public event Action? BackToWorlds;
	// Edit in 3D: the zone and the size in zones (Target: an object found to select there).
	public event Action<int, int, int>? EditRequested;
	// Save, apply live, discard: the window does them (it asks first).
	public event Action? SaveRequested, DiscardRequested, ReloadRequested;
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	internal Func<string, Task> Tell { get; set; } = _ => Task.CompletedTask;

	internal TextBlock Meta { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	internal TextBlock Pending { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
	internal Button SaveButton { get; } = new Button { Content = "Save to world…", FontSize = 12.5 }.Classed("primary");
	internal Button DiscardButton { get; } = new() { Content = "Discard", FontSize = 12 };
	internal TextBlock LiveText { get; } = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
	internal Button ReloadButton { get; } = new() { Content = Icons.With("reload", "Reload from the game"), FontSize = 12 };
	private readonly Control _liveBox, _changesBox;
	internal CheckBox BuildingsBox { get; } = new() { Content = "Show buildings (player-built pieces)", IsChecked = true, FontSize = 12 };
	internal CheckBox GridBox { get; } = new() { Content = "Zone grid", FontSize = 12 };
	internal CheckBox EditedBox { get; } = new() { Content = "Outline edited zones", IsChecked = true, FontSize = 12 };
	internal CheckBox PaintBox { get; } = new() { Content = "Show painted ground (dirt, paved, fields) when zoomed in", IsChecked = true, FontSize = 12 };
	internal CheckBox CloudsBox { get; } = new() { Content = "Clouds (as in game)", FontSize = 12 };
	internal TextBlock PickTitle { get; } = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
	internal Button EditButton { get; } = new Button { Content = "Edit in 3D", FontSize = 12.5 }.Classed("primary");
	internal ComboBox SizeBox { get; } = new() { ItemsSource = new[] { "3 × 3 zones (192 m)", "5 × 5 zones (320 m)", "7 × 7 zones (448 m)" }, SelectedIndex = 1, FontSize = 12 };
	private readonly Control _pickBox;
	internal TextBlock Cursor { get; } = new() { FontSize = 12, Foreground = Ui.Muted };
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
	internal Button FindButton { get; } = new Button { Content = Icons.With("search", "Find"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Stretch }.Classed("primary");
	internal TextBlock SearchInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
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
	internal Button MarkButton { get; } = new Button { Content = "Mark for reset…", FontSize = 12 }.Classed("primary");
	internal Button UnmarkButton { get; } = new() { Content = "Unmark all", FontSize = 12 };
	internal TextBlock ZoneInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	private int[]? _stats;
	private bool _filtered;
	internal List<(int X, int Z)> Matches { get; private set; } = new();

	// Zone detail and the edited zones.
	internal ListBox EditedList { get; } = new() { MaxHeight = 200, FontSize = 11 };
	private readonly Expander _editedSection;
	private List<ZoneEdit> _edited = new();

	private static readonly IBrush PanelBg = Ui.Panel, Line = Ui.Line;

	private static TextBlock H2(string t) => Ui.Heading(t, 4);

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
		EditButton.Click += (_, _) => { if (Spot is var (x, z)) EditRequested?.Invoke(x, z, Size); };
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
		RMinBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refilter(); };
		RMaxBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refilter(); };
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
						new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Inlines = { new Avalonia.Controls.Documents.Run("Valheim") { Foreground = Ui.Accent }, new Avalonia.Controls.Documents.Run(" World Editor") }, FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, Col(worlds, 1) } },
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
		var status = new Border { Background = PanelBg, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = Ui.Pad, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = _status };
		status.IsVisible = false;
		Map.Status += t => status.IsVisible = !string.IsNullOrEmpty(t);
		cursor.IsVisible = false;
		Cursor.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) cursor.IsVisible = !string.IsNullOrEmpty(Cursor.Text); };
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
		return new Expander { Header = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeight.SemiBold }, IsExpanded = open, Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch };
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
}
