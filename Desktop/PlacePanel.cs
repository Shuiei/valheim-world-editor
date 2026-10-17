using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool's panel, like the web editor's: the mode, a preset, the kinds (with weights), and the settings of the
/// mode; the kinds to choose from open in a list beside it.
/// </summary>
public sealed class PlacePanel
{
    /// <summary>The kinds button's text while the list of kinds is closed.</summary>
    internal static string ChooseKindsText => Lang.T("place.label.chooseKinds");
    /// <summary>The kinds button's text while the list of kinds is open.</summary>
    internal static string DoneText => Lang.T("place.label.done");

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
    internal Button SavePresetButton { get; } = new() { Content = Lang.T("place.label.savePreset"), FontSize = 12 };
    /// <summary>Deletes the selected preset (your own only).</summary>
    internal Button DeletePresetButton { get; } = new() { Content = Lang.T("common.delete"), FontSize = 12, IsEnabled = false };
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
    internal TextBox Search { get; } = new() { PlaceholderText = Lang.T("place.hint.search"), FontSize = 12 };
    /// <summary>The kinds this world can place, by category.</summary>
    internal StackPanel List { get; } = new() { Spacing = 1 };
    /// <summary>The next click on an object takes its kind (Shift + click adds it).</summary>
    internal Button PickButton { get; } = new() { Content = Lang.T("place.label.pick"), FontSize = 12 };
    /// <summary>Unchooses every kind.</summary>
    internal Button UntickAllButton { get; } = new() { Content = Lang.T("place.label.untickAll"), FontSize = 12 };
    /// <summary>Ticks every favourite kind.</summary>
    internal Button FavAllButton { get; } = Link(Lang.T("place.label.all"));
    /// <summary>Unticks every favourite kind.</summary>
    internal Button FavNoneButton { get; } = Link(Lang.T("place.label.none"));
    /// <summary>Ticks every recent kind.</summary>
    internal Button RecentAllButton { get; } = Link(Lang.T("place.label.all"));
    /// <summary>Unticks every recent kind.</summary>
    internal Button RecentNoneButton { get; } = Link(Lang.T("place.label.none"));
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
    internal ComboBox ElevationBox { get; } = new() { ItemsSource = new[] { Lang.T("place.elevation.ground"), Lang.T("place.elevation.above"), Lang.T("place.elevation.at") }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>The height above the ground, or the one height for all, in metres.</summary>
    internal NumericUpDown ElevBox { get; } = new() { Value = 0, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>Snap pieces to the pieces already there (shown when pieces are chosen).</summary>
    internal CheckBox SnapToBox { get; } = new() { Content = Lang.T("place.label.snapTo"), IsChecked = true, FontSize = 12 };
    /// <summary>Brush: snap a single piece beside the nearest one.</summary>
    internal Button BesideButton { get; } = new() { Content = Lang.T("place.label.beside"), FontSize = 12 };
    /// <summary>Brush: snap a single piece on top of the one under the cursor.</summary>
    internal Button OnTopButton { get; } = new() { Content = Lang.T("place.label.onTop"), FontSize = 12 };
    /// <summary>Leave saplings and crops room to grow (shown when one is chosen).</summary>
    internal CheckBox GrowBox { get; } = new() { Content = Lang.T("place.label.grow"), IsChecked = true, FontSize = 12 };
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
    internal CheckBox RandomYawBox { get; } = new() { Content = Lang.T("place.label.randomYaw"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Brush: one at a time, exactly at the cursor (setting it by hand stops it following the chosen kinds).
    /// </summary>
    internal CheckBox SingleBox { get; } = new() { Content = Lang.T("place.label.single"), FontSize = 12 };
    /// <summary>Line: pieces end to end (setting it by hand stops it following the chosen kinds).</summary>
    internal CheckBox EndToEndBox { get; } = new() { Content = Lang.T("place.label.endToEnd"), FontSize = 12 };
    /// <summary>End to end: how many pieces high, 1 to 20.</summary>
    internal NumericUpDown LayersBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 20, Increment = 1, FormatString = "0", FontSize = 12 };
    /// <summary>Line (Points): back to the first point.</summary>
    internal CheckBox LoopBox { get; } = new() { Content = Lang.T("place.label.loop"), FontSize = 12 };
    /// <summary>Line: the distance between objects, 0.5 m to <c>PlaceTool.MaxEvery</c>.</summary>
    internal Slider EverySlider { get; private set; } = null!;
    /// <summary>Line: the random sideways stray, 0 to 5 m.</summary>
    internal Slider WiggleSlider { get; private set; } = null!;
    /// <summary>Line (Points): objects face along the line.</summary>
    internal CheckBox AlongBox { get; } = new() { Content = Lang.T("place.label.along"), IsChecked = true, FontSize = 12 };
    /// <summary>Line (Points): a smooth curve through the points.</summary>
    internal CheckBox CurveBox { get; } = new() { Content = Lang.T("path.label.curve"), IsChecked = true, FontSize = 12 };
    /// <summary>Grid: the spacing, 1 m to <c>PlaceTool.MaxCell</c>.</summary>
    internal Slider CellSlider { get; private set; } = null!;
    /// <summary>Places what the line, grid or zone shows (Enter).</summary>
    internal Button PlaceButton { get; } = new Button { Content = Lang.T("place.label.place"), FontSize = 12 }.Classed("primary");
    /// <summary>Clears the line, grid box or zone.</summary>
    internal Button ClearButton { get; } = new() { Content = Lang.T("common.clearEsc"), FontSize = 12 };
    /// <summary>Removes the line's or zone's last point.</summary>
    internal Button UndoPointButton { get; } = new() { Content = Lang.T("place.label.undoPoint"), FontSize = 12 };
    /// <summary>New random choices (R).</summary>
    internal Button NewLayoutButton { get; } = new() { Content = Lang.T("place.label.newLayout"), FontSize = 12 };
    /// <summary>What the preview shows and the keys that go with it.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    /// <summary>Kinds wider than the spacing: says so (the fit button then sets the spacing to them).</summary>
    internal TextBlock FitText { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>Sets the spacing to the widest chosen kind.</summary>
    internal Button FitButton { get; } = new() { Content = Lang.T("place.label.fit"), FontSize = 11, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
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
    private static Slider Slide(double min, double max, double step, double value, TextBlock shown, Func<double, string> fmt, Action<float> set) =>
        Ui.Slide(min, max, step, value, shown, fmt, v => set((float)v));

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
    private static Grid Row(string label, Control input, Control? after = null) => Ui.Row(label, input, after, "72,*,Auto");

    /// <summary>A floating card of the given width that scrolls when the window is too short.</summary>
    private static Border CardOf(Control child, double width) => Ui.AsCard(new Border
    {
        VerticalAlignment = VerticalAlignment.Top,
        // As tall as the window allows: the scroll bar only when it does not fit.
        Child = new ScrollViewer { Content = new StackPanel { Width = width, Spacing = 6, Children = { child } } },
    });

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
            t.Chosen.AddRange(Memory.Chosen.Where(n => !NotOffered(n)));
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
            var b = new Button { Content = ModeName(m), FontSize = 12 };
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
                Say(Said.Of("place.msg.tickFirst"));
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
            Say(Said.Of("place.msg.presetSaved", name));
        };
        DeletePresetButton.Click += async (_, _) =>
        {
            if (PresetAt(PresetBox.SelectedIndex) is not { Own: true } p || !await Confirm(Lang.T("place.ask.deletePreset.text", p.Preset.Name)))
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
                    Say(Said.Of("select.msg.cannotPlace", name));
                    return;
                }
                // A click places only that kind; Shift + click adds it to the ticked kinds.
                Choose(name, add: shift);
                Say(Said.Of("place.msg.placing", string.Join(", ", T.Chosen)));
            };
            Say(Said.Of("place.msg.pickHow"));
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
        foreach (var (ls, label, tip) in new[] { (PlaceTool.LineShapes.Points, Lang.T("place.shape.points"), Lang.T("place.tip.points")),
            (PlaceTool.LineShapes.Circle, Lang.T("brush.shape.circle"), Lang.T("place.tip.circle")), (PlaceTool.LineShapes.Rect, Lang.T("place.shape.rect"), Lang.T("place.tip.rect")) })
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

        _elevRow = Row(Lang.T("common.height"), ElevBox, new TextBlock { Text = Lang.T("common.metres"), Margin = new Thickness(3, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        _attachRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { BesideButton, OnTopButton } };
        _pieceBox = new StackPanel { Spacing = 4, Children = { SnapToBox, _attachRow } };
        _brushRows = new StackPanel { Spacing = 4, Children = { Row(Lang.T("place.label.brushSize"), SizeSlider, sizeV), SingleBox } };
        _patchRow = Row(Lang.T("place.label.patchSize"), PatchSlider, paV);
        _scatterRows = new StackPanel { Spacing = 4, Children = { Row(Lang.T("place.label.density"), DensitySlider, densV), Row(Lang.T("place.label.spacing"), SpacingSlider, spV), Row(Lang.T("place.label.clumping"), ClumpSlider, clV), _patchRow } };
        _facingRows = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Row(Lang.T("place.label.sizePercent"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SizeMinBox, SizeMaxBox } }),
                Row(Lang.T("place.label.tilt"), TiltSlider, tiV),
                Row(Lang.T("place.label.rotation"), RotationSlider, roV),
                RandomYawBox,
            },
        };
        _layersRow = Row(Lang.T("place.label.layers"), LayersBox);
        _loopRow = LoopBox;
        _snapRows = new StackPanel { Spacing = 4, Children = { EndToEndBox, _layersRow } };
        _freeLineRows = new StackPanel { Spacing = 4, Children = { Row(Lang.T("place.label.every"), EverySlider, evV), Row(Lang.T("place.label.wiggle"), WiggleSlider, wiV), AlongBox } };
        _lineShapeBox = Section(Lang.T("place.head.lineShape"), shapes, _lineHintText);
        _lineBox = new StackPanel { Spacing = 4, Children = { _snapRows, _loopRow, _freeLineRows, CurveBox } };
        _gridRow = Row(Lang.T("place.label.spacing"), CellSlider, ceV);
        _shapeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PlaceButton, ClearButton } };
        _fitRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { FitText, Ui.Col(FitButton, 1) } };
        _undoRow = UndoPointButton;
        _brushHint = Hint(Lang.T("place.hint.brush"));

        Card = CardOf(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = Lang.T("rail.label.place"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                modes,
                _lineShapeBox,
                Section(Lang.T("place.head.preset"), PresetBox, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SavePresetButton, DeletePresetButton } }),
                Section(
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Head(Lang.T("place.head.selected")), _mixCount } },
                    new Border { Background = Ui.Panel2, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 4), Child = Mix },
                    Note,
                    KindsButton),
                Section(Lang.T("common.objects"), Row(Lang.T("place.label.elevation"), ElevationBox), _elevRow, _pieceBox, GrowBox, _facingRows),
                Section(_modeHead, _brushRows, _scatterRows, _lineBox, _gridRow, _fitRow),
                new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0), Children = { _shapeRow, _undoRow, NewLayoutButton, Info, _brushHint } },
            },
        }, 320);
        Chooser = CardOf(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = Lang.T("place.head.chooseKinds"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                Search,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PickButton, UntickAllButton } },
                (_favBox = new StackPanel { Spacing = 3, Children = { ChipHead(Lang.T("place.head.favourites"), FavAllButton, FavNoneButton), Favourites } }),
                (_recentBox = new StackPanel { Spacing = 3, Children = { ChipHead(Lang.T("place.head.recent"), RecentAllButton, RecentNoneButton), Recent } }),
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
        PresetBox.ItemsSource = new[] { Lang.T("place.preset.none") }.Concat(PlaceTool.BuiltIn.Select(PlaceTool.ShownName)).Concat(Memory.Presets.Select(p => Lang.T("place.preset.yours", p.Name))).ToList();
        int own = Memory.Presets.FindIndex(p => p.Name == select);
        PresetBox.SelectedIndex = own >= 0 ? 1 + PlaceTool.BuiltIn.Length + own : 0;
        DeletePresetButton.IsEnabled = own >= 0;
        _filling = false;
    }

    /// <summary>Remembers the chosen kinds and weights for the next time (saves the memory a moment later).</summary>
    private void Remember()
    {
        Memory.Chosen = [.. T.Chosen];
        Memory.Weights = new(T.Weights);
        Memory.SaveLater();
    }

    /// <summary>The heading of a chip row (in capitals), with its all and none buttons.</summary>
    private static StackPanel ChipHead(string title, Button all, Button none) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
        Margin = new Thickness(0, 6, 0, 2),
        Children = { new TextBlock { Text = title.ToUpperInvariant(), FontSize = 10, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, all, none },
    };

    /// <summary>
    /// The Recent row's kinds: the chosen kinds that are not among them yet, then the ones placed lately (newest
    /// first), so what is chosen always shows.
    /// </summary>
    private List<string> RecentShown() => [.. T.Chosen.Where(n => !Memory.Recent.Contains(n)).Concat(Memory.Recent)];

    /// <summary>all / none: tick or untick every kind of the row (those this world can place).</summary>
    private void TickRow(IEnumerable<string> names, bool on)
    {
        // With no world open, every kind (nothing to check them against).
        var known = Creatable().Select(c => c.Name).ToHashSet();
        bool open = _scene()?.World != null;
        foreach (string? n in names.Where(n => !open || known.Contains(n)).ToList())
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
        // Not a kind no longer offered (a chip shown before the mods' manifests or catalogue changed).
        if (_scene()?.World != null && NotOffered(name))
        {
            return;
        }
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
        // With no world open, every kind (nothing to check them against).
        var known = Creatable().Select(c => c.Name).ToHashSet();
        bool open = _scene()?.World != null;
        foreach (var (panel, list, box) in new[] { (Favourites, Memory.Favourites, _favBox), (Recent, RecentShown(), _recentBox) })
        {
            panel.Children.Clear();
            foreach (string? n in list.Where(n => !open || known.Contains(n)))
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

    /// <summary>A line for the status bar, in the user's language and in English (for Claude).</summary>
    public event Action<Said>? Message;
    /// <summary>Raises <see cref="Message"/>.</summary>
    private void Say(Said s) => Message?.Invoke(s);

    /// <summary>The brush line's note on what the piece under the cursor snaps to, or "" when it snaps to nothing.</summary>
    /// <param name="snapped">How and to what the piece snaps, if it does.</param>
    internal static string SnapNote((PlaceTool.SnapWays Way, string Name)? snapped)
    {
        return snapped switch
        {
            null => "",
            { Way: PlaceTool.SnapWays.OnTop } s => Lang.T("place.info.snappedOnTop", s.Name),
            { Way: PlaceTool.SnapWays.Beside } s => Lang.T("place.info.snappedBeside", s.Name),
            { } s => Lang.T("place.info.snappedTo", s.Name),
        };
    }

    /// <summary>A mode's name, on its button.</summary>
    /// <param name="m">The mode.</param>
    private static string ModeName(PlaceTool.Modes m)
    {
        return m switch
        {
            PlaceTool.Modes.Line => Lang.T("place.mode.line"),
            PlaceTool.Modes.Grid => Lang.T("place.mode.grid"),
            PlaceTool.Modes.Zone => Lang.T("place.mode.zone"),
            _ => Lang.T("place.mode.brush"),
        };
    }

    /// <summary>The kinds that can be placed in this world, with their kind of object (for the list's order).</summary>
    private List<(string Name, ObjectKind Kind)> _creatable = [];
    /// <summary>The world, the manifests and the mods' catalogue <c>_creatable</c> was worked out from.</summary>
    private (Save.WorldSave? World, int Manifests, int Catalogue) _creatableFrom;
    /// <summary>
    /// The kinds that can be placed in this world, with their kind of object; worked out again for another world or
    /// when the mods' manifests or catalogue change (empty while no world is open).
    /// </summary>
    internal List<(string Name, ObjectKind Kind)> Creatable()
    {
        if (_scene()?.World is { } w && _creatableFrom != (w, ModManifests.Generation, ModCatalogue.Generation))
        {
            _creatableFrom = (w, ModManifests.Generation, ModCatalogue.Generation);
            // Not the prefabs a mod's manifest asks not to offer (its tools, effects, helpers), nor its prefabs the game
            // draws nothing of (sounds, effects), nor chosen any more.
            _creatable = [.. w.Creatable.Select(p => _nameOf(p)).Where(n => n != null && !NotOffered(n)).Distinct().Select(n => (n!, ObjectKinds.Of(n, false)))];
            if (T.Chosen.RemoveAll(NotOffered) > 0)
            {
                // What is chosen changed: shown again, and the tool told.
                Dispatcher.UIThread.Post(Fill);
                T.Notify();
            }
            // The chips, and the list while it shows, show only what is offered.
            Dispatcher.UIThread.Post(RenderChips);
            Dispatcher.UIThread.Post(FillList);
        }
        return _creatable;
    }

    /// <summary>
    /// Whether a prefab is not offered: a mod's manifest hides it, or it is a mod's helper the game draws nothing of (its
    /// reported model has no parts: a sound, an effect, a marker). Items and pickables are offered whatever their model.
    /// Objects of the kind already in a world still show.
    /// </summary>
    internal static bool NotOffered(string name) =>
        ModManifests.IsHidden(name) || ModCatalogue.ModelOf(name) is { Parts.Count: 0 }
            && PrefabCatalog.ItemKindOf(Save.StableHash.Of(name)) == null && ObjectKinds.Of(name, false) != ObjectKind.Pickables;

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
                    Ui.Col(new TextBlock { Text = names.Count(T.Chosen.Contains) is int on && on > 0 ? $"{on} / {names.Count}" : $"{names.Count}", FontSize = 10.5, Foreground = names.Any(T.Chosen.Contains) ? Ui.Accent : Ui.Muted, Margin = new Thickness(8, 0, 0, 0) }, 1),
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
                            // A kind no longer offered since the list was filled (the mods' catalogue changed): not chosen.
                            if (NotOffered(n))
                            {
                                box.IsChecked = false;
                                return;
                            }
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
                    ToolTip.SetTip(star, Lang.T(fav ? "place.favRemove" : "place.favAdd"));
                    star.Click += (_, _) => { Memory.ToggleFavourite(n); FillList(); RenderChips(); };
                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { box, Ui.Col(star, 1) } };
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
            List.Children.Add(new TextBlock { Text = Lang.T("blueprints.noMatch"), FontSize = 11, Foreground = Ui.Muted });
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
            Mix.Children.Add(new TextBlock { Text = Lang.T("place.hint.noneYet"), FontSize = 12, Foreground = Ui.Muted, Margin = new Thickness(0, 4) });
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
                ToolTip.SetTip(w, Lang.T("place.shareOf", n, Tips.Of("place.weight")));
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
                g.Children.Add(Ui.Col(w, 1));
            }
            _shares[n] = (pct, w);
            g.Children.Add(Ui.Col(pct, 2));
            var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(x, $"{Tips.Of("place.unchoose")} ({n})");
            x.Click += (_, _) => { T.Chosen.Remove(n); Chosen(); };
            g.Children.Add(Ui.Col(x, 3));
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
        _modeHead.Text = (m switch
        {
            PlaceTool.Modes.Line => Lang.T("place.head.line"),
            PlaceTool.Modes.Grid => Lang.T("place.mode.grid"),
            PlaceTool.Modes.Zone => Lang.T("place.head.zone"),
            _ => Lang.T("place.mode.brush"),
        }).ToUpperInvariant();
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
        _brushHint.Text = t.OneAtATime ? Lang.T("place.hint.single") : Lang.T("place.hint.brush");
        _lineHintText.Text = t.LineShape switch
        {
            PlaceTool.LineShapes.Circle => Lang.T("place.hint.circle"),
            PlaceTool.LineShapes.Rect => Lang.T("place.hint.rect"),
            _ => Lang.T("place.hint.points"),
        };
        var cult = t.NeedCultivated;
        Note.Text = t.Chosen.Count == 0 ? Lang.T("place.hint.nothingYet", ChooseKindsText)
            : cult.Count > 1 ? Lang.T("place.hint.cultivated.many", string.Join(", ", cult))
            : cult.Count == 1 ? Lang.T("place.hint.cultivated.one", cult[0]) : "";
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
            Info.Text = Lang.T("place.info.none");
        }
        else if (t.Mode == PlaceTool.Modes.Brush)
        {
            Info.Text = string.Join(" ", new[] { SnapNote(t.Snapped), Lang.T("place.info.brush", n) }.Where(p => p.Length > 0));
        }
        else
        {
            bool turned = t.ShapeTurn != 0;
            Info.Text = t.Mode switch
            {
                PlaceTool.Modes.Line when t.EndToEnd && t.SnapGap > 0.05f && n > 0 => Lang.T("place.info.lineGap", n, t.SnapGap),
                PlaceTool.Modes.Line => Lang.T("place.info.line", n),
                PlaceTool.Modes.Zone when turned => Lang.T("place.info.zoneTurned", n, t.ShapeTurn),
                PlaceTool.Modes.Zone => Lang.T("place.info.zone", n),
                _ when turned => Lang.T("place.info.gridTurned", n, t.ShapeTurn),
                _ => Lang.T("place.info.grid", n),
            };
        }
    }
}
