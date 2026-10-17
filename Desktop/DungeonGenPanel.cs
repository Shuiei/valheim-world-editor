using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Generating a dungeon (the Dungeon panel's Generate): its settings, a plan of what they make (made
/// again at each change; the same settings and seed always make the same dungeon), and where it goes:
/// into the world above the view's centre, behind an entrance of its kind on the ground there (a portal
/// pair when it has none), or into the Workshop as a blueprint; and, once placed, the view up to it and
/// back to its entrance. In the Workshop (the Build panel's Generate dungeon) it is made of building
/// pieces only, and goes onto the plot: alone (Open on the plot) or with what is there (Add to the plot).
/// </summary>
public sealed class DungeonGenPanel
{
    /// <summary>The settings, the plan and the summary (and, outside the Workshop, <see cref="Actions"/>).</summary>
    public Control View { get; }
    /// <summary>
    /// What the dungeon made goes to: under the settings, or (in the Workshop) where the host puts it, so
    /// that it stays in sight when the settings scroll.
    /// </summary>
    public Control Actions { get; }

    /// <summary>Place in the world: the settings and the dungeon they made.</summary>
    public event Action<DungeonGen.Settings, DungeonGen.Result>? PlaceAsked;
    /// <summary>Open in the Workshop: the settings, made of building pieces.</summary>
    public event Action<DungeonGen.Settings>? WorkshopAsked;
    /// <summary>In the Workshop: the dungeon made onto the plot (true: added to what is there).</summary>
    public event Action<DungeonGen.Settings, DungeonGen.Result, bool>? PlotAsked;
    /// <summary>Go to the dungeon: the view up to the dungeon last placed.</summary>
    public event Action? DungeonAsked;
    /// <summary>Go to the entrance: the view back down to its entrance.</summary>
    public event Action? EntranceAsked;
    /// <summary>Whether a dungeon placed in the area can be gone to changed (<see cref="Placed"/>).</summary>
    public event Action<bool>? PlacedChanged;

    /// <summary>Made of building pieces or of the game's own rooms (not shown in the Workshop).</summary>
    internal ComboBox Made { get; } = Combo(Lang.T("dungeon.made.pieces"), Lang.T("dungeon.made.rooms"));
    /// <summary>The biome (Black Forest at first).</summary>
    internal ComboBox Biome { get; } = Combo([.. DungeonKit.Biomes.Select(b => b.Name)]);
    /// <summary>The style, or the biome's.</summary>
    internal ComboBox Style { get; } = Combo([Lang.T("dungeon.biomesOwn"), .. DungeonKit.Styles.Select(s => s.Name)]);
    /// <summary>The wall material, or the biome's.</summary>
    internal ComboBox Walls { get; } = Combo([Lang.T("dungeon.biomesOwn"), .. DungeonKit.Materials.Select(m => m.Name)]);
    /// <summary>Rooms per level: Small to Huge (1 to 4).</summary>
    internal ComboBox Size { get; } = Combo(Lang.T("dungeon.size.small"), Lang.T("dungeon.size.medium"), Lang.T("dungeon.size.large"), Lang.T("dungeon.size.huge"));
    /// <summary>Levels, 1 to <see cref="DungeonGen.MaxLevels"/>.</summary>
    internal ComboBox Levels { get; } = Combo([.. Enumerable.Range(1, DungeonGen.MaxLevels).Select(n => n == 1 ? Lang.T("dungeon.levels.one") : Lang.T("dungeon.levels.many", n))]);
    /// <summary>Monsters, 0 (none) to 3 times the usual.</summary>
    internal Slider Monsters { get; } = Slide(0, 3, 1, 0.25);
    /// <summary>Monsters come back (spawners).</summary>
    internal CheckBox Respawn { get; } = new() { Content = Lang.T("dungeon.label.respawn"), IsChecked = true, FontSize = 12 };
    /// <summary>The boss: the biome's, none, or any biome's boss or elite.</summary>
    internal ComboBox Boss { get; } = Combo([Lang.T("dungeon.boss.biomes"), Lang.T("dungeon.boss.none"), .. Bosses]);
    /// <summary>Loot, 0 (none) to 2 times the usual.</summary>
    internal Slider Loot { get; } = Slide(0, 2, 1, 0.25);
    /// <summary>Light: dark, dim or bright.</summary>
    internal ComboBox Light { get; } = Combo(Lang.T("dungeon.light.dark"), Lang.T("dungeon.light.dim"), Lang.T("dungeon.light.bright"));
    /// <summary>Furniture: bare, furnished or rich.</summary>
    internal ComboBox Decor { get; } = Combo(Lang.T("dungeon.decor.bare"), Lang.T("dungeon.decor.furnished"), Lang.T("dungeon.decor.rich"));
    /// <summary>Ruin, 0 to 1 (set to the style's own when the style changes).</summary>
    internal Slider Decay { get; } = Slide(0, 1, 0.15, 0.05);
    /// <summary>Ways round, 0 (a tree) to 2 times the usual.</summary>
    internal Slider Loops { get; } = Slide(0, 2, 1, 0.25);
    /// <summary>Boss behind a gate, its key in a chest.</summary>
    internal CheckBox BossKey { get; } = new() { Content = Lang.T("dungeon.label.bossKey"), IsChecked = true, FontSize = 12 };
    /// <summary>Hidden caches.</summary>
    internal CheckBox Secrets { get; } = new() { Content = Lang.T("dungeon.label.secrets"), IsChecked = true, FontSize = 12 };
    /// <summary>The name on the entrance sign; empty: the made-up one shown as its placeholder.</summary>
    internal TextBox Name { get; } = new() { FontSize = 12 };
    /// <summary>The seed (a whole number; anything else counts as 0).</summary>
    internal TextBox Seed { get; } = new() { FontSize = 12, Width = 110, Text = "1" };
    /// <summary>Puts a random seed in.</summary>
    internal Button Randomize { get; } = new() { Content = Lang.T("dungeon.label.randomize"), FontSize = 12 };
    /// <summary>Raises <see cref="PlaceAsked"/>.</summary>
    internal Button Place { get; } = new() { Content = Lang.T("dungeon.label.place"), FontSize = 12 };
    /// <summary>Raises <see cref="WorkshopAsked"/>.</summary>
    internal Button ToWorkshop { get; } = new() { Content = Lang.T("dungeon.label.toWorkshop"), FontSize = 12 };
    /// <summary>In the Workshop: the dungeon alone on the plot (<see cref="PlotAsked"/>, false).</summary>
    internal Button OpenOnPlot { get; } = new() { Content = Lang.T("dungeon.label.openOnPlot"), FontSize = 12 };
    /// <summary>In the Workshop: the dungeon added to what is on the plot (<see cref="PlotAsked"/>, true).</summary>
    internal Button AddToPlot { get; } = new() { Content = Lang.T("dungeon.label.addToPlot"), FontSize = 12 };
    /// <summary>The view up to the dungeon last placed (<see cref="DungeonAsked"/>); enabled once one is placed in the area.</summary>
    internal Button ToDungeon { get; } = new() { Content = Lang.T("dungeon.label.toDungeon"), FontSize = 12, IsEnabled = false };
    /// <summary>The view back to its entrance on the ground (<see cref="EntranceAsked"/>); enabled with <see cref="ToDungeon"/>.</summary>
    internal Button ToEntrance { get; } = new() { Content = Lang.T("dungeon.label.toEntrance"), FontSize = 12, IsEnabled = false };
    /// <summary>What the dungeon made holds (its notes), or why it could not be made.</summary>
    internal TextBlock Summary { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The sliders' labels, with their values.</summary>
    private readonly TextBlock _monstersText = new() { FontSize = 12 }, _lootText = new() { FontSize = 12 }, _decayText = new() { FontSize = 12 }, _loopsText = new() { FontSize = 12 };
    /// <summary>The plan of the level shown.</summary>
    private readonly Canvas _plan = new() { Width = 258, Height = 190, Background = Ui.Bg, ClipToBounds = true };
    /// <summary>One button per level, shown when there are several.</summary>
    private readonly StackPanel _levelButtons = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    /// <summary>The rows shown only for a dungeon of building pieces.</summary>
    private readonly List<Control> _piecesOnly = [];
    /// <summary>The dungeon last made, null before the first or when it failed (its buttons are off while another is made).</summary>
    private DungeonGen.Result? _result;
    /// <summary>
    /// Made again once the settings stop changing for a moment, away from the window's thread; only the
    /// newest is shown.
    /// </summary>
    private readonly Avalonia.Threading.DispatcherTimer _wait = new() { Interval = TimeSpan.FromMilliseconds(300) };
    /// <summary>Counts the changes, so that only the newest dungeon made is shown.</summary>
    private int _generation;

    /// <summary>The dungeon being made for the settings shown (tests wait for it).</summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;
    /// <summary>The level shown on the plan (from 0).</summary>
    private int _level;

    /// <summary>Bosses and elites of every biome, by creature.</summary>
    private static string[] Bosses => [.. DungeonKit.Biomes.SelectMany(b => b.Elites.Append(b.Boss)).Select(f => f.Creature).Distinct()];

    /// <summary>A list of these entries, the first chosen.</summary>
    private static ComboBox Combo(params string[] items) => new() { ItemsSource = items, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };

    /// <summary>A slider that steps.</summary>
    private static Slider Slide(double min, double max, double value, double step) =>
        new() { Minimum = min, Maximum = max, Value = value, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true };

    /// <summary>A labelled row.</summary>
    private static DockPanel Row(string label, Control c) => new()
    {
        Children = { new TextBlock { Text = label, FontSize = 12, Width = 80, VerticalAlignment = VerticalAlignment.Center }, c },
    };

    /// <summary>Builds the panel; the first dungeon is made after the usual pause.</summary>
    /// <param name="inWorkshop">In the Workshop: building pieces only, and Open and Add to the plot in <see cref="Actions"/>.</param>
    public DungeonGenPanel(bool inWorkshop = false)
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
        OpenOnPlot.Tip("dungeon.gen.openOnPlot");
        AddToPlot.Tip("dungeon.gen.addToPlot");
        ToDungeon.Tip("dungeon.gen.toDungeon");
        ToEntrance.Tip("dungeon.gen.toEntrance");
        ToDungeon.Click += (_, _) => DungeonAsked?.Invoke();
        ToEntrance.Click += (_, _) => EntranceAsked?.Invoke();
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
        // The style's own decay, until moved; with no style of its own (the biome's), the biome's style's, also when the
        // biome changes.
        void StyleDecay()
        {
            var st = DungeonKit.StyleOf(Style.SelectedIndex > 0 ? (string)Style.SelectedItem! : DungeonKit.BiomeOf((string?)Biome.SelectedItem).Style);
            Decay.Value = Math.Round(st.Decay / 0.05) * 0.05;
        }
        Style.SelectionChanged += (_, _) => StyleDecay();
        Biome.SelectionChanged += (_, _) =>
        {
            if (Style.SelectedIndex <= 0)
            {
                StyleDecay();
            }
        };
        Randomize.Click += (_, _) => Seed.Text = Random.Shared.Next(1, 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Place.Click += (_, _) =>
        {
            if (_result != null)
            {
                PlaceAsked?.Invoke(Settings(), _result);
            }
        };
        ToWorkshop.Click += (_, _) => WorkshopAsked?.Invoke(Settings() with { Made = DungeonGen.Made.Pieces });
        OpenOnPlot.Click += (_, _) => { if (_result != null) { PlotAsked?.Invoke(Settings(), _result, false); } };
        AddToPlot.Click += (_, _) => { if (_result != null) { PlotAsked?.Invoke(Settings(), _result, true); } };
        var pieces = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Row(Lang.T("dungeon.label.style"), Style), Row(Lang.T("dungeon.label.walls"), Walls), Row(Lang.T("dungeon.label.levels"), Levels),
                new StackPanel { Spacing = 0, Children = { _lootText, Loot } },
                Row(Lang.T("dungeon.label.light"), Light), Row(Lang.T("dungeon.label.furniture"), Decor),
                new StackPanel { Spacing = 0, Children = { _decayText, Decay } },
                new StackPanel { Spacing = 0, Children = { _loopsText, Loops } },
                Row(Lang.T("dungeon.label.boss"), Boss), BossKey, Secrets, Row(Lang.T("dialog.label.name"), Name),
            },
        };
        _piecesOnly.Add(pieces);
        _piecesOnly.Add(ToWorkshop);
        // The plot only takes building pieces (the game's rooms are kept in a world's dungeon).
        var made = Row(Lang.T("dungeon.label.madeOf"), Made);
        made.IsVisible = !inWorkshop;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.AddRange(inWorkshop ? new Control[] { OpenOnPlot, AddToPlot } : [Place, ToWorkshop]);
        Actions = actions;
        View = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                made, Row(Lang.T("dungeon.label.biome"), Biome), Row(Lang.T("dungeon.label.size"), Size),
                new StackPanel { Spacing = 0, Children = { _monstersText, Monsters } }, Respawn,
                pieces,
                new DockPanel { Children = { new TextBlock { Text = Lang.T("dungeon.label.seed"), FontSize = 12, Width = 80, VerticalAlignment = VerticalAlignment.Center }, Randomize, Seed } },
                new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = _plan },
                _levelButtons, Summary,
            },
        };
        if (!inWorkshop)
        {
            ((StackPanel)View).Children.Add(actions);
        }
        DockPanel.SetDock(Randomize, Dock.Right);
        _wait.Tick += (_, _) => Make();
        Changed();
    }

    /// <summary>
    /// Whether a dungeon placed in the area can be gone to: the Go to buttons follow (the Dungeon panel shows them
    /// outside this panel, so they stay in sight once the dungeon made is folded away).
    /// </summary>
    public void Placed(bool yes)
    {
        ToDungeon.IsEnabled = ToEntrance.IsEnabled = yes;
        PlacedChanged?.Invoke(yes);
    }

    /// <summary>The last making failed (nothing to place): the game's data may not have been read yet.</summary>
    internal bool Failed => _result == null && !_wait.IsEnabled && Pending.IsCompleted;

    /// <summary>Makes the dungeon again for the settings shown (after the usual pause).</summary>
    internal void Remake() => Changed();

    /// <summary>The settings shown.</summary>
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

    /// <summary>A setting changed: the labels follow, and the dungeon is made again after a pause.</summary>
    private void Changed()
    {
        var s = Settings();
        bool pieces = s.Made == DungeonGen.Made.Pieces;
        foreach (var c in _piecesOnly)
        {
            c.IsVisible = pieces;
        }
        _monstersText.Text = s.Monsters == 0 ? Lang.T("dungeon.label.monstersNone") : Lang.T("dungeon.label.monsters", s.Monsters);
        _lootText.Text = s.Loot == 0 ? Lang.T("dungeon.label.lootNone") : Lang.T("dungeon.label.loot", s.Loot);
        _decayText.Text = Lang.T("dungeon.label.ruin", s.Decay * 100);
        _loopsText.Text = s.Loops == 0 ? Lang.T("dungeon.label.waysNone") : Lang.T("dungeon.label.ways", s.Loops);
        // The game's rooms are named after the biome alone (the style, hidden then, is the pieces' own).
        Name.PlaceholderText = DungeonGen.NameFor(s.Seed, pieces ? s.Style : null, s.Biome);
        Place.IsEnabled = OpenOnPlot.IsEnabled = AddToPlot.IsEnabled = false;
        // A dungeon still being made for the settings before is not shown when it is done: it would come
        // back as the one to place, made for other settings.
        _generation++;
        _wait.Stop();
        _wait.Start();
    }

    /// <summary>Makes the dungeon for the settings shown, away from the window's thread (see <see cref="Pending"/>).</summary>
    private void Make()
    {
        _wait.Stop();
        var s = Settings();
        int generation = ++_generation;
        Summary.Text = Lang.T("dungeon.hint.making");
        Pending = Task.Run<(DungeonGen.Result? Result, string? Error)>(() =>
        {
            try
            {
                return (Result: DungeonGen.Make(s), Error: null);
            }
            catch (Exception e)
            {
                return (Result: null, Error: e.Message);
            }
        }).ContinueWith(t =>
        {
            if (generation == _generation)
            {
                Show(t.Result.Result, t.Result.Error);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Shows a dungeon made (or why not): its summary, the level buttons and the plan.</summary>
    private void Show(DungeonGen.Result? result, string? error)
    {
        _result = result;
        Summary.Text = result != null ? Said.Join(" ", result.Notes).Shown : Lang.T("dungeon.hint.notMade", error);
        Place.IsEnabled = OpenOnPlot.IsEnabled = AddToPlot.IsEnabled = _result != null;
        var levels = _result?.Map?.Select(r => r.Level).Distinct().Order().ToList() ?? [];
        _level = Math.Clamp(_level, 0, Math.Max(0, levels.Count - 1));
        _levelButtons.Children.Clear();
        foreach (int l in levels)
        {
            var b = new ToggleButton { Content = Lang.T("dungeon.label.level", l + 1), FontSize = 11, IsChecked = l == _level, Padding = new Thickness(6, 2) };
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

    /// <summary>
    /// The level's rooms seen from above: the way through brighter, the boss red, treasure gold, the key
    /// chest's room green, doorways as dots (the boss's gate magenta, hidden ones cyan).
    /// </summary>
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
            // A shaped room: its parts, each without an outline (they would cut it up), then its outline is its parts' edges.
            foreach (var (px0, pz0, px1, pz1) in r.Floor)
            {
                var rect = new Rectangle
                {
                    Width = Math.Max(1, (px1 - px0) * scale),
                    Height = Math.Max(1, (pz1 - pz0) * scale),
                    Fill = new SolidColorBrush(fill),
                    Stroke = r.Corridor ? null : new SolidColorBrush(Color.Parse("#b8b2a8")),
                    StrokeThickness = r.Parts == null ? 0.6 : 0.3,
                };
                ToolTip.SetTip(rect, r.Name);
                Canvas.SetLeft(rect, X(px0));
                Canvas.SetTop(rect, Y(pz1));
                _plan.Children.Add(rect);
            }
        }
        foreach (var d in _result.Doors!.Where(d => d.Level == _level))
        {
            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(d.Kind switch { "Key" => Colors.Magenta, "Secret" => Colors.Cyan, "Open" => Color.Parse("#7bc47f"), _ => Color.Parse("#e0a64b") }),
            };
            Canvas.SetLeft(dot, X(d.X) - 2);
            Canvas.SetTop(dot, Y(d.Z) - 2);
            _plan.Children.Add(dot);
        }
    }
}
