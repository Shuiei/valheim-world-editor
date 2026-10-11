using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
	internal Image Picture { get; } = new() { Width = 460, Height = 460, Stretch = Stretch.Uniform };
	// The landmarks over the picture, and which kinds show.
	internal Canvas Marks { get; } = new() { Width = 460, Height = 460 };
	internal CheckBox ShowBosses { get; } = new() { IsChecked = true, FontSize = 12 };
	internal CheckBox ShowTraders { get; } = new() { IsChecked = true, FontSize = 12 };
	internal TextBlock Stats { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Width = 460 };
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
		ShowBosses.Tip("newWorld.showBosses");
		ShowTraders.Tip("newWorld.showTraders");
		ShowBosses.IsCheckedChanged += (_, _) => PlaceMarks();
		ShowTraders.IsCheckedChanged += (_, _) => PlaceMarks();
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
		var left = new StackPanel { Spacing = 10, Width = 420, Children = { world, search } };
		var right = Ui.Card(new StackPanel
		{
			Spacing = 8,
			Children =
			{
				new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = new Panel { Children = { Picture, Marks } }, HorizontalAlignment = HorizontalAlignment.Left },
				new StackPanel
				{
					Orientation = Orientation.Horizontal, Spacing = 14,
					Children =
					{
						Legend(Mark("start"), "The start"),
						Legend(Mark("boss"), "Bosses' altars", ShowBosses),
						Legend(new StackPanel
						{
							Orientation = Orientation.Horizontal, Spacing = 10,
							Children = { Legend(Mark("trader", "Haldor"), "Haldor"), Legend(Mark("trader", "The Bog Witch"), "Bog Witch"), Legend(Mark("trader", "Hildir"), "Hildir") },
						}, "", ShowTraders),
					},
				},
				Stats,
			},
		});
		View = new ScrollViewer
		{
			Content = new StackPanel
			{
				Margin = new Thickness(20),
				Spacing = 12,
				Children =
				{
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { back, new TextBlock { Text = "A new world", FontSize = 20, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } },
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { left, new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { right } } } },
				},
			},
		};
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
			Stats.Text = "The seed is 1 to 10 characters, as in the game.";
			Picture.Source = null;
			Shown = null;
			return;
		}
		var cancel = _preview = new CancellationTokenSource();
		Stats.Text = "Looking at the world…";
		// The game's own location rules first (read once, the first time in a few seconds).
		var rules = GameLocations.UseGameRules(GameLook.Bundles);
		Pending = Task.Run(async () =>
		{
			await rules.ConfigureAwait(false);
			return SeedPreview.Make(seed, 200, parallel: true, landmarks: true, cancel.Token);
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
		Shown = p;
		Picture.Source = Draw(p, 460, startDot: false);
		Stats.Text = Describe(p.Stats) + (p.Landmarks == null
			? "\nThe bosses and traders could not be read from the game's files (the log says why): they are not shown, and the search cannot look for them."
			: "");
		PlaceMarks();
	}

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

	private static Control Legend(Control mark, string text, CheckBox? toggle = null)
	{
		mark.VerticalAlignment = VerticalAlignment.Center;
		var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { mark } };
		if (text.Length > 0)
		{
			label.Children.Add(new TextBlock { Text = text, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
		}
		if (toggle is null)
		{
			return label;
		}
		toggle.Content = label;
		return toggle;
	}

	// The shown seed's landmarks over the picture (north up), each with what it is and how far.
	internal void PlaceMarks()
	{
		Marks.Children.Clear();
		if (Shown?.Landmarks is not { } marks)
		{
			return;
		}
		var start = Shown.Stats.Start;
		// The start last: on top.
		foreach (var m in marks.OrderBy(m => m.Kind == "start"))
		{
			if ((m.Kind == "boss" && ShowBosses.IsChecked != true) || (m.Kind == "trader" && ShowTraders.IsChecked != true))
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
			Canvas.SetLeft(c, (m.X + SeedPreview.Radius) / (2 * SeedPreview.Radius) * Marks.Width - size / 2);
			Canvas.SetTop(c, (SeedPreview.Radius - m.Z) / (2 * SeedPreview.Radius) * Marks.Height - size / 2);
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

	// The preview as a picture of px pixels: the game map's biome colours, shaded by height, the sea by
	// depth, the start in red, north up.
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
				byte r, g, b;
				var (cx, cz) = p.CellCenter(i, j);
				if (cx * cx + cz * cz > SeedPreview.Radius * SeedPreview.Radius)
				{
					(r, g, b) = (18, 22, 28);
				}
				else if (p.Biome[k] == SeedPreview.Ocean)
				{
					float d = Math.Clamp((TerrainService.WaterLevel - p.Height[k]) / 60f, 0, 1);
					(r, g, b) = ((byte)(48 - 28 * d), (byte)(98 - 50 * d), (byte)(150 - 50 * d));
				}
				else
				{
					var (br, bg, bb) = MapData.PixelColor(SeedPreview.Biomes[p.Biome[k]]);
					if (p.Height[k] < TerrainService.WaterLevel)
					{
						// Water on land (swamp pools, rivers, lakes): the biome under a blue tint.
						(r, g, b) = ((byte)((br + 2 * 48) / 3), (byte)((bg + 2 * 98) / 3), (byte)((bb + 2 * 150) / 3));
					}
					else
					{
						float lift = Math.Clamp((p.Height[k] - TerrainService.WaterLevel) / 120f, 0, 1) * 0.35f;
						(r, g, b) = ((byte)(br + (255 - br) * lift), (byte)(bg + (255 - bg) * lift), (byte)(bb + (255 - bb) * lift));
					}
				}
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
