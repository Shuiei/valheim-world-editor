using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Workshop's Build panel: the hammer's pieces in its tabs (Building, Heavy building, Furniture…),
// each with a small picture, searchable; the one clicked is what the Place tool puts down, one at a
// time under the cursor. Snapping to the pieces already there, stacking on the one under the cursor,
// a grid for the rest, and the step , and . turn by. What the piece costs in game.
public sealed class BuildPanel
{
	public Control Card { get; }
	internal TextBox Search { get; } = new() { PlaceholderText = "Search pieces", FontSize = 12 };
	internal WrapPanel Tiles { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };
	internal CheckBox SnapBox { get; } = new CheckBox { Content = "Snap to pieces", FontSize = 12, IsChecked = true }.Classed("switch");
	internal ComboBox GridBox { get; } = new() { ItemsSource = new[] { "Off", "0.5 m", "1 m", "2 m" }, SelectedIndex = 0, FontSize = 12 };
	// The game turns the hammer's piece by 22.5° a step.
	internal ComboBox TurnBox { get; } = new() { ItemsSource = new[] { "1°", "15°", "22.5°", "45°", "90°" }, SelectedIndex = 2, FontSize = 12 };
	internal TextBlock Chosen { get; } = new() { FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
	internal TextBlock Cost { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(214, 190, 140)), TextWrapping = TextWrapping.Wrap };
	internal TextBlock LiftText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
	internal Button LiftReset { get; } = new() { Content = "Reset", FontSize = 11, Padding = new Thickness(6, 1) };
	// The game's models, for the pictures (null: each piece's boxes).
	internal Func<ModelStore?> Models { get; set; } = () => null;

	// The hammer's tabs (Piece.PieceCategory), in its order, then the cultivator's and the serving
	// tray's pieces (plants, feasts).
	internal const int Plants = 100, Feasts = 101;
	internal static readonly (int Category, string Name)[] Tabs =
	{
		(2, "Building"), (3, "Heavy building"), (4, "Furniture"), (1, "Crafting"), (0, "Misc"), (5, "More"), (Plants, "Plants"), (Feasts, "Feasts"),
	};

	private static readonly float[] Grids = { 0, 0.5f, 1, 2 };
	private static readonly float[] Turns = { 1, 15, 22.5f, 45, 90 };

	private readonly PlaceInput _input;
	private readonly WrapPanel _tabs = new() { ItemSpacing = 3, LineSpacing = 3 };
	private int _tab = 2;
	private readonly Dictionary<string, Bitmap?> _pictures = new();
	private readonly HashSet<string> _drawing = new();

	// Everything players build: the hammer's pieces, the cultivator's and the serving tray's (prefab,
	// English name, tab). The hoe's are ground edits, not pieces.
	internal static readonly Lazy<List<(string Prefab, string Name, int Category)>> Pieces = new(() =>
		PieceCatalog.Names.Select(n => (n, PieceCatalog.Get(StableHash.Of(n))))
			.Where(p => p.Item2 is { Tool: "hammer" or "cultivator" or "feaster" })
			.Select(p => (p.n, PieceCost.PieceName(p.n), p.Item2!.Tool switch { "cultivator" => Plants, "feaster" => Feasts, _ => p.Item2.Category }))
			.OrderBy(p => StationRank(PieceCost.Get(p.n)?.Station)).ThenBy(p => PieceCost.Get(p.n)?.Station ?? "", StringComparer.OrdinalIgnoreCase)
			.ThenBy(p => p.Item2, StringComparer.OrdinalIgnoreCase).ToList());

	// Pieces by the station they need, in the order players get them (none first: built anywhere), then
	// by name.
	internal static int StationRank(string? station) => station?.ToLowerInvariant() switch
	{
		null or "" => 0,
		"workbench" => 1,
		"stonecutter" => 2,
		"forge" => 3,
		"artisan table" => 4,
		"black forge" => 5,
		"galdr table" => 6,
		_ => 7,
	};

	public BuildPanel(PlaceInput input)
	{
		_input = input;
		foreach (var (cat, name) in Tabs)
		{
			var b = new Button { Content = name, FontSize = 11, Padding = new Thickness(7, 2), Tag = cat };
			b.Click += (_, _) => { _tab = cat; Search.Text = ""; Render(); };
			_tabs.Children.Add(b);
		}
		Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Render(); };
		SnapBox.IsCheckedChanged += (_, _) => { Tool.SnapTo = SnapBox.IsChecked == true; Tool.Notify(); };
		GridBox.SelectionChanged += (_, _) => { Tool.GridStep = Grids[Math.Max(0, GridBox.SelectedIndex)]; Tool.Notify(); };
		TurnBox.SelectionChanged += (_, _) => _input.TurnStep = Turns[Math.Max(0, TurnBox.SelectedIndex)];
		SnapBox.Tip("build.snap");
		GridBox.Tip("build.grid");
		TurnBox.Tip("build.turn");
		Search.Tip("build.search");
		LiftReset.Tip("build.lift");
		LiftReset.Click += (_, _) => { _input.Lift(-Tool.HeightNudge); ShowLift(); };
		ShowLift();
		static Control Row(string label, Control c) => new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("90,*"),
			Children = { new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Col(c) },
		};
		static Control Col(Control c)
		{
			Grid.SetColumn(c, 1);
			return c;
		}
		Card = Ui.Card(new StackPanel
		{
			Width = 340,
			Spacing = 6,
			Children =
			{
				new TextBlock { Text = "Build", FontSize = 14, FontWeight = FontWeight.SemiBold },
				Search,
				_tabs,
				new ScrollViewer { MaxHeight = 360, Content = Tiles },
				Chosen,
				Cost,
				SnapBox,
				Row("Grid", GridBox),
				Row("Turn by", TurnBox),
				Row("Lift", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { LiftText, LiftReset } }),
				new TextBlock
				{
					Text = "As the game's hammer: point at the ground or a piece, the piece touches it and snaps to the snap points within half a metre. Click to put it down. , and . turn it (Alt + wheel too; Shift: 1°); Ctrl + wheel lifts it (Shift: 0.1 m). Select (E) moves, turns and deletes pieces; Ctrl+Z undoes.",
					FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
				},
			},
		});
		Card.IsVisible = false;
	}

	private PlaceTool Tool => _input.Tool;

	// Into the Workshop: the Place tool set for building (one piece at a time, as it is, facing the
	// turn), the first piece of the tab chosen when none is.
	public void Start()
	{
		var t = Tool;
		t.Mode = PlaceTool.Modes.Brush;
		t.Building = true;
		t.OneAtATime = t.OneAtATimeByHand = true;
		t.RandomYaw = false;
		t.Tilt = 0;
		t.SizeMin = t.SizeMax = 100;
		t.Elevation = PlaceTool.Elevations.Ground;
		t.SnapTo = SnapBox.IsChecked == true;
		t.OnTop = false;
		t.GridStep = Grids[Math.Max(0, GridBox.SelectedIndex)];
		_input.TurnStep = Turns[Math.Max(0, TurnBox.SelectedIndex)];
		if (!(t.Chosen.Count == 1 && Pieces.Value.Any(p => p.Prefab == t.Chosen[0])))
		{
			Choose(Pieces.Value.FirstOrDefault(p => p.Prefab == "woodwall").Prefab ?? Pieces.Value[0].Prefab);
		}
		t.Notify();
		Render();
	}

	// The lift Ctrl + wheel gives.
	public void ShowLift()
	{
		LiftText.Text = Tool.HeightNudge == 0 ? "none (Ctrl + wheel)" : $"{Tool.HeightNudge:+0.0#;-0.0#} m";
		LiftReset.IsEnabled = Tool.HeightNudge != 0;
	}

	public void Choose(string prefab)
	{
		Tool.Chosen.Clear();
		Tool.Chosen.Add(prefab);
		Tool.Notify();
		Chosen.Text = PieceCost.PieceName(prefab);
		Cost.Text = "Costs " + PieceCost.Of(new[] { prefab }).Describe(6);
		foreach (var b in Tiles.Children.OfType<Button>())
		{
			b.Classes.Set("on", b.Tag as string == prefab);
		}
	}

	internal void Render()
	{
		Tiles.Children.Clear();
		string q = Search.Text?.Trim() ?? "";
		foreach (var b in _tabs.Children.OfType<Button>())
		{
			b.Classes.Set("on", q.Length == 0 && (int)b.Tag! == _tab);
		}
		var list = Pieces.Value.Where(p => q.Length > 0
			? q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => p.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || p.Prefab.Contains(w, StringComparison.OrdinalIgnoreCase))
			: p.Category == _tab).ToList();
		if (list.Count == 0)
		{
			Tiles.Children.Add(new TextBlock { Text = "No piece matches.", FontSize = 11, Foreground = Ui.Muted });
			return;
		}
		string? chosen = Tool.Chosen.Count == 1 ? Tool.Chosen[0] : null;
		string? group = "?";
		foreach (var (prefab, name, _) in list)
		{
			// A heading where the station changes.
			string? station = PieceCost.Get(prefab)?.Station;
			if (station != group)
			{
				group = station;
				Tiles.Children.Add(new TextBlock { Text = station ?? "No station needed", Width = 320, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, Margin = new Thickness(2, 4, 0, 0) });
			}
			var image = new Image { Width = 64, Height = 64, Source = Picture(prefab) };
			var b = new Button
			{
				Tag = prefab,
				Width = 76,
				Padding = new Thickness(2),
				Content = new StackPanel
				{
					Children =
					{
						image,
						new TextBlock { Text = name, FontSize = 10, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis },
					},
				},
			};
			ToolTip.SetTip(b, $"{name} ({prefab}) · {PieceCost.Of(new[] { prefab }).Describe(6)}");
			b.Classes.Set("on", prefab == chosen);
			b.Click += (_, _) => Choose(prefab);
			Tiles.Children.Add(b);
		}
	}

	// A piece's picture: drawn once, away from the window's thread, then shown.
	private Bitmap? Picture(string prefab)
	{
		if (_pictures.TryGetValue(prefab, out var known))
		{
			return known;
		}
		if (_drawing.Add(prefab))
		{
			var models = Models();
			Task.Run(() =>
			{
				byte[] png = BuildingPicture.Draw(new[] { new BuildingPicture.Piece(prefab, Vector3.Zero, new Vector3(0, 30, 0), 0) }, models, 96);
				Dispatcher.UIThread.Post(() =>
				{
					using var ms = new MemoryStream(png);
					_pictures[prefab] = new Bitmap(ms);
					foreach (var b in Tiles.Children.OfType<Button>().Where(b => b.Tag as string == prefab))
					{
						((b.Content as StackPanel)!.Children[0] as Image)!.Source = _pictures[prefab];
					}
				});
			});
		}
		return null;
	}
}
