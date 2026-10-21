using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool's panel, like the web editor's: the mode, a preset, the kinds (with weights), and the settings of the
/// mode; the kinds to choose from open in a list beside it.
/// </summary>
public sealed partial class PlacePanel
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

    /// <summary>What the tool remembers between runs.</summary>
    public PlaceMemory Memory { get; }
    /// <summary>Asks for a preset's name (the window's dialog; replaced by tests). Null or empty: cancelled.</summary>
    internal Func<Task<string?>> AskName { get; set; } = () => Task.FromResult<string?>(null);
    /// <summary>Asks whether to delete a preset (the window's dialog; replaced by tests).</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>Shift held on the last press of a chip (a click takes Shift from its press).</summary>
    private bool _shiftPress;

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
        RestoreMemory(input, t);
        var modes = BuildModeRow();
        PresetBox = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch }.Tip("place.preset");
        TipPlaceControls();
        FillPresets();
        WirePresets(t);
        WireKinds(input, t);
        WireSizeSliders(t);
        WireFacingControls(t);
        WireLineControls(t);
        var shapes = BuildShapeButtons(t);
        WireActions(input, t);
        static TextBlock Hint(string s) => new() { Text = s, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

        _elevRow = Row(Lang.T("common.height"), ElevBox, new TextBlock { Text = Lang.T("common.metres"), Margin = new Thickness(3, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        _attachRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { BesideButton, OnTopButton } };
        _pieceBox = new StackPanel { Spacing = 4, Children = { SnapToBox, _attachRow } };
        _brushRows = new StackPanel { Spacing = 4, Children = { Row(Lang.T("place.label.brushSize"), SizeSlider, _sizeV), SingleBox } };
        _patchRow = Row(Lang.T("place.label.patchSize"), PatchSlider, _paV);
        _scatterRows = new StackPanel { Spacing = 4, Children = { Row(Lang.T("place.label.density"), DensitySlider, _densV), Row(Lang.T("place.label.spacing"), SpacingSlider, _spV), Row(Lang.T("place.label.clumping"), ClumpSlider, _clV), _patchRow } };
        _facingRows = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Row(Lang.T("place.label.sizePercent"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SizeMinBox, SizeMaxBox } }),
                Row(Lang.T("place.label.tilt"), TiltSlider, _tiV),
                Row(Lang.T("place.label.rotation"), RotationSlider, _roV),
                RandomYawBox,
            },
        };
        _layersRow = Row(Lang.T("place.label.layers"), LayersBox);
        _loopRow = LoopBox;
        _snapRows = new StackPanel { Spacing = 4, Children = { EndToEndBox, _layersRow } };
        _freeLineRows = new StackPanel { Spacing = 4, Children = { Row(Lang.T("place.label.every"), EverySlider, _evV), Row(Lang.T("place.label.wiggle"), WiggleSlider, _wiV), AlongBox } };
        _lineShapeBox = Section(Lang.T("place.head.lineShape"), shapes, _lineHintText);
        _lineBox = new StackPanel { Spacing = 4, Children = { _snapRows, _loopRow, _freeLineRows, CurveBox } };
        _gridRow = Row(Lang.T("place.label.spacing"), CellSlider, _ceV);
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

    /// <summary>Restores what was remembered: the kinds and their weights, the automatic snap, and the notes of kinds placed.</summary>
    private void RestoreMemory(PlaceInput input, PlaceTool t)
    {
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
    }
    /// <summary>The tips of the placing controls.</summary>
    private void TipPlaceControls()
    {
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
    }

    /// <summary>The kinds that can be placed in this world, with their kind of object (for the list's order).</summary>
    private List<(string Name, ObjectKind Kind)> _creatable = [];
    /// <summary>The world, the manifests and the mods' catalogue <c>_creatable</c> was worked out from.</summary>
    private (Save.WorldSave? World, int Manifests, int Catalogue) _creatableFrom;
}
