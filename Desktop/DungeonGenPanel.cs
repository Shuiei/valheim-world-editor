using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// Generating a dungeon (the Dungeon panel's Generate): its settings, a plan of what they make (made
// again at each change; the same settings and seed always make the same dungeon), and where it goes:
// into the world above the view's centre, a portal pair joining it to the ground, or into the
// Workshop as a blueprint.
public sealed class DungeonGenPanel
{
	public Control View { get; }

	public event Action<DungeonGen.Settings, DungeonGen.Result>? PlaceAsked;
	public event Action<DungeonGen.Settings>? WorkshopAsked;

	internal ComboBox Made { get; } = Combo("Building pieces", "The game's own rooms");
	internal ComboBox Biome { get; } = Combo(DungeonKit.Biomes.Select(b => b.Name).ToArray());
	internal ComboBox Style { get; } = Combo(new[] { "The biome's" }.Concat(DungeonKit.Styles.Select(s => s.Name)).ToArray());
	internal ComboBox Walls { get; } = Combo(new[] { "The biome's" }.Concat(DungeonKit.Materials.Select(m => m.Name)).ToArray());
	internal ComboBox Size { get; } = Combo("Small", "Medium", "Large", "Huge");
	internal ComboBox Levels { get; } = Combo("1 level", "2 levels", "3 levels", "4 levels");
	internal Slider Monsters { get; } = Slide(0, 3, 1, 0.25);
	internal CheckBox Respawn { get; } = new() { Content = "Monsters come back (spawners)", IsChecked = true, FontSize = 12 };
	internal ComboBox Boss { get; } = Combo(new[] { "The biome's boss", "No boss" }.Concat(Bosses).ToArray());
	internal Slider Loot { get; } = Slide(0, 2, 1, 0.25);
	internal ComboBox Light { get; } = Combo("Dark", "Dim", "Bright");
	internal ComboBox Decor { get; } = Combo("Bare", "Furnished", "Rich");
	internal Slider Decay { get; } = Slide(0, 1, 0.15, 0.05);
	internal Slider Loops { get; } = Slide(0, 2, 1, 0.25);
	internal CheckBox BossKey { get; } = new() { Content = "Boss behind a gate, its key in a chest", IsChecked = true, FontSize = 12 };
	internal CheckBox Secrets { get; } = new() { Content = "Hidden caches", IsChecked = true, FontSize = 12 };
	internal TextBox Name { get; } = new() { FontSize = 12 };
	internal TextBox Seed { get; } = new() { FontSize = 12, Width = 110, Text = "1" };
	internal Button Randomize { get; } = new() { Content = "Randomize", FontSize = 12 };
	internal Button Place { get; } = new() { Content = "Place in the world", FontSize = 12 };
	internal Button ToWorkshop { get; } = new() { Content = "Open in the Workshop", FontSize = 12 };
	internal TextBlock Summary { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	private readonly TextBlock _monstersText = new() { FontSize = 12 }, _lootText = new() { FontSize = 12 }, _decayText = new() { FontSize = 12 }, _loopsText = new() { FontSize = 12 };
	private readonly Canvas _plan = new() { Width = 258, Height = 190, Background = Ui.Bg, ClipToBounds = true };
	private readonly StackPanel _levelButtons = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
	private readonly List<Control> _piecesOnly = new();
	private DungeonGen.Result? _result;
	private int _level;
	private bool _quiet;

	// Bosses and elites of every biome, by creature.
	private static string[] Bosses => DungeonKit.Biomes.SelectMany(b => b.Elites.Append(b.Boss)).Select(f => f.Creature).Distinct().ToArray();

	private static ComboBox Combo(params string[] items) => new() { ItemsSource = items, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };

	private static Slider Slide(double min, double max, double value, double step) =>
		new() { Minimum = min, Maximum = max, Value = value, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true };

	private static Control Row(string label, Control c) => new DockPanel
	{
		Children = { new TextBlock { Text = label, FontSize = 12, Width = 80, VerticalAlignment = VerticalAlignment.Center }, c },
	};

	public DungeonGenPanel()
	{
		Biome.SelectedIndex = 1;
		Size.SelectedIndex = 1;
		Levels.SelectedIndex = 1;
		Light.SelectedIndex = 1;
		Decor.SelectedIndex = 1;
		Name.PlaceholderText = DungeonGen.NameFor(1, null, "Black Forest");
		Made.Tip("dungeon.gen.made");
		Biome.Tip("dungeon.gen.biome");
		Style.Tip("dungeon.gen.style");
		Walls.Tip("dungeon.gen.walls");
		Size.Tip("dungeon.gen.size");
		Levels.Tip("dungeon.gen.levels");
		Monsters.Tip("dungeon.gen.monsters");
		Respawn.Tip("dungeon.gen.respawn");
		Boss.Tip("dungeon.gen.boss");
		Loot.Tip("dungeon.gen.loot");
		Light.Tip("dungeon.gen.light");
		Decor.Tip("dungeon.gen.decor");
		Decay.Tip("dungeon.gen.decay");
		Loops.Tip("dungeon.gen.loops");
		BossKey.Tip("dungeon.gen.key");
		Secrets.Tip("dungeon.gen.secrets");
		Name.Tip("dungeon.gen.name");
		Seed.Tip("dungeon.gen.seed");
		Randomize.Tip("dungeon.gen.randomize");
		Place.Tip("dungeon.gen.place");
		ToWorkshop.Tip("dungeon.gen.workshop");
		foreach (var c in new Control[] { Made, Biome, Style, Walls, Size, Levels, Boss, Light, Decor })
		{
			((ComboBox)c).SelectionChanged += (_, _) => Changed();
		}
		foreach (var sl in new[] { Monsters, Loot, Decay, Loops })
		{
			sl.ValueChanged += (_, _) => Changed();
		}
		foreach (var cb in new[] { Respawn, BossKey, Secrets })
		{
			cb.IsCheckedChanged += (_, _) => Changed();
		}
		Name.TextChanged += (_, _) => Changed();
		Seed.TextChanged += (_, _) => Changed();
		Style.SelectionChanged += (_, _) =>
		{
			// The style's own decay, until moved.
			var st = DungeonKit.StyleOf(Style.SelectedIndex > 0 ? (string)Style.SelectedItem! : DungeonKit.BiomeOf((string?)Biome.SelectedItem).Style);
			Decay.Value = Math.Round(st.Decay / 0.05) * 0.05;
		};
		Randomize.Click += (_, _) => Seed.Text = Random.Shared.Next(1, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
		Place.Click += (_, _) =>
		{
			if (_result != null)
			{
				PlaceAsked?.Invoke(Settings(), _result);
			}
		};
		ToWorkshop.Click += (_, _) => WorkshopAsked?.Invoke(Settings() with { BossKey = false, Made = DungeonGen.Made.Pieces });
		var pieces = new StackPanel
		{
			Spacing = 6,
			Children =
			{
				Row("Style", Style), Row("Walls", Walls), Row("Levels", Levels),
				new StackPanel { Spacing = 0, Children = { _lootText, Loot } },
				Row("Light", Light), Row("Furniture", Decor),
				new StackPanel { Spacing = 0, Children = { _decayText, Decay } },
				new StackPanel { Spacing = 0, Children = { _loopsText, Loops } },
				Row("Boss", Boss), BossKey, Secrets, Row("Name", Name),
			},
		};
		_piecesOnly.Add(pieces);
		_piecesOnly.Add(ToWorkshop);
		View = new StackPanel
		{
			Spacing = 6,
			Children =
			{
				Row("Made of", Made), Row("Biome", Biome), Row("Size", Size),
				new StackPanel { Spacing = 0, Children = { _monstersText, Monsters } }, Respawn,
				pieces,
				new DockPanel { Children = { new TextBlock { Text = "Seed", FontSize = 12, Width = 80, VerticalAlignment = VerticalAlignment.Center }, Randomize, Seed } },
				new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = _plan },
				_levelButtons, Summary,
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Place, ToWorkshop } },
			},
		};
		DockPanel.SetDock(Randomize, Dock.Right);
		Changed();
	}

	public DungeonGen.Settings Settings()
	{
		int.TryParse(Seed.Text?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seed);
		string? boss = Boss.SelectedIndex switch { 0 => null, 1 => "", _ => (string)Boss.SelectedItem! };
		return new DungeonGen.Settings(
			Made: Made.SelectedIndex == 1 ? DungeonGen.Made.Rooms : DungeonGen.Made.Pieces,
			Biome: (string?)Biome.SelectedItem ?? "Black Forest",
			Style: Style.SelectedIndex > 0 ? (string)Style.SelectedItem! : null,
			Walls: Walls.SelectedIndex > 0 ? (string)Walls.SelectedItem! : null,
			Size: Size.SelectedIndex + 1, Levels: Levels.SelectedIndex + 1,
			Monsters: (float)Monsters.Value, Respawn: Respawn.IsChecked == true, Boss: boss,
			Loot: (float)Loot.Value, Light: Light.SelectedIndex, Decor: Decor.SelectedIndex, Decay: (float)Decay.Value, Loops: (float)Loops.Value,
			BossKey: BossKey.IsChecked == true, Secrets: Secrets.IsChecked == true, Seed: seed,
			Name: string.IsNullOrWhiteSpace(Name.Text) ? null : Name.Text.Trim());
	}

	private void Changed()
	{
		if (_quiet)
		{
			return;
		}
		var s = Settings();
		bool pieces = s.Made == DungeonGen.Made.Pieces;
		foreach (var c in _piecesOnly)
		{
			c.IsVisible = pieces;
		}
		_monstersText.Text = $"Monsters: {(s.Monsters == 0 ? "none" : $"× {s.Monsters:0.##}")}";
		_lootText.Text = $"Loot: {(s.Loot == 0 ? "none" : $"× {s.Loot:0.##}")}";
		_decayText.Text = $"Ruin: {s.Decay * 100:0} %";
		_loopsText.Text = $"Ways round: {(s.Loops == 0 ? "none (a tree)" : $"× {s.Loops:0.##}")}";
		Name.PlaceholderText = DungeonGen.NameFor(s.Seed, s.Style, s.Biome);
		try
		{
			_result = DungeonGen.Make(s);
			Summary.Text = string.Join(" ", _result.Notes);
		}
		catch (Exception e)
		{
			_result = null;
			Summary.Text = "Could not make it: " + e.Message;
		}
		Place.IsEnabled = _result != null;
		var levels = _result?.Map?.Select(r => r.Level).Distinct().Order().ToList() ?? new List<int>();
		_level = Math.Clamp(_level, 0, Math.Max(0, levels.Count - 1));
		_levelButtons.Children.Clear();
		foreach (int l in levels)
		{
			var b = new ToggleButton { Content = $"Level {l + 1}", FontSize = 11, IsChecked = l == _level, Padding = new Thickness(6, 2) };
			b.Tip("dungeon.gen.level");
			int at = l;
			b.Click += (_, _) =>
			{
				_level = at;
				DrawPlan();
				foreach (var c in _levelButtons.Children.OfType<ToggleButton>())
				{
					c.IsChecked = c == b;
				}
			};
			_levelButtons.Children.Add(b);
		}
		_levelButtons.IsVisible = levels.Count > 1;
		DrawPlan();
	}

	// The level's rooms seen from above: the way through brighter, the boss red, treasure gold, the key
	// chest's room green, doorways as dots (the boss's gate magenta, hidden ones cyan).
	private void DrawPlan()
	{
		_plan.Children.Clear();
		if (_result?.Map is not { Count: > 0 } map)
		{
			return;
		}
		float x0 = map.Min(r => r.X0), x1 = map.Max(r => r.X1), z0 = map.Min(r => r.Z0), z1 = map.Max(r => r.Z1);
		double scale = Math.Min((_plan.Width - 8) / Math.Max(1, x1 - x0), (_plan.Height - 8) / Math.Max(1, z1 - z0));
		double X(float x) => 4 + (x - x0) * scale;
		double Y(float z) => _plan.Height - 4 - (z - z0) * scale;
		foreach (var r in map.Where(r => r.Level == _level))
		{
			var fill = r.Corridor ? Color.Parse("#3a3f4a") : r.Key ? Color.Parse("#2f7d4a") : r.Name == "Arena" ? Color.Parse("#8a2b2b")
				: r.Name is "Treasury" or "Hidden cache" ? Color.Parse("#9a8424") : r.Main ? Color.Parse("#7a5c3a") : Color.Parse("#5a4632");
			var rect = new Rectangle
			{
				Width = Math.Max(1, (r.X1 - r.X0) * scale), Height = Math.Max(1, (r.Z1 - r.Z0) * scale),
				Fill = new SolidColorBrush(fill), Stroke = r.Corridor ? null : new SolidColorBrush(Color.Parse("#b8b2a8")), StrokeThickness = 0.6,
			};
			ToolTip.SetTip(rect, r.Name);
			Canvas.SetLeft(rect, X(r.X0));
			Canvas.SetTop(rect, Y(r.Z1));
			_plan.Children.Add(rect);
		}
		foreach (var d in _result.Doors!.Where(d => d.Level == _level))
		{
			var dot = new Ellipse
			{
				Width = 4, Height = 4,
				Fill = new SolidColorBrush(d.Kind switch { "Key" => Colors.Magenta, "Secret" => Colors.Cyan, "Open" => Color.Parse("#7bc47f"), _ => Color.Parse("#e0a64b") }),
			};
			Canvas.SetLeft(dot, X(d.X) - 2);
			Canvas.SetTop(dot, Y(d.Z) - 2);
			_plan.Children.Add(dot);
		}
	}
}
