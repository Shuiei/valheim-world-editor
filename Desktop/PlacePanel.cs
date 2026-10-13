using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool's panel, like the web editor's: the mode, a preset, the kinds (with weights), and the settings of the
/// mode; the kinds to choose from open in a list beside it.
/// </summary>
public sealed class PlacePanel
{
    /// <summary>The preset list's first entry: no preset (choosing it again unticks the preset's kinds).</summary>
    private static readonly string[] NoPreset = ["None"];
    /// <summary>The kinds button's text while the list of kinds is closed.</summary>
    internal const string ChooseKindsText = "Choose kinds…";
    /// <summary>The kinds button's text while the list of kinds is open.</summary>
    internal const string DoneText = "Done";

    /// <summary>The panel itself, shown over the view while the Place tool is on.</summary>
    public Control Card { get; }
    /// <summary>The list of kinds to choose from, beside the panel (hidden until Choose kinds…).</summary>
    public Control Chooser { get; }
    /// <summary>The Place tool's input, for placing shapes and the preview count.</summary>
    private readonly PlaceInput _input;
    /// <summary>The Place tool.</summary>
    private PlaceTool T => _input.Tool;
    /// <summary>The open scene, for the kinds this world can place.</summary>
    private readonly Func<WorldScene?> _scene;
    /// <summary>A prefab hash's name (null when unknown).</summary>
    private readonly Func<int, string?> _nameOf;
    /// <summary>
    /// Set while the panel fills its controls from the tool, so their change events leave the tool alone.
    /// </summary>
    private bool _filling;

    /// <summary>One button per mode.</summary>
    internal Dictionary<PlaceTool.Modes, Button> ModeButtons { get; } = [];
    /// <summary>Line mode: one button per shape (Points, Circle, Rectangle).</summary>
    internal Dictionary<PlaceTool.LineShapes, Button> ShapeButtons { get; } = [];
    /// <summary>The presets: "None", the built-in ones, then your own.</summary>
    internal ComboBox PresetBox { get; }
    /// <summary>Saves the chosen kinds, weights and scatter settings as your own preset.</summary>
    internal Button SavePresetButton { get; } = new() { Content = "Save as preset…", FontSize = 12 };
    /// <summary>Deletes the selected preset (your own only).</summary>
    internal Button DeletePresetButton { get; } = new() { Content = "Delete", FontSize = 12, IsEnabled = false };
    /// <summary>The favourite kinds as chips.</summary>
    internal WrapPanel Favourites { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };
    /// <summary>The recently placed kinds as chips.</summary>
    internal WrapPanel Recent { get; } = new() { ItemSpacing = 4, LineSpacing = 4 };
    /// <summary>The favourites and recent sections, hidden while empty.</summary>
    private readonly Control _favBox, _recentBox;
    /// <summary>What the tool remembers between runs.</summary>
    public PlaceMemory Memory { get; }
    /// <summary>Asks for a preset's name (the window's dialog; replaced by tests). Null or empty: cancelled.</summary>
    internal Func<Task<string?>> AskName { get; set; } = () => Task.FromResult<string?>(null);
    /// <summary>Asks whether to delete a preset (the window's dialog; replaced by tests).</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>The chosen kinds, each with its weight slider and share, and a button to unchoose it.</summary>
    internal StackPanel Mix { get; } = new() { Spacing = 3 };
    /// <summary>Shows or hides the list of kinds.</summary>
    internal Button KindsButton { get; } = new() { Content = ChooseKindsText, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
    /// <summary>How many kinds are chosen, after the SELECTED KINDS heading.</summary>
    private readonly TextBlock _mixCount = new() { FontSize = 10, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>
    /// Each chosen kind's share text and slider (none for a single kind), updated in place while a slider moves.
    /// </summary>
    private readonly Dictionary<string, (TextBlock Share, Slider? Slider)> _shares = [];
    /// <summary>Set while the other sliders follow a moved one, so their own change events leave the weights alone.</summary>
    private bool _sharing;
    /// <summary>Filters the list of kinds by name.</summary>
    internal TextBox Search { get; } = new() { PlaceholderText = "Search kinds (oak, rock, bush…)", FontSize = 12 };
    /// <summary>The kinds this world can place, by category.</summary>
    internal StackPanel List { get; } = new() { Spacing = 1 };
    /// <summary>The next click on an object takes its kind (Shift + click adds it).</summary>
    internal Button PickButton { get; } = new() { Content = "Pick from world", FontSize = 12 };
    /// <summary>Unchooses every kind.</summary>
    internal Button UntickAllButton { get; } = new() { Content = "Untick all", FontSize = 12 };
    /// <summary>Ticks every favourite kind.</summary>
    internal Button FavAllButton { get; } = Link("all");
    /// <summary>Unticks every favourite kind.</summary>
    internal Button FavNoneButton { get; } = Link("none");
    /// <summary>Ticks every recent kind.</summary>
    internal Button RecentAllButton { get; } = Link("all");
    /// <summary>Unticks every recent kind.</summary>
    internal Button RecentNoneButton { get; } = Link("none");
    /// <summary>
    /// The list's categories that are open (folded ones show only their header); remembered by the window.
    /// </summary>
    internal HashSet<ObjectKind> OpenKinds { get; } = [ObjectKind.Trees];
    /// <summary>A category was opened or folded (not while searching), for the window to remember.</summary>
    public event Action? OpenKindsChanged;
    /// <summary>The list's categories as last filled.</summary>
    internal Dictionary<ObjectKind, Expander> Groups { get; } = [];
    /// <summary>Shift held on the last press of a chip (a click takes Shift from its press).</summary>
    private bool _shiftPress;
    /// <summary>Where objects stand, in <c>PlaceTool.Elevations</c> order.</summary>
    internal ComboBox ElevationBox { get; } = new() { ItemsSource = new[] { "On the ground", "Above the ground", "At one height" }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>The height above the ground, or the one height for all, in metres.</summary>
    internal NumericUpDown ElevBox { get; } = new() { Value = 0, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>Snap pieces to the pieces already there (shown when pieces are chosen).</summary>
    internal CheckBox SnapToBox { get; } = new() { Content = "Snap to pieces already there", IsChecked = true, FontSize = 12 };
    /// <summary>Brush: snap a single piece beside the nearest one.</summary>
    internal Button BesideButton { get; } = new() { Content = "Beside", FontSize = 12 };
    /// <summary>Brush: snap a single piece on top of the one under the cursor.</summary>
    internal Button OnTopButton { get; } = new() { Content = "On top", FontSize = 12 };
    /// <summary>Leave saplings and crops room to grow (shown when one is chosen).</summary>
    internal CheckBox GrowBox { get; } = new() { Content = "Leave saplings and crops room to grow", IsChecked = true, FontSize = 12 };
    /// <summary>A warning: nothing to place yet, or chosen kinds that only grow on cultivated ground.</summary>
    internal TextBlock Note { get; } = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(240, 190, 90)), TextWrapping = TextWrapping.Wrap };
    /// <summary>Brush: its radius, 1 to 30 m.</summary>
    internal Slider SizeSlider { get; private set; } = null!;
    /// <summary>Objects per 100 m², 0.2 to 20.</summary>
    internal Slider DensitySlider { get; private set; } = null!;
    /// <summary>The least distance between objects, 0.5 m to <c>PlaceTool.MaxSpacing</c>.</summary>
    internal Slider SpacingSlider { get; private set; } = null!;
    /// <summary>Clumping, 0 to 100%.</summary>
    internal Slider ClumpSlider { get; private set; } = null!;
    /// <summary>The clumping pattern's size, 5 to 120 m (shown when clumping is on).</summary>
    internal Slider PatchSlider { get; private set; } = null!;
    /// <summary>The smallest random scale, 10 to 300%.</summary>
    internal NumericUpDown SizeMinBox { get; } = new() { Value = 80, Minimum = 10, Maximum = 300, Increment = 5, FormatString = "0", FontSize = 12 };
    /// <summary>The largest random scale, 10 to 300%.</summary>
    internal NumericUpDown SizeMaxBox { get; } = new() { Value = 120, Minimum = 10, Maximum = 300, Increment = 5, FormatString = "0", FontSize = 12 };
    /// <summary>The most random lean, 0 to 20 degrees.</summary>
    internal Slider TiltSlider { get; private set; } = null!;
    /// <summary>The turn added to every facing, -180 to 180 degrees.</summary>
    internal Slider RotationSlider { get; private set; } = null!;
    /// <summary>Random facing, or all facing the rotation.</summary>
    internal CheckBox RandomYawBox { get; } = new() { Content = "Random facing (off: all face the rotation)", IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Brush: one at a time, exactly at the cursor (setting it by hand stops it following the chosen kinds).
    /// </summary>
    internal CheckBox SingleBox { get; } = new() { Content = "One at a time, exactly at the cursor", FontSize = 12 };
    /// <summary>Line: pieces end to end (setting it by hand stops it following the chosen kinds).</summary>
    internal CheckBox EndToEndBox { get; } = new() { Content = "End to end (snap together, like in game)", FontSize = 12 };
    /// <summary>End to end: how many pieces high, 1 to 20.</summary>
    internal NumericUpDown LayersBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 20, Increment = 1, FormatString = "0", FontSize = 12 };
    /// <summary>Line (Points): back to the first point.</summary>
    internal CheckBox LoopBox { get; } = new() { Content = "Close the loop (back to the first point)", FontSize = 12 };
    /// <summary>Line: the distance between objects, 0.5 m to <c>PlaceTool.MaxEvery</c>.</summary>
    internal Slider EverySlider { get; private set; } = null!;
    /// <summary>Line: the random sideways stray, 0 to 5 m.</summary>
    internal Slider WiggleSlider { get; private set; } = null!;
    /// <summary>Line (Points): objects face along the line.</summary>
    internal CheckBox AlongBox { get; } = new() { Content = "Follow the line (plus the rotation; replaces random facing)", IsChecked = true, FontSize = 12 };
    /// <summary>Line (Points): a smooth curve through the points.</summary>
    internal CheckBox CurveBox { get; } = new() { Content = "Smooth curve through the points", IsChecked = true, FontSize = 12 };
    /// <summary>Grid: the spacing, 1 m to <c>PlaceTool.MaxCell</c>.</summary>
    internal Slider CellSlider { get; private set; } = null!;
    /// <summary>Places what the line, grid or zone shows (Enter).</summary>
    internal Button PlaceButton { get; } = new Button { Content = "Place (Enter)", FontSize = 12 }.Classed("primary");
    /// <summary>Clears the line, grid box or zone.</summary>
    internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
    /// <summary>Removes the line's or zone's last point.</summary>
    internal Button UndoPointButton { get; } = new() { Content = "Remove last point (Backspace)", FontSize = 12 };
    /// <summary>New random choices (R).</summary>
    internal Button NewLayoutButton { get; } = new() { Content = "New layout (R)", FontSize = 12 };
    /// <summary>What the preview shows and the keys that go with it.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    /// <summary>Kinds wider than the spacing: says so (the fit button then sets the spacing to them).</summary>
    internal TextBlock FitText { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>Sets the spacing to the widest chosen kind.</summary>
    internal Button FitButton { get; } = new() { Content = "fit", FontSize = 11, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
    /// <summary>The fit hint and its button, shown only when a chosen kind is wider than the spacing.</summary>
    private readonly Control _fitRow;
    /// <summary>Brush mode's hint: how the scatter works, or (One at a time) that a click places one.</summary>
    private readonly TextBlock _brushHint;
    /// <summary>Rows and groups that <see cref="Sync"/> shows or hides by mode, shape and what is chosen.</summary>
    private readonly Control _elevRow, _pieceBox, _attachRow, _brushRows, _scatterRows, _patchRow, _facingRows, _lineShapeBox, _lineBox, _snapRows, _freeLineRows, _loopRow, _layersRow, _gridRow, _shapeRow, _undoRow;
    /// <summary>The heading of the mode's own settings (Brush, Along the line, Grid, Zone fill).</summary>
    private readonly TextBlock _modeHead = Head("");
    /// <summary>Line mode: how to draw the chosen shape.</summary>
    private readonly TextBlock _lineHintText = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>
    /// A slider that shows its value through <c>fmt</c> in <c>shown</c> and passes each new value to <c>set</c>.
    /// </summary>
    private static Slider Slide(double min, double max, double step, double value, TextBlock shown, Func<double, string> fmt, Action<float> set)
    {
        var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true, Value = value };
        shown.Text = fmt(value);
        shown.FontSize = 12;
        shown.VerticalAlignment = VerticalAlignment.Center;
        shown.Margin = new Thickness(6, 0, 0, 0);
        s.ValueChanged += (_, e) => { shown.Text = fmt(e.NewValue); set((float)e.NewValue); };
        return s;
    }

    /// <summary>A small flat button (the all / none of the chips).</summary>
    private static Button Link(string text) => new() { Content = text, FontSize = 11, Padding = new Thickness(9, 2), MinHeight = 22, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Foreground = Ui.Muted };

    /// <summary>
    /// A kind's name as a button's or box's content: a text block, since a plain string's underscores would be taken
    /// as access keys and vanish (Pickable_Flax shown as PickableFlax). The name itself goes in the control's Tag.
    /// </summary>
    private static TextBlock Plain(string text) => new() { Text = text };

    /// <summary>A section's heading: small muted capitals.</summary>
    private static TextBlock Head(string text) => new() { Text = text.ToUpperInvariant(), FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, LetterSpacing = 0.6, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>A section of the panel: its heading (and what goes after it, on the right), then its rows.</summary>
    private static StackPanel Section(Control head, params Control[] rows)
    {
        var s = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0), Children = { head } };
        s.Children.AddRange(rows);
        return s;
    }

    /// <summary>A section of the panel with a plain heading.</summary>
    private static StackPanel Section(string title, params Control[] rows) => Section(Head(title), rows);

    /// <summary>A labelled row: the label, the input, and optionally something after it (a value or a unit).</summary>
    private static Grid Row(string label, Control input, Control? after = null)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*,Auto") };
        var l = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        Tips.Label(l, input);
        g.Children.Add(l);
        Grid.SetColumn(input, 1);
        g.Children.Add(input);
        if (after != null)
        {
            Grid.SetColumn(after, 2);
            g.Children.Add(after);
        }
        return g;
    }

    /// <summary>A floating card of the given width that scrolls when the window is too short.</summary>
    private static Border CardOf(Control child, double width) => new()
    {
        Background = Ui.Panel,
        BorderBrush = Ui.Line,
        BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = Ui.Pad,
        VerticalAlignment = VerticalAlignment.Top,
        // As tall as the window allows: the scroll bar only when it does not fit.
        Child = new ScrollViewer { Content = new StackPanel { Width = width, Spacing = 6, Children = { child } } },
    };

    /// <summary>
    /// Builds the panel and the list of kinds, and restores the chosen kinds and weights from the memory.
    /// </summary>
    /// <param name="input">The Place tool's input.</param>
    /// <param name="scene">The open scene (null while none).</param>
    /// <param name="nameOf">A prefab hash's name (null when unknown).</param>
    /// <param name="memory">What to remember with; null loads place.json (tests pass their own).</param>
    public PlacePanel(PlaceInput input, Func<WorldScene?> scene, Func<int, string?> nameOf, PlaceMemory? memory = null)
    {
        _input = input;
        _scene = scene;
        _nameOf = nameOf;
        var t = input.Tool;
        Memory = memory ?? PlaceMemory.Load();
        if (Memory.Chosen.Count > 0)
        {
            t.Chosen.Clear();
            t.Chosen.AddRange(Memory.Chosen.Where(n => !ModManifests.IsHidden(n)));
        }
        foreach (var (k, w) in Memory.Weights)
        {
            t.Weights[k] = w;
        }
        t.AutoSnap();
        input.Placed += names => { Memory.NoteRecent(names); RenderChips(); };
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var m in Enum.GetValues<PlaceTool.Modes>())
        {
            var b = new Button { Content = m.ToString(), FontSize = 12 };
            b.Click += (_, _) => SetMode(m);
            ModeButtons[m] = b;
            modes.Children.Add(b);
        }
        ModeButtons[PlaceTool.Modes.Brush].Tip("place.brush");
        ModeButtons[PlaceTool.Modes.Line].Tip("place.line");
        ModeButtons[PlaceTool.Modes.Grid].Tip("place.grid");
        ModeButtons[PlaceTool.Modes.Zone].Tip("place.zone");
        PresetBox = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch }.Tip("place.preset");
        DeletePresetButton.Tip("place.deletePreset");
        KindsButton.Tip("place.kinds");
        Search.Tip("place.search");
        PickButton.Tip("place.pick");
        ElevationBox.Tip("place.elevation");
        ElevBox.Tip("place.elev");
        SnapToBox.Tip("place.snapTo");
        GrowBox.Tip("place.grow");
        SizeMinBox.Tip("place.sizeMin");
        SizeMaxBox.Tip("place.sizeMax");
        RandomYawBox.Tip("place.randomYaw");
        SingleBox.Tip("place.single");
        EndToEndBox.Tip("place.endToEnd");
        LayersBox.Tip("place.layers");
        LoopBox.Tip("place.loop");
        AlongBox.Tip("place.along");
        CurveBox.Tip("place.curve");
        PlaceButton.Tip("place.place");
        ClearButton.Tip("place.clearShape");
        UndoPointButton.Tip("place.undoPoint");
        NewLayoutButton.Tip("place.newLayout");
        FillPresets();
        PresetBox.SelectionChanged += (_, _) =>
        {
            DeletePresetButton.IsEnabled = PresetAt(PresetBox.SelectedIndex) is { Own: true };
            if (_filling)
            {
                return;
            }
            if (PresetAt(PresetBox.SelectedIndex) is not { } chosen)
            {
                // None, after a preset: its kinds are unticked.
                if (PresetBox.SelectedIndex == 0 && T.Chosen.Count > 0)
                {
                    T.Chosen.Clear();
                    Chosen();
                }
                return;
            }
            var known = Creatable().Select(c => c.Name).ToHashSet();
            Say(t.Load(chosen.Preset, known.Contains));
            Remember();
            Fill();
            FillList();
        };
        SavePresetButton.Tip("place.savePreset");
        SavePresetButton.Click += async (_, _) =>
        {
            if (t.Chosen.Count == 0)
            {
                Say("Tick one or more kinds first.");
                return;
            }
            string? name = (await AskName())?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            var p = new PlaceTool.Preset(name, t.Chosen.ToDictionary(n => n, t.WeightOf), t.Density, t.Spacing, t.SizeMin, t.SizeMax, t.Tilt, t.Clump, t.Patch);
            Memory.Presets = [.. Memory.Presets.Where(x => x.Name != name).Append(p).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
            Memory.Save();
            FillPresets(name);
            Say($"Saved the preset {name}.");
        };
        DeletePresetButton.Click += async (_, _) =>
        {
            if (PresetAt(PresetBox.SelectedIndex) is not { Own: true } p || !await Confirm($"Delete the preset {p.Preset.Name}?"))
            {
                return;
            }
            Memory.Presets.RemoveAll(x => x.Name == p.Preset.Name);
            Memory.Save();
            FillPresets();
        };
        KindsButton.Click += (_, _) => { Chooser!.IsVisible = !Chooser.IsVisible; KindsButton.Content = Chooser.IsVisible ? DoneText : ChooseKindsText; FillList(); };
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { FillList(); } };
        PickButton.Click += (_, _) =>
        {
            input.PickOnce = (name, shift) =>
            {
                if (!Creatable().Any(c => c.Name == name))
                {
                    Say($"{name} cannot be placed: the game has no such kind to copy.");
                    return;
                }
                // A click places only that kind; Shift + click adds it to the ticked kinds.
                Choose(name, add: shift);
                Say($"Placing {string.Join(", ", T.Chosen)}.");
            };
            Say("Click an object to pick its kind for placing. Esc cancels.");
        };
        UntickAllButton.Tip("place.untickAll");
        UntickAllButton.Click += (_, _) => { T.Chosen.Clear(); Chosen(); };
        FavAllButton.Tip("place.favAll");
        FavNoneButton.Tip("place.favNone");
        RecentAllButton.Tip("place.recentAll");
        RecentNoneButton.Tip("place.recentNone");
        FavAllButton.Click += (_, _) => TickRow(Memory.Favourites, true);
        FavNoneButton.Click += (_, _) => TickRow(Memory.Favourites, false);
        RecentAllButton.Click += (_, _) => TickRow(RecentShown(), true);
        RecentNoneButton.Click += (_, _) => TickRow(RecentShown(), false);
        ElevationBox.SelectionChanged += (_, _) => { if (!_filling) { t.Elevation = (PlaceTool.Elevations)Math.Max(0, ElevationBox.SelectedIndex); t.Notify(); } Sync(); };
        ElevBox.ValueChanged += (_, e) => { if (!_filling) { t.Elev = (float)(e.NewValue ?? 0); t.Notify(); } };
        SnapToBox.IsCheckedChanged += (_, _) => { t.SnapTo = SnapToBox.IsChecked == true; t.Notify(); };
        BesideButton.Click += (_, _) => { t.OnTop = false; Sync(); t.Notify(); };
        OnTopButton.Click += (_, _) => { t.OnTop = true; Sync(); t.Notify(); };
        BesideButton.Tip("place.beside");
        OnTopButton.Tip("place.onTop");
        GrowBox.IsCheckedChanged += (_, _) => { t.GrowRoom = GrowBox.IsChecked == true; t.Notify(); };
        var sizeV = new TextBlock();
        SizeSlider = Slide(1, 30, 0.5, t.Brush.Radius, sizeV, v => $"{v:0.#} m", v => { t.Brush.Radius = v; t.Notify(); });
        var densV = new TextBlock();
        DensitySlider = Slide(0.2, 20, 0.2, t.Density, densV, v => $"{v:0.0}", v => { t.Density = v; t.Notify(); });
        var spV = new TextBlock();
        SpacingSlider = Slide(0.5, PlaceTool.MaxSpacing, 0.5, t.Spacing, spV, v => $"{v:0.#} m", v => { t.Spacing = v; t.Notify(); });
        var clV = new TextBlock();
        ClumpSlider = Slide(0, 100, 5, t.Clump, clV, v => $"{v:0}%", v => { t.Clump = v; Sync(); t.Notify(); });
        var paV = new TextBlock();
        PatchSlider = Slide(5, 120, 5, t.Patch, paV, v => $"{v:0} m", v => { t.Patch = v; t.Notify(); });
        SizeMinBox.ValueChanged += (_, e) => { t.SizeMin = (float)(e.NewValue ?? 80); t.Notify(); };
        SizeMaxBox.ValueChanged += (_, e) => { t.SizeMax = (float)(e.NewValue ?? 120); t.Notify(); };
        SizeSlider.Tip("place.size");
        DensitySlider.Tip("place.density");
        SpacingSlider.Tip("place.spacing");
        ClumpSlider.Tip("place.clump");
        PatchSlider.Tip("place.patch");
        var tiV = new TextBlock();
        TiltSlider = Slide(0, 20, 1, t.Tilt, tiV, v => $"{v:0}°", v => { t.Tilt = v; t.Notify(); });
        var roV = new TextBlock();
        RotationSlider = Slide(-180, 180, 1, t.Rotation, roV, v => $"{v:0}°", v => { if (!_filling) { t.Rotation = v; t.Notify(); } });
        RandomYawBox.IsCheckedChanged += (_, _) => { t.RandomYaw = RandomYawBox.IsChecked == true; t.Notify(); };
        SingleBox.IsCheckedChanged += (_, _) => { if (!_filling) { t.OneAtATime = SingleBox.IsChecked == true; t.OneAtATimeByHand = true; Sync(); t.Notify(); } };
        EndToEndBox.IsCheckedChanged += (_, _) => { if (!_filling) { t.EndToEnd = EndToEndBox.IsChecked == true; t.EndToEndByHand = true; Sync(); t.Notify(); } };
        LayersBox.ValueChanged += (_, e) => { t.Layers = (int)(e.NewValue ?? 1); t.Notify(); };
        LoopBox.IsCheckedChanged += (_, _) => { t.Loop = LoopBox.IsChecked == true; t.Notify(); };
        TiltSlider.Tip("place.tilt");
        RotationSlider.Tip("place.rotation");
        var evV = new TextBlock();
        EverySlider = Slide(0.5, PlaceTool.MaxEvery, 0.5, t.Every, evV, v => $"{v:0.#} m", v => { t.Every = v; t.Notify(); });
        var wiV = new TextBlock();
        WiggleSlider = Slide(0, 5, 0.25, t.Wiggle, wiV, v => $"{v:0.##} m", v => { t.Wiggle = v; t.Notify(); });
        AlongBox.IsCheckedChanged += (_, _) => { t.Along = AlongBox.IsChecked == true; t.Notify(); };
        CurveBox.IsCheckedChanged += (_, _) => { t.Curve = CurveBox.IsChecked == true; t.Notify(); };
        EverySlider.Tip("place.every");
        WiggleSlider.Tip("place.wiggle");
        var ceV = new TextBlock();
        CellSlider = Slide(1, PlaceTool.MaxCell, 0.5, t.Cell, ceV, v => $"{v:0.#} m", v => { t.Cell = v; t.Notify(); });
        CellSlider.Tip("place.cell");
        var shapes = new UniformGrid { Rows = 1, ColumnSpacing = 4 };
        foreach (var (ls, label, tip) in new[] { (PlaceTool.LineShapes.Points, "Points", "Click points along the way, or hold and drag to draw freely"),
            (PlaceTool.LineShapes.Circle, "Circle", "Press at the centre and drag out to the size you want"), (PlaceTool.LineShapes.Rect, "Rectangle", "Press at one corner and drag to the opposite corner") })
        {
            var b = new Button { Content = label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => { t.LineShape = ls; t.ClearShape(); Sync(); };
            ShapeButtons[ls] = b;
            shapes.Children.Add(b);
        }
        PlaceButton.Click += (_, _) => input.PlaceShape();
        ClearButton.Click += (_, _) => t.ClearShape();
        UndoPointButton.Click += (_, _) => input.RemoveLastPoint();
        NewLayoutButton.Click += (_, _) => t.NewLayout();
        FitButton.Tip("place.fit");
        FitButton.Click += (_, _) =>
        {
            if (t.Fit())
            {
                Fill();
                t.Notify();
            }
        };
        static TextBlock Hint(string s) => new() { Text = s, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

        _elevRow = Row("Height", ElevBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        _attachRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { BesideButton, OnTopButton } };
        _pieceBox = new StackPanel { Spacing = 4, Children = { SnapToBox, _attachRow } };
        _brushRows = new StackPanel { Spacing = 4, Children = { Row("Brush size", SizeSlider, sizeV), SingleBox } };
        _patchRow = Row("Patch size", PatchSlider, paV);
        _scatterRows = new StackPanel { Spacing = 4, Children = { Row("Density", DensitySlider, densV), Row("Spacing", SpacingSlider, spV), Row("Clumping", ClumpSlider, clV), _patchRow } };
        _facingRows = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Row("Size %", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SizeMinBox, SizeMaxBox } }),
                Row("Tilt", TiltSlider, tiV),
                Row("Rotation", RotationSlider, roV),
                RandomYawBox,
            },
        };
        _layersRow = Row("Layers", LayersBox);
        _loopRow = LoopBox;
        _snapRows = new StackPanel { Spacing = 4, Children = { EndToEndBox, _layersRow } };
        _freeLineRows = new StackPanel { Spacing = 4, Children = { Row("Every", EverySlider, evV), Row("Wiggle", WiggleSlider, wiV), AlongBox } };
        _lineShapeBox = Section("Line shape", shapes, _lineHintText);
        _lineBox = new StackPanel { Spacing = 4, Children = { _snapRows, _loopRow, _freeLineRows, CurveBox } };
        _gridRow = Row("Spacing", CellSlider, ceV);
        _shapeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PlaceButton, ClearButton } };
        _fitRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { FitText, Col(FitButton, 1) } };
        _undoRow = UndoPointButton;
        _brushHint = Hint("Density is objects per 100 m². Spacing keeps them apart (also from what is already there). Shift + drag removes the chosen kinds. The Mask applies.");

        Card = CardOf(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Place", FontSize = 14, FontWeight = FontWeight.SemiBold },
                modes,
                _lineShapeBox,
                Section("Choose a preset", PresetBox, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SavePresetButton, DeletePresetButton } }),
                Section(
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Head("Selected kinds"), _mixCount } },
                    new Border { Background = Ui.Panel2, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 4), Child = Mix },
                    Note,
                    KindsButton),
                Section("Objects", Row("Elevation", ElevationBox), _elevRow, _pieceBox, GrowBox, _facingRows),
                Section(_modeHead, _brushRows, _scatterRows, _lineBox, _gridRow, _fitRow),
                new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0), Children = { _shapeRow, _undoRow, NewLayoutButton, Info, _brushHint } },
            },
        }, 320);
        Chooser = CardOf(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Choose kinds", FontSize = 14, FontWeight = FontWeight.SemiBold },
                Search,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PickButton, UntickAllButton } },
                (_favBox = new StackPanel { Spacing = 3, Children = { ChipHead("FAVOURITES", FavAllButton, FavNoneButton), Favourites } }),
                (_recentBox = new StackPanel { Spacing = 3, Children = { ChipHead("RECENT", RecentAllButton, RecentNoneButton), Recent } }),
                List,
            },
        }, 240);
        Chooser.IsVisible = false;
        input.Changed += ShowInfo;
        Fill();
        RenderChips();
    }

    /// <summary>A preset in the list.</summary>
    /// <param name="Preset">The preset.</param>
    /// <param name="Own">One of yours (it can be deleted), not a built-in one.</param>
    private sealed record PresetEntry(PlaceTool.Preset Preset, bool Own);

    /// <summary>The preset at an index of the list, or null for the first entry or past the end.</summary>
    private PresetEntry? PresetAt(int i) => i <= 0 ? null : i <= PlaceTool.BuiltIn.Length ? new(PlaceTool.BuiltIn[i - 1], false)
        : i - 1 - PlaceTool.BuiltIn.Length < Memory.Presets.Count ? new(Memory.Presets[i - 1 - PlaceTool.BuiltIn.Length], true) : null;

    /// <summary>Fills the preset list, selecting your own preset of that name if given.</summary>
    private void FillPresets(string? select = null)
    {
        _filling = true;
        PresetBox.ItemsSource = NoPreset.Concat(PlaceTool.BuiltIn.Select(p => p.Name)).Concat(Memory.Presets.Select(p => $"Yours: {p.Name}")).ToList();
        int own = Memory.Presets.FindIndex(p => p.Name == select);
        PresetBox.SelectedIndex = own >= 0 ? 1 + PlaceTool.BuiltIn.Length + own : 0;
        DeletePresetButton.IsEnabled = own >= 0;
        _filling = false;
    }

    /// <summary>Remembers the chosen kinds and weights for the next time (saves the memory).</summary>
    private void Remember()
    {
        Memory.Chosen = [.. T.Chosen];
        Memory.Weights = new(T.Weights);
        Memory.Save();
    }

    /// <summary>The heading of a chip row, with its all and none buttons.</summary>
    private static StackPanel ChipHead(string title, Button all, Button none) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
        Margin = new Thickness(0, 6, 0, 2),
        Children = { new TextBlock { Text = title, FontSize = 10, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, all, none },
    };

    /// <summary>
    /// The Recent row's kinds: the chosen kinds that are not among them yet, then the ones placed lately (newest
    /// first), so what is chosen always shows.
    /// </summary>
    private List<string> RecentShown() => [.. T.Chosen.Where(n => !Memory.Recent.Contains(n)).Concat(Memory.Recent)];

    /// <summary>all / none: tick or untick every kind of the row (those this world can place).</summary>
    private void TickRow(IEnumerable<string> names, bool on)
    {
        var known = Creatable().Select(c => c.Name).ToHashSet();
        foreach (string? n in names.Where(n => known.Count == 0 || known.Contains(n)).ToList())
        {
            if (on && !T.Chosen.Contains(n))
            {
                T.Chosen.Add(n);
            }
            else if (!on)
            {
                T.Chosen.Remove(n);
            }
        }
        Chosen();
    }

    /// <summary>A kind chosen from a chip or the world: add (or take away if ticked), or place only it.</summary>
    internal void Choose(string name, bool add)
    {
        if (!add)
        {
            T.Chosen.Clear();
            T.Chosen.Add(name);
        }
        else if (!T.Chosen.Remove(name))
        {
            T.Chosen.Add(name);
        }
        Chosen();
    }

    /// <summary>A chip clicked: ticks or unticks that kind; with Shift, places only it.</summary>
    internal void ChipClicked(string name, bool shift) => Choose(name, add: !shift);

    /// <summary>
    /// Favourites and recent kinds as buttons: a click ticks or unticks that kind, Shift + click places only it.
    /// </summary>
    private void RenderChips()
    {
        var known = Creatable().Select(c => c.Name).ToHashSet();
        foreach (var (panel, list, box) in new[] { (Favourites, Memory.Favourites, _favBox), (Recent, RecentShown(), _recentBox) })
        {
            panel.Children.Clear();
            foreach (string? n in list.Where(n => known.Count == 0 || known.Contains(n)))
            {
                var b = new ToggleButton { Content = Plain(n), Tag = n, IsChecked = T.Chosen.Contains(n), FontSize = 11, Padding = new Thickness(8, 3) }.Tip(ReferenceEquals(panel, Favourites) ? "place.favourite" : "place.recent");
                b.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, e) => _shiftPress = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
                b.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, _) => _shiftPress = false, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
                b.Click += (_, _) =>
                {
                    bool shift = _shiftPress;
                    _shiftPress = false;
                    // The button turned itself on or off: what is ticked decides.
                    b.IsChecked = T.Chosen.Contains(n);
                    ChipClicked(n, shift);
                };
                panel.Children.Add(b);
            }
            box.IsVisible = panel.Children.Count > 0;
        }
    }

    /// <summary>Puts a control in a grid column; returns it.</summary>
    private static Control Col(Control c, int col)
    {
        Grid.SetColumn(c, col);
        return c;
    }

    /// <summary>A line for the status bar.</summary>
    public event Action<string>? Message;
    /// <summary>Raises <see cref="Message"/>.</summary>
    private void Say(string s) => Message?.Invoke(s);

    /// <summary>The kinds that can be placed in this world, with their kind of object (for the list's order).</summary>
    private List<(string Name, ObjectKind Kind)> _creatable = [];
    /// <summary>The world and the manifests <c>_creatable</c> was worked out from.</summary>
    private (Save.WorldSave? World, int Manifests) _creatableFrom;
    /// <summary>
    /// The kinds that can be placed in this world, with their kind of object; worked out again for another world or
    /// when the mods' manifests change (empty while no world is open).
    /// </summary>
    internal List<(string Name, ObjectKind Kind)> Creatable()
    {
        if (_scene()?.World is { } w && _creatableFrom != (w, ModManifests.Generation))
        {
            _creatableFrom = (w, ModManifests.Generation);
            // Not the prefabs a mod's manifest asks not to offer (its tools, effects, helpers), nor chosen any more.
            _creatable = [.. w.Creatable.Select(p => _nameOf(p)).Where(n => n != null && !ModManifests.IsHidden(n)).Distinct().Select(n => (n!, ObjectKinds.Of(n, false)))];
            T.Chosen.RemoveAll(ModManifests.IsHidden);
        }
        return _creatable;
    }

    /// <summary>
    /// Whether a kind matches the search: by its name, or for spawn points and nests by a creature they spawn.
    /// </summary>
    internal static bool Matches(string name, string q) =>
        name.Contains(q, StringComparison.OrdinalIgnoreCase) || PrefabCatalog.SpawnOf(name) is { } spawn && spawn.Creatures.Any(c => c.Contains(q, StringComparison.OrdinalIgnoreCase));

    /// <summary>The list's categories, in order.</summary>
    private static readonly ObjectKind[] Order =
    [
        ObjectKind.Trees, ObjectKind.Rocks, ObjectKind.Bushes, ObjectKind.Pickables, ObjectKind.Ore, ObjectKind.Ruins, ObjectKind.Furniture, ObjectKind.Remains,
        ObjectKind.SpawnPoints, ObjectKind.Nests, ObjectKind.Creatures, ObjectKind.Effects, ObjectKind.Buildings, ObjectKind.Other,
    ];

    /// <summary>
    /// Fills the list of kinds (while it shows), filtered by the search: a folding category per kind of object, its
    /// checkboxes made when it opens.
    /// </summary>
    public void FillList()
    {
        if (!Chooser.IsVisible)
        {
            return;
        }
        string q = Search.Text?.Trim() ?? "";
        List.Children.Clear();
        Groups.Clear();
        foreach (var k in Order)
        {
            var names = Creatable().Where(c => c.Kind == k && (q == "" || Matches(c.Name, q))).Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0)
            {
                continue;
            }
            // One category that folds; searching opens every category with a match (not remembered).
            var body = new StackPanel { Spacing = 1 };
            var g = new Expander { HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(2), IsExpanded = q != "" || OpenKinds.Contains(k), Content = body }.Tip("place.group");
            void Header() => g.Header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock { Text = ObjectKinds.Label(k).ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted },
                    Col(new TextBlock { Text = names.Count(T.Chosen.Contains) is int on && on > 0 ? $"{on} / {names.Count}" : $"{names.Count}", FontSize = 10.5, Foreground = names.Any(T.Chosen.Contains) ? Ui.Accent : Ui.Muted, Margin = new Thickness(8, 0, 0, 0) }, 1),
                },
            };
            Header();
            // The checkboxes are made when the category opens (some hold hundreds of kinds).
            void Fill()
            {
                if (body.Children.Count > 0 || !g.IsExpanded)
                {
                    return;
                }
                foreach (string? n in names)
                {
                    var box = new CheckBox { Content = Plain(n), Tag = n, IsChecked = T.Chosen.Contains(n), FontSize = 12 }.Tip("place.kind");
                    box.IsCheckedChanged += (_, _) =>
                    {
                        if (box.IsChecked == true)
                        {
                            if (!T.Chosen.Contains(n))
                            {
                                T.Chosen.Add(n);
                            }
                        }
                        else
                        {
                            T.Chosen.Remove(n);
                        }

                        Header();
                        Chosen(fillList: false);
                    };
                    bool fav = Memory.Favourites.Contains(n);
                    var star = new Button { Content = fav ? "★" : "☆", FontSize = 12, Padding = new Thickness(4, 0), Background = Brushes.Transparent };
                    ToolTip.SetTip(star, fav ? "Remove from favourites" : "Add to favourites");
                    star.Click += (_, _) => { Memory.ToggleFavourite(n); FillList(); RenderChips(); };
                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { box, Col(star, 1) } };
                    // Spawn points and nests: what they spawn, under their name.
                    body.Children.Add(PrefabCatalog.SpawnOf(n) is { } spawn
                        ? new StackPanel { Children = { row, new TextBlock { Text = spawn.Summary(), FontSize = 10.5, Foreground = Ui.Muted, Margin = new Thickness(26, -4, 0, 2), TextWrapping = TextWrapping.Wrap } } }
                        : row);
                }
            }
            Fill();
            g.PropertyChanged += (_, e) =>
            {
                if (e.Property != Expander.IsExpandedProperty)
                {
                    return;
                }
                Fill();
                if (q != "")
                {
                    return;
                }
                if (g.IsExpanded ? OpenKinds.Add(k) : OpenKinds.Remove(k))
                {
                    OpenKindsChanged?.Invoke();
                }
            };
            Groups[k] = g;
            List.Children.Add(g);
        }
        if (List.Children.Count == 0)
        {
            List.Children.Add(new TextBlock { Text = "Nothing matches.", FontSize = 11, Foreground = Ui.Muted });
        }
    }

    /// <summary>
    /// The chosen kinds changed: updates End to end and One at a time, the panel and the list, remembers them and makes
    /// a new layout.
    /// </summary>
    private void Chosen(bool fillList = true)
    {
        T.AutoSnap();
        _filling = true;
        PresetBox.SelectedIndex = 0;
        _filling = false;
        Fill();
        if (fillList)
        {
            FillList();
        }
        Remember();
        RenderChips();
        T.NewLayout();
    }

    /// <summary>
    /// Fills the mix: each chosen kind on a row of the same layout, with its share, a button to unchoose it, and a weight
    /// slider (1 to 10) when there are several; "None yet" while nothing is chosen.
    /// </summary>
    private void RenderMix()
    {
        Mix.Children.Clear();
        _shares.Clear();
        var names = T.Chosen.ToList();
        _mixCount.Text = names.Count > 0 ? $"{names.Count}" : "";
        if (names.Count == 0)
        {
            Mix.Children.Add(new TextBlock { Text = "None yet", FontSize = 12, Foreground = Ui.Muted, Margin = new Thickness(0, 4) });
            return;
        }
        float total = names.Sum(T.WeightOf);
        foreach (string? n in names)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,80,40,Auto"), ColumnSpacing = 4, MinHeight = 30 };
            g.Children.Add(new TextBlock { Text = n, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var pct = new TextBlock { FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Text = $"{T.WeightOf(n) / total * 100:0}%" };
            Slider? w = null;
            if (names.Count > 1)
            {
                // The slider is the kind's share in %, the equal share in its middle (it ends at twice that, each of the
                // others keeping at least 1%); a share past its end shows at the end, the real one in the text.
                w = new Slider { Minimum = 1, Maximum = MaxShare(names.Count), TickFrequency = 1, IsSnapToTickEnabled = true, Value = Math.Round(T.WeightOf(n) / total * 100), VerticalAlignment = VerticalAlignment.Center };
                ToolTip.SetTip(w, $"Share of {n}: {Tips.Of("place.weight")}");
                // Rows stay (rebuilding them would drop the slider being dragged); the others follow.
                w.ValueChanged += (_, e) =>
                {
                    if (_sharing)
                    {
                        return;
                    }
                    Share(n, (int)Math.Round(e.NewValue));
                    Remember();
                    T.NewLayout();
                };
                g.Children.Add(Col(w, 1));
            }
            _shares[n] = (pct, w);
            g.Children.Add(Col(pct, 2));
            var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(x, $"{Tips.Of("place.unchoose")} ({n})");
            x.Click += (_, _) => { T.Chosen.Remove(n); Chosen(); };
            g.Children.Add(Col(x, 3));
            Mix.Children.Add(g);
        }
    }

    /// <summary>
    /// A kind's slider moved to <paramref name="percent"/>: the other kinds share the rest in their current
    /// proportions (whole percents, each at least 1, summing to 100), and their sliders and shares follow.
    /// </summary>
    private void Share(string kind, int percent)
    {
        var others = _shares.Keys.Where(k => k != kind).ToList();
        int rest = 100 - percent;
        float before = others.Sum(T.WeightOf);
        var raw = others.Select(k => before > 0 ? rest * T.WeightOf(k) / before : (float)rest / others.Count).ToList();
        var whole = raw.Select(r => Math.Max(1, (int)MathF.Floor(r))).ToList();
        // What rounding down left over goes to the largest remainders.
        foreach (int i in Enumerable.Range(0, others.Count).OrderByDescending(i => raw[i] - MathF.Floor(raw[i])).Take(Math.Max(0, rest - whole.Sum())))
        {
            whole[i]++;
        }
        T.Weights[kind] = percent;
        for (int i = 0; i < others.Count; i++)
        {
            T.Weights[others[i]] = whole[i];
        }
        _sharing = true;
        foreach (string k in others)
        {
            if (_shares[k].Slider is { } s)
            {
                s.Value = T.WeightOf(k);
            }
        }
        _sharing = false;
        ShowShares();
    }

    /// <summary>
    /// The end of the share sliders with this many kinds: twice the equal share, so the equal share is in the middle
    /// (99% for 2 kinds, 67% for 3, 50% for 4), leaving the others at least 1% each.
    /// </summary>
    internal static int MaxShare(int kinds) => Math.Min(100 - (kinds - 1), (int)Math.Round(200.0 / kinds));

    /// <summary>Writes each chosen kind's share of the weights on its row.</summary>
    private void ShowShares()
    {
        float total = _shares.Keys.Sum(T.WeightOf);
        foreach (var (n, row) in _shares)
        {
            row.Share.Text = $"{T.WeightOf(n) / total * 100:0}%";
        }
    }

    /// <summary>Switches the mode.</summary>
    private void SetMode(PlaceTool.Modes m)
    {
        T.Mode = m;
        Sync();
        T.Notify();
    }

    /// <summary>
    /// Fills the controls from the tool's settings; the settings shown follow the mode and what is chosen.
    /// </summary>
    public void Fill()
    {
        _filling = true;
        var t = T;
        ElevationBox.SelectedIndex = (int)t.Elevation;
        ElevBox.Value = (decimal)t.Elev;
        RotationSlider.Value = t.Rotation;
        SingleBox.IsChecked = t.OneAtATime;
        EndToEndBox.IsChecked = t.EndToEnd;
        DensitySlider.Value = t.Density;
        SpacingSlider.Value = t.Spacing;
        ClumpSlider.Value = t.Clump;
        PatchSlider.Value = t.Patch;
        SizeMinBox.Value = (decimal)t.SizeMin;
        SizeMaxBox.Value = (decimal)t.SizeMax;
        TiltSlider.Value = t.Tilt;
        RandomYawBox.IsChecked = t.RandomYaw;
        EverySlider.Value = t.Every;
        CellSlider.Value = t.Cell;
        _filling = false;
        RenderMix();
        Sync();
    }

    /// <summary>
    /// Shows the rows the mode, shape and chosen kinds use, marks the buttons that are on, and updates the hints.
    /// </summary>
    private void Sync()
    {
        var t = T;
        var m = t.Mode;
        foreach (var (k, b) in ModeButtons)
        {
            b.Classes.Set("on", k == m);
        }
        foreach (var (k, b) in ShapeButtons)
        {
            b.Classes.Set("on", k == t.LineShape);
        }
        BesideButton.Classes.Set("on", !(t.OnTop));
        OnTopButton.Classes.Set("on", t.OnTop);
        bool line = m == PlaceTool.Modes.Line, endToEnd = line && t.EndToEnd;
        _elevRow.IsVisible = t.Elevation != PlaceTool.Elevations.Ground;
        _pieceBox.IsVisible = t.PiecesChosen;
        _attachRow.IsVisible = m == PlaceTool.Modes.Brush;
        GrowBox.IsVisible = t.GrowMatters;
        _brushRows.IsVisible = m == PlaceTool.Modes.Brush;
        _modeHead.Text = m switch
        {
            PlaceTool.Modes.Line => "ALONG THE LINE",
            PlaceTool.Modes.Grid => "GRID",
            PlaceTool.Modes.Zone => "ZONE FILL",
            _ => "BRUSH",
        };
        // One at a time places only what the click shows: nothing to scatter.
        _scatterRows.IsVisible = m == PlaceTool.Modes.Zone || m == PlaceTool.Modes.Brush && !t.OneAtATime;
        _patchRow.IsVisible = t.Clump > 0;
        _facingRows.IsVisible = !endToEnd;
        _lineShapeBox.IsVisible = line;
        _lineBox.IsVisible = line;
        _layersRow.IsVisible = endToEnd;
        _loopRow.IsVisible = line && t.LineShape == PlaceTool.LineShapes.Points;
        _freeLineRows.IsVisible = !endToEnd;
        AlongBox.IsVisible = t.LineShape == PlaceTool.LineShapes.Points;
        CurveBox.IsVisible = t.LineShape == PlaceTool.LineShapes.Points;
        _gridRow.IsVisible = m == PlaceTool.Modes.Grid;
        _shapeRow.IsVisible = m != PlaceTool.Modes.Brush;
        _undoRow.IsVisible = m is PlaceTool.Modes.Line or PlaceTool.Modes.Zone && t.Points.Count > 0;
        _brushHint.IsVisible = m == PlaceTool.Modes.Brush;
        _brushHint.Text = t.OneAtATime
            ? "A click places one object exactly at the cursor; dragging adds no more. Shift + drag removes the chosen kinds. The Mask applies."
            : "Density is objects per 100 m². Spacing keeps them apart (also from what is already there). Shift + drag removes the chosen kinds. The Mask applies.";
        _lineHintText.Text = t.LineShape switch
        {
            PlaceTool.LineShapes.Circle => "Press at the centre and drag out to the size; let go to see it, Enter places it. With End to end the size snaps so whole pieces close the ring.",
            PlaceTool.LineShapes.Rect => "Press at one corner and drag to the opposite one; Enter places it. With End to end the sides snap to whole pieces.",
            _ => "Click points along the route, or hold and drag to draw freely. Then drag a point to move it, drag the line to add a point, Ctrl + click a point to remove it; Backspace removes the last point.",
        };
        var cult = t.NeedCultivated;
        Note.Text = t.Chosen.Count == 0 ? $"Nothing to place yet: {ChooseKindsText} below, or a preset."
            : cult.Count > 0 ? $"{string.Join(", ", cult)} only grow{(cult.Count > 1 ? "" : "s")} on cultivated ground (paint it with Cultivate first)." : "";
        Note.IsVisible = Note.Text != "";
        ShowFit();
    }

    /// <summary>The fit hint: only when a chosen kind is wider than the spacing.</summary>
    private void ShowFit()
    {
        string? hint = T.FitHint();
        FitText.Text = hint ?? "";
        _fitRow.IsVisible = hint != null;
    }

    /// <summary>What the preview shows, and the keys that go with it.</summary>
    private void ShowInfo()
    {
        var t = T;
        ShowFit();
        _undoRow.IsVisible = t.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone && t.Points.Count > 0;
        int n = _input.PreviewNow.Count;
        if (t.Chosen.Count == 0)
        {
            Info.Text = "Tick at least one kind to place.";
        }
        else if (t.Mode == PlaceTool.Modes.Brush)
        {
            Info.Text = $"{(t.SnappedTo != null ? $"Snapped {t.SnappedTo}. " : "")}{n} object(s) shown under the cursor. Click places exactly these; drag paints more. R new layout · , . rotate.";
        }
        else
        {
            string where = t.Mode == PlaceTool.Modes.Line ? "along the line" : t.Mode == PlaceTool.Modes.Zone ? "in the zone" : "in the grid";
            string gap = t.Mode == PlaceTool.Modes.Line && t.EndToEnd && t.SnapGap > 0.05f && n > 0 ? $", end to end ({t.SnapGap:0.0} m of the line left at the end: move a point to close it)" : "";
            string turn = t.Mode is PlaceTool.Modes.Zone or PlaceTool.Modes.Grid ? $" · , . turn the {(t.Mode == PlaceTool.Modes.Zone ? "zone" : "grid")}{(t.ShapeTurn != 0 ? $" (now {t.ShapeTurn:0}°)" : "")}" : "";
            Info.Text = $"{n} object(s) {where}{gap}. Enter places them{turn} · R new random choices.";
        }
    }
}
