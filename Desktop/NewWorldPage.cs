using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

// A new world (the start page's "A new world"): a name and a seed, the seed's world seen from above
// before it exists (SeedPreview: the game's own generator, with where it will put the start, the
// bosses' altars and the traders), a search through random seeds for one that suits (the start on a
// large landmass, the first Swamp or Mountain close, Haldor near...), and Create: a normal Valheim world
// in the game's world folder. The game lays out its locations when it first loads it, and generates its
// zones as players explore them; the editor edits what is generated.
public sealed class NewWorldPage : IDisposable
{
	public Control View { get; }

	public event Action? BackRequested;

	// The world's folder (inside a world folder of the game), its name and seed.
	public event Action<string, string, string>? CreateRequested;

	internal TextBox NameBox { get; } = new() { Text = "", PlaceholderText = "My world", FontSize = 13 };
	internal TextBox SeedBox { get; } = new() { FontSize = 13, MaxLength = 10, PlaceholderText = "Up to 10 letters or digits" };
	internal Button Roll { get; } = new() { Content = "Roll", FontSize = 12 };
	internal ComboBox Where { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
	internal Button Create { get; } = new Button { Content = "Create world", FontSize = 13 }.Classed("primary");
	internal TextBlock Problem { get; } = new() { Foreground = new SolidColorBrush(Avalonia.Media.Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
	// The seed's map, the landmarks over it, and which kinds show (switches over the map's corner, as
	// the editor's View panel).
	internal SeedMap Map { get; } = new();
	internal Canvas Marks { get; } = new() { ClipToBounds = true };
	internal CheckBox ShowStart { get; } = Toggle("start", "", "The start");
	internal CheckBox ShowBosses { get; } = Toggle("boss", "", "Bosses' altars");
	internal CheckBox ShowHaldor { get; } = Toggle("trader", "Haldor", "Haldor");
	internal CheckBox ShowBogWitch { get; } = Toggle("trader", "The Bog Witch", "Bog Witch");
	internal CheckBox ShowHildir { get; } = Toggle("trader", "Hildir", "Hildir");
	internal TextBlock Readout { get; } = new() { FontSize = 11, Foreground = Ui.Muted, Margin = new Thickness(8, 0, 0, 6), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
	// What the seed's world holds, as lists.
	internal StackPanel Info { get; } = new() { Spacing = 14 };
	// Finding a seed.
	internal CheckBox BigLand { get; } = new() { Content = "The start on a large landmass", IsChecked = true, FontSize = 12 };
	internal CheckBox LandAround { get; } = new() { Content = "Land all around the start", IsChecked = true, FontSize = 12 };
	internal CheckBox OnFoot { get; } = new() { Content = "Each biome reachable on foot from the start", FontSize = 12 };
	internal NumericUpDown Swamp { get; } = Near(1500);
	internal NumericUpDown Mountain { get; } = Near(2000);
	internal NumericUpDown Plains { get; } = Near(0);
	internal NumericUpDown Haldor { get; } = Near(0);
	internal NumericUpDown BogWitch { get; } = Near(0);
	internal NumericUpDown Hildir { get; } = Near(0);
	internal NumericUpDown Bosses { get; } = Near(0);
	internal ComboBox Count { get; } = new() { ItemsSource = new[] { "60 seeds", "150 seeds", "400 seeds" }, SelectedIndex = 1, FontSize = 12 };
	internal Button Find { get; } = new() { Content = "Find seeds", FontSize = 12 };
	internal Button Stop { get; } = new() { Content = "Stop", FontSize = 12, IsVisible = false };
	internal ProgressBar Progress { get; } = new() { Minimum = 0, Maximum = 1, Height = 6, MinHeight = 6, IsVisible = false };
	internal WrapPanel Found { get; } = new() { ItemSpacing = 6, LineSpacing = 6 };

	// The preview shown (null while it is made) and the search's results.
	internal SeedPreview.Preview? Shown { get; private set; }
	internal List<(SeedPreview.Preview Preview, float Score)> Results { get; private set; } = new();
	internal Task Pending { get; private set; } = Task.CompletedTask;

	private CancellationTokenSource? _search, _preview;
	private readonly DispatcherTimer _wait = new() { Interval = TimeSpan.FromMilliseconds(350) };

	private static NumericUpDown Near(double metres) => new()
	{
		Minimum = 0, Maximum = 8000, Increment = 250, Value = (decimal)metres, FormatString = "0", Width = 110, FontSize = 12,
	};

	// The game's world folders (that exist, the local ones first), for Where.
	internal static List<string> Roots() => Places.WorldRoots().Where(Directory.Exists).Distinct().ToList();

	public NewWorldPage(IReadOnlyList<string>? roots = null)
	{
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
		foreach (var t in new[] { ShowStart, ShowBosses, ShowHaldor, ShowBogWitch, ShowHildir })
		{
			t.IsCheckedChanged += (_, _) => PlaceMarks();
		}
		Map.Tip("newWorld.map");
		Map.ViewChanged += PlaceMarks;
		Map.Hovered += (x, z) => Readout.Text = $"{x:0}, {z:0}";
		Find.Tip("newWorld.find");
		Roll.Click += (_, _) => SeedBox.Text = SeedPreview.RandomSeed();
		SeedBox.TextChanged += (_, _) => { _wait.Stop(); _wait.Start(); };
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
				},
			},
		};
		var zoom = new StackPanel { Spacing = 4, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Children = { zoomIn, zoomOut, fit } };
		var map = new Border
		{
			BorderBrush = Ui.Line,
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			ClipToBounds = true,
			Child = new Panel { Children = { Map, Marks, Readout, toggles, zoom } },
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

	internal void StopAll()
	{
		_search?.Cancel();
		_preview?.Cancel();
	}

	// Leaving the page: what runs is stopped.
	public void Dispose()
	{
		StopAll();
		_wait.Stop();
		Map.Dispose();
		_search?.Dispose();
		_preview?.Dispose();
	}

	// The seed's preview, made away from the window's thread (only the newest is shown).
	internal void ShowSeed(string seed)
	{
		_preview?.Cancel();
		Problem.Text = "";
		if (seed.Length == 0 || seed.Length > 10)
		{
			Note("The seed is 1 to 10 characters, as in the game.");
			Map.Clear();
			Marks.Children.Clear();
			Shown = null;
			return;
		}
		var cancel = _preview = new CancellationTokenSource();
		if (Shown == null)
		{
			Note("Looking at the world…");
		}
		// The game's own location rules first (read once, the first time in a few seconds).
		var rules = GameLocations.UseGameRules(GameLook.Bundles);
		Pending = Task.Run(async () =>
		{
			await rules.ConfigureAwait(false);
			return SeedPreview.Make(seed, 512, parallel: true, landmarks: true, cancel.Token);
		}, cancel.Token).ContinueWith(t =>
		{
			if (cancel.IsCancellationRequested || t.Status != TaskStatus.RanToCompletion)
			{
				return;
			}
			Show(t.Result);
		}, TaskScheduler.FromCurrentSynchronizationContext());
	}

	private void Show(SeedPreview.Preview p)
	{
		bool same = Shown?.Seed == p.Seed;
		Shown = p;
		Map.Show(p, keepView: same);
		ShowInfo(p);
		PlaceMarks();
	}

	private void Note(string text)
	{
		Info.Children.Clear();
		Info.Children.Add(Ui.Hint(text));
	}

	// The lists beside the map: the world, its biomes, the traders, the bosses. A trader's or boss's row
	// shows the nearest on the map.
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
		Info.Children.Add(Section("Traders (the nearest spot)", SeedPreview.Landmarks.Where(l => l.Kind == "trader").Select(l => Of("trader", l.Name)).ToArray()));
		Info.Children.Add(Ui.Hint("Each trader has several spots: the first one players come near becomes the trader's camp, the others vanish."));
		Info.Children.Add(Section("Bosses (the nearest altar)", SeedPreview.Landmarks.Where(l => l.Kind == "boss").Select(l => Of("boss", l.Name)).ToArray()));
	}

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

	// One row: a mark, the name, the value, a detail under it; show: what clicking it does.
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

	// A switch for one kind of mark, with its mark beside the name.
	private static CheckBox Toggle(string kind, string name, string text) => new CheckBox
	{
		IsChecked = true,
		FontSize = 12.5,
		Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Mark(kind, name), new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } } },
	}.Classed("switch");

	// The landmark kinds' looks: the start red, bosses purple diamonds, each trader a colour of its own.
	private static SolidColorBrush Fill(string kind, string name = "") => new SolidColorBrush(kind switch
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

	private static Control Mark(string kind, string name = "")
	{
		var outline = new SolidColorBrush(Avalonia.Media.Color.FromRgb(20, 20, 24));
		return kind == "boss"
			? new Border { Width = 9, Height = 9, Background = Fill(kind), BorderBrush = outline, BorderThickness = new Thickness(1), RenderTransform = new RotateTransform(45) }
			: new Avalonia.Controls.Shapes.Ellipse { Width = kind == "start" ? 11 : 9, Height = kind == "start" ? 11 : 9, Fill = Fill(kind, name), Stroke = kind == "start" ? Brushes.White : outline, StrokeThickness = kind == "start" ? 2 : 1 };
	}

	// The shown seed's landmarks over the map, each with what it is and how far.
	internal void PlaceMarks()
	{
		Marks.Children.Clear();
		if (Shown?.Landmarks is not { } marks)
		{
			return;
		}
		var start = Shown.Stats.Start;
		bool Showing(SeedPreview.Landmark m) => (m.Kind switch
		{
			"start" => ShowStart,
			"boss" => ShowBosses,
			_ => m.Name switch { "The Bog Witch" => ShowBogWitch, "Hildir" => ShowHildir, _ => ShowHaldor },
		}).IsChecked == true;
		// The start last: on top.
		foreach (var m in marks.OrderBy(m => m.Kind == "start"))
		{
			var at = Map.ScreenOf(m.X, m.Z);
			if (!Showing(m) || at.X < -10 || at.Y < -10 || at.X > Map.Bounds.Width + 10 || at.Y > Map.Bounds.Height + 10)
			{
				continue;
			}
			var c = Mark(m.Kind, m.Name);
			float d = MathF.Sqrt((m.X - start.X) * (m.X - start.X) + (m.Z - start.Z) * (m.Z - start.Z));
			string what = m.Kind == "boss" ? $"{m.Name}'s altar" : m.Name;
			string tip = m.Kind == "start" ? $"The start: {m.X:0}, {m.Z:0}" : $"{what}: {m.X:0}, {m.Z:0}, {Km(d)} from the start";
			if (m.OneOf)
			{
				tip += $"\nOne of {marks.Count(o => o.Name == m.Name)} spots: the first one players come near becomes the camp, the others vanish.";
			}
			ToolTip.SetTip(c, tip);
			double size = c.Width;
			Canvas.SetLeft(c, at.X - size / 2);
			Canvas.SetTop(c, at.Y - size / 2);
			Marks.Children.Add(c);
		}
	}

	internal static string Km(float m) => float.IsPositiveInfinity(m) ? "none" : m < 1000 ? $"{m / 100:0}00 m" : $"{m / 1000:0.0} km";

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

	internal static string Name(Heightmap.Biome b) => b switch
	{
		Heightmap.Biome.BlackForest => "Black Forest",
		Heightmap.Biome.AshLands => "Ashlands",
		Heightmap.Biome.DeepNorth => "Deep North",
		_ => b.ToString(),
	};

	// The preview as a small picture of px pixels (a search's results): the map's colours (SeedMap.Shade),
	// the start in red, north up.
	internal static WriteableBitmap Draw(SeedPreview.Preview p, int px, bool startDot = true)
	{
		var bmp = new WriteableBitmap(new PixelSize(px, px), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
		using var fb = bmp.Lock();
		var row = new byte[px * 4];
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

	internal SeedPreview.Wishes Wishes() => new(
		BigStartLand: BigLand.IsChecked == true, LandAroundStart: LandAround.IsChecked == true,
		MaxSwampDistance: (float)(Swamp.Value ?? 0), MaxMountainDistance: (float)(Mountain.Value ?? 0), MaxPlainsDistance: (float)(Plains.Value ?? 0),
		BiomesOnStartLand: OnFoot.IsChecked == true,
		MaxHaldorDistance: (float)(Haldor.Value ?? 0), MaxBogWitchDistance: (float)(BogWitch.Value ?? 0), MaxHildirDistance: (float)(Hildir.Value ?? 0),
		MaxBossDistance: (float)(Bosses.Value ?? 0));

	// Random seeds looked at in the background; the best shown as they come.
	internal void StartSearch()
	{
		_search?.Cancel();
		var cancel = _search = new CancellationTokenSource();
		int count = Count.SelectedIndex switch { 0 => 60, 2 => 400, _ => 150 };
		var wishes = Wishes();
		Find.IsEnabled = false;
		Stop.IsVisible = Progress.IsVisible = true;
		Progress.Value = 0;
		Found.Children.Clear();
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
					return;
				}
				Results = t.Result;
				ShowResults();
			}, TaskScheduler.FromCurrentSynchronizationContext());
	}

	private void ShowResults()
	{
		Found.Children.Clear();
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
			b.Click += (_, _) => SeedBox.Text = p.Seed;
			Found.Children.Add(b);
		}
	}

	// Create: checked here, made by the window (CreateRequested).
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
		if (seed.Length == 0 || seed.Length > 10)
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
