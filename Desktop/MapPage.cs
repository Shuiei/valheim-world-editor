using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TerrainEditor.Desktop;

// The world map page, like the web editor's (index.html): the world drawn like the game's map, what is
// waiting to be saved (Save, or Apply live, and Discard), the map's options, and the spot picked with
// a click, opened in the 3D editor with the size chosen.
public sealed class MapPage
{
	public Control View { get; }
	public MapView Map { get; } = new();
	private WorldSession? _session;
	public event Action? BackToWorlds;
	// Edit in 3D: the zone and the size in zones.
	public event Action<int, int, int>? EditRequested;
	// Save, apply live, discard: the window does them (it asks first).
	public event Action? SaveRequested, DiscardRequested, ReloadRequested;

	internal TextBlock Meta { get; } = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	internal TextBlock Pending { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
	internal Button SaveButton { get; } = new() { Content = "Save to world…", FontSize = 12 };
	internal Button DiscardButton { get; } = new() { Content = "Discard", FontSize = 12 };
	internal TextBlock LiveText { get; } = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
	internal Button ReloadButton { get; } = new() { Content = "Reload from the game", FontSize = 12 };
	private readonly Control _liveBox, _changesBox;
	internal CheckBox GridBox { get; } = new() { Content = "Zone grid", FontSize = 12 };
	internal CheckBox EditedBox { get; } = new() { Content = "Outline edited zones", IsChecked = true, FontSize = 12 };
	internal CheckBox PaintBox { get; } = new() { Content = "Show painted ground (dirt, paved, fields) when zoomed in", IsChecked = true, FontSize = 12 };
	internal CheckBox CloudsBox { get; } = new() { Content = "Clouds (as in game)", FontSize = 12 };
	internal TextBlock PickTitle { get; } = new() { FontSize = 14, FontWeight = FontWeight.SemiBold };
	internal Button EditButton { get; } = new() { Content = "Edit in 3D", FontSize = 13 };
	internal ComboBox SizeBox { get; } = new() { ItemsSource = new[] { "3 × 3 zones (192 m)", "5 × 5 zones (320 m)", "7 × 7 zones (448 m)" }, SelectedIndex = 1, FontSize = 12 };
	private readonly Control _pickBox;
	internal TextBlock Cursor { get; } = new() { FontSize = 12, Foreground = Brushes.LightGray };
	private readonly TextBlock _status = new() { FontSize = 13, Foreground = Brushes.White };
	public (int X, int Z)? Spot { get; private set; }
	public int Size => new[] { 3, 5, 7 }[Math.Max(0, SizeBox.SelectedIndex)];
	private readonly DispatcherTimer _players = new() { Interval = TimeSpan.FromSeconds(5) };

	private static readonly IBrush PanelBg = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)), Line = new SolidColorBrush(Color.FromRgb(46, 53, 63));

	public MapPage()
	{
		var worlds = new Button { Content = "Worlds", FontSize = 12 };
		ToolTip.SetTip(worlds, "Back to the start page to open another world");
		worlds.Click += (_, _) => BackToWorlds?.Invoke();
		SaveButton.Click += (_, _) => SaveRequested?.Invoke();
		DiscardButton.Click += (_, _) => DiscardRequested?.Invoke();
		ReloadButton.Click += (_, _) => ReloadRequested?.Invoke();
		ToolTip.SetTip(ReloadButton, "Load the world again from the running game");
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
		_liveBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { LiveText, ReloadButton } };
		_changesBox = new StackPanel { Spacing = 6, Children = { Pending, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { SaveButton, DiscardButton } } } };
		_pickBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { PickTitle, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { EditButton, SizeBox } } } };
		var side = new Border
		{
			Background = PanelBg,
			BorderBrush = Line,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(12, 10),
			Margin = new Thickness(10),
			Width = 330,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel
			{
				Spacing = 8,
				Children =
				{
					new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Valheim World Editor", FontSize = 16, FontWeight = FontWeight.SemiBold }, Col(worlds, 1) } },
					Meta,
					_liveBox,
					_changesBox,
					new TextBlock { Text = "MAP", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) },
					EditedBox, PaintBox, GridBox, CloudsBox,
					_pickBox,
					new TextBlock { Text = "Click the map to pick a spot, drag to move, wheel to zoom.", FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap },
				},
			},
		};
		var surface = new Border { Background = Brushes.Transparent };
		Map.Attach(surface);
		var cursor = new Border { Background = PanelBg, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4), Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Child = Cursor };
		var status = new Border { Background = PanelBg, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = _status };
		status.IsVisible = false;
		Map.Status += t => status.IsVisible = !string.IsNullOrEmpty(t);
		View = new Grid { Background = new SolidColorBrush(Color.FromRgb(20, 23, 28)), Children = { Map, surface, side, cursor, status } };
		_players.Tick += async (_, _) => await PollPlayers();
	}

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
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
			Map.Chosen = null;
			_pickBox.IsVisible = false;
			// The whole world at first.
			Map.LookAt(0, 0, 10500 * 2 / 900f);
		}
		else
		{
			Map.Refresh();
		}
		var w = session.World;
		Meta.Text = session.IsLive
			? $"{w.Name} · live ({session.Label}) · seed {w.SeedName} · {w.ObjectCount:N0} objects"
			: $"{w.Name} · seed {w.SeedName} · save #{w.SaveNumber} · {w.ObjectCount:N0} objects in {w.ChunkCount} chunks";
		_liveBox.IsVisible = session.IsLive;
		SaveButton.Content = session.IsLive ? "Apply live" : "Save to world…";
		ToolTip.SetTip(SaveButton, session.IsLive ? "Send every pending change to the running game (everyone sees it at once)" : "Write every pending change into the world files (a backup of the world folder is made first)");
		UpdatePending();
		_players.Stop();
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
			var names = json?.Select(p => (string?)p?["name"]).Where(n => n != null).ToList() ?? new();
			LiveText.Text = $"Live world · {names.Count} player(s) online{(names.Count > 0 ? ": " + string.Join(", ", names) : "")}";
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
		{
			LiveText.Text = "Live world · the game does not answer right now.";
		}
	}

	private static string BiomeName(ValheimGen.Heightmap.Biome b) => b switch
	{
		ValheimGen.Heightmap.Biome.BlackForest => "Black Forest",
		ValheimGen.Heightmap.Biome.AshLands => "Ashlands",
		ValheimGen.Heightmap.Biome.DeepNorth => "Deep North",
		_ => b.ToString(),
	};

	// A zone picked: its name, and the area the 3D editor would open around it.
	public void Pick(int zx, int zz)
	{
		Spot = (zx, zz);
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
}
