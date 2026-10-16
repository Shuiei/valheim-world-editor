using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Area tool's panel, like the web editor's (editor/area.js): the selection's shape and soft edge,
/// one action at a time with only its own settings, and Apply (Enter). Ground actions change the ground
/// inside; object actions remove, select or replace the objects of the chosen kinds inside; Reset zones
/// hands the zones under the selection back to the world generator when saving.
/// </summary>
public sealed class AreaPanel
{
    /// <summary>The actions on the Action list, in its order; each shows only its own rows.</summary>
    public enum Act
    {
        /// <summary>Flattens the ground inside to one height.</summary>
        Flatten,
        /// <summary>Raises the ground inside.</summary>
        Raise,
        /// <summary>Lowers the ground inside.</summary>
        Lower,
        /// <summary>Evens out bumps inside.</summary>
        Smooth,
        /// <summary>Natural-looking bumps (Naturalize), with the Naturalize brush's Bumps and Size.</summary>
        Natural,
        /// <summary>Back to the ground the game generated, paint removed.</summary>
        Restore,
        /// <summary>Weathers the ground inside: slopes settle and rain cuts gullies.</summary>
        Erode,
        /// <summary>Paints the ground inside.</summary>
        Paint,
        /// <summary>Removes the objects of the chosen kinds inside.</summary>
        Remove,
        /// <summary>Selects the objects of the chosen kinds inside.</summary>
        Select,
        /// <summary>
        /// Replaces every object inside of the prefab chosen in Replace with the one chosen in With, whatever
        /// the kind toggles say.
        /// </summary>
        Replace,
        /// <summary>Puts back what the game grows here, by its vegetation rules, for the chosen kinds.</summary>
        Regrow,
        /// <summary>Copies the selection, to paste it elsewhere.</summary>
        Copy,
        /// <summary>Exports the ground inside as a heightmap picture, or imports one.</summary>
        Heightmap,
        /// <summary>Restores the selection from a backup.</summary>
        Backup,
        /// <summary>Hands the zones under the selection back to the world generator when saving.</summary>
        Reset,
    }

    /// <summary>The Action list: each action with its label.</summary>
    private static readonly (Act Act, string Label)[] Actions =
    [
        (Act.Flatten, "Flatten"), (Act.Raise, "Raise"), (Act.Lower, "Lower"), (Act.Smooth, "Smooth"), (Act.Natural, "Naturalize"),
        (Act.Restore, "Restore the ground"), (Act.Erode, "Erode"), (Act.Paint, "Paint"),
        (Act.Remove, "Remove objects"), (Act.Select, "Select objects"), (Act.Replace, "Replace objects"), (Act.Regrow, "Regrow nature"), (Act.Copy, "Copy and paste"), (Act.Heightmap, "Heightmap"),
        (Act.Backup, "Restore from a backup"), (Act.Reset, "Reset zones"),
    ];

    /// <summary>The Paint action's paints, with the brush tool whose paint they put down.</summary>
    private static readonly (string Label, BrushTool Tool)[] Paints =
    [
        ("Dirt", BrushTool.PaintDirt), ("Cultivated", BrushTool.PaintCultivated), ("Paved", BrushTool.PaintPaved), ("Clear paint", BrushTool.PaintClear),
    ];

    /// <summary>The panel itself, shown over the view while the Area tool is on.</summary>
    public Control Card { get; }
    /// <summary>The view's selection (box or polygon) the actions work on.</summary>
    public AreaTool Area { get; }
    private readonly GlView _view;
    private readonly Func<EditSession?> _session;

    /// <summary>
    /// Whether an Apply is running: it can wait on a question, the generator or a backup being read, and a second one
    /// meanwhile would do the same again.
    /// </summary>
    private bool _applying;
    private readonly Func<int, string?> _nameOf;
    /// <summary>Asks a yes/no question (the window's dialog); resetting zones goes on only on true.</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>A line for the status bar.</summary>
    public event Action<string>? Message;
    /// <summary>Select objects: the window switches to the Select tool.</summary>
    public event Action? SwitchToSelect;

    /// <summary>Switches the selection to a box dragged on the ground (clears the current one).</summary>
    internal Button BoxButton { get; } = new() { Content = "Box", FontSize = 12 };
    /// <summary>Switches the selection to a polygon clicked point by point (clears the current one).</summary>
    internal Button PolyButton { get; } = new() { Content = "Polygon", FontSize = 12 };
    /// <summary>Clears the selection.</summary>
    internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
    /// <summary>The selection's size and what is inside, or how to make one.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The soft edge, 0 to 20 m: how far inwards from the outline an action fades in.</summary>
    internal Slider SoftSlider { get; }
    /// <summary>The action list.</summary>
    internal ComboBox ActionBox { get; }
    /// <summary>Flatten: the height to flatten to, in metres.</summary>
    internal NumericUpDown HeightBox { get; } = new() { Value = 35, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>Puts the average ground height inside the selection into <see cref="HeightBox"/>.</summary>
    internal Button AverageButton { get; } = new() { Content = "avg", FontSize = 11, Padding = new Thickness(6, 2) };
    /// <summary>Raise and Lower: by how much, in metres.</summary>
    internal NumericUpDown AmountBox { get; } = new() { Value = 2, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>Paint: which paint (one of <c>Paints</c>).</summary>
    internal ComboBox PaintBox { get; } = new() { ItemsSource = Paints.Select(p => p.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>
    /// Flatten, Raise and Lower: No limit, the ground goes past the game's ±8 m (the brush options' switch, the
    /// same one shown here; the window keeps the two together).
    /// </summary>
    internal CheckBox NoLimitBox { get; } = new() { Content = "No limit: past the game's ±8 m", FontSize = 12 };
    /// <summary>No limit was switched here (on: true).</summary>
    public event Action<bool>? NoLimitChanged;
    /// <summary>Flatten, Raise and Lower: the cut and fill the action would move.</summary>
    internal TextBlock VolumeText { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>One toggle per object kind: the kinds the object actions (and a backup restore) work on.</summary>
    internal Dictionary<ObjectKind, ToggleButton> KindButtons { get; } = [];
    /// <summary>Replace: the kind to replace, from those inside the selection (shown as "name (count)").</summary>
    internal ComboBox FromBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>Replace: the kind to put in their place, from the kinds the world can make.</summary>
    internal ComboBox ToBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };
    /// <summary>Eyedropper: the "with" kind picked from the world (the window takes the next click on an object).</summary>
    internal Button ToPickButton { get; } = new() { Content = "pick", FontSize = 11, Padding = new Thickness(6, 2), Margin = new Thickness(4, 0, 0, 0) };
    /// <summary>
    /// Asks the window to take the next click on an object: the action's name for the prompt, and what to do with
    /// the picked prefab hash.
    /// </summary>
    public event Action<string, Action<int>>? PickKindAsked;
    /// <summary>Reset zones: keep the player's buildings in the zones.</summary>
    internal CheckBox KeepBuildingsBox { get; } = new() { Content = "Keep my buildings", IsChecked = true, FontSize = 12 };
    /// <summary>Reset zones: also undo the zones' ground edits.</summary>
    internal CheckBox ResetGroundBox { get; } = new() { Content = "Reset ground edits too", IsChecked = true, FontSize = 12 };
    /// <summary>Reset zones: unmarks the zones under the selection.</summary>
    internal Button UnresetButton { get; } = new() { Content = "Cancel reset", FontSize = 12 };
    /// <summary>Runs the current action (Enter); its text names the action. Hidden for Copy and Heightmap.</summary>
    internal Button ApplyButton { get; } = new Button { FontSize = 12 }.Classed("primary");
    /// <summary>The rows shown only for some actions, with those actions.</summary>
    private readonly Dictionary<Control, Act[]> _rows = [];
    /// <summary>The prefab hashes behind <see cref="ToBox"/>'s entries, in its order.</summary>
    private List<int> _creatable = [];
    /// <summary>
    /// The world and its templates, the game's data, the manifests and the mods' catalogue <c>_creatable</c> was
    /// worked out from.
    /// </summary>
    private (WorldSave? World, int Templates, int Data, int Manifests, int Catalogue) _creatableFrom;

    /// <summary>The action chosen on the list.</summary>
    public Act Current => Actions[Math.Max(0, ActionBox.SelectedIndex)].Act;

    /// <summary>Chooses an action on the list.</summary>
    internal void Choose(Act a) => ActionBox.SelectedIndex = Array.FindIndex(Actions, x => x.Act == a);

    /// <summary>Restore from a backup: the backup, from <c>_backupPaths</c>, then "Another folder…".</summary>
    internal ComboBox BackupBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
    /// <summary>Restore from a backup: put the ground back.</summary>
    internal CheckBox BackupGroundBox { get; } = new() { Content = "Ground (height and paint)", IsChecked = true, FontSize = 12 };
    /// <summary>Restore from a backup: put the objects of the chosen kinds back.</summary>
    internal CheckBox BackupObjectsBox { get; } = new() { Content = "Objects of the kinds chosen above", IsChecked = true, FontSize = 12 };
    /// <summary>Restore from a backup: what it does, then the backup read or why it cannot be used.</summary>
    internal TextBlock BackupInfo { get; } = new()
    {
        FontSize = 11,
        Foreground = Ui.Muted,
        TextWrapping = TextWrapping.Wrap,
        Text = "Puts the selection back as it was in a backup: the editor's backups and the game's own are listed. Choose Your buildings above to bring buildings back too."
    };
    /// <summary>The backup folders behind <see cref="BackupBox"/>'s entries, in its order.</summary>
    private List<string> _backupPaths = [];
    /// <summary>Asks for another backup folder (the window's folder picker).</summary>
    internal Func<Task<string?>> PickFolder { get; set; } = () => Task.FromResult<string?>(null);
    /// <summary>The last backup read by <see cref="OpenBackup"/>, kept for the next restore from it.</summary>
    private (string Path, WorldSave World, EditStore Edits)? _backup;

    // Copy and paste: the window copies, pastes and keeps blueprints.
    /// <summary>Asks the window to copy the selection.</summary>
    internal Button CopyButton { get; } = new() { Content = "Copy (Ctrl+C)", FontSize = 12 };
    /// <summary>Asks the window to cut: copy, then remove the originals.</summary>
    internal Button CutButton { get; } = new() { Content = "Cut (Ctrl+X)", FontSize = 12 };
    /// <summary>Asks the window to paste.</summary>
    internal Button PasteButton { get; } = new() { Content = "Paste (Ctrl+V)", FontSize = 12 };
    /// <summary>Asks the window to save the selection as a blueprint.</summary>
    internal Button SaveBlueprintButton { get; } = new() { Content = "Save blueprint…", FontSize = 12 };
    /// <summary>Asks the window to open the blueprint library.</summary>
    internal Button LibraryButton { get; } = new() { Content = "Blueprints…", FontSize = 12 };
    /// <summary>What copying takes.</summary>
    internal TextBlock ClipInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The Copy, Cut and paste buttons, handled by the window.</summary>
    public event Action? CopyAsked, CutAsked, PasteAsked, SaveBlueprintAsked, LibraryAsked;

    // Heightmap: export the area, or import a picture into the selection (or the whole area).
    /// <summary>Writes the area's heightmap (see <see cref="ExportHeightmap"/>).</summary>
    internal Button ExportButton { get; } = new() { Content = "Export the area", FontSize = 12 };
    /// <summary>Reads a picture to import (see <see cref="LoadPicture"/>).</summary>
    internal Button ImportButton { get; } = new() { Content = "Import…", FontSize = 12 };
    /// <summary>Import: the height black stands for, in metres.</summary>
    internal NumericUpDown LowestBox { get; } = new() { Increment = 0.5m, FormatString = "0.###", FontSize = 12 };
    /// <summary>Import: the height white stands for, in metres.</summary>
    internal NumericUpDown HighestBox { get; } = new() { Increment = 0.5m, FormatString = "0.###", FontSize = 12 };
    /// <summary>Import: puts the picture read into the ground (see <see cref="PutPicture"/>).</summary>
    internal Button PutButton { get; } = new Button { Content = "Put it into the ground", FontSize = 12 }.Classed("primary");
    /// <summary>Import: drops the picture read.</summary>
    internal Button CancelPictureButton { get; } = new() { Content = "Cancel", FontSize = 12 };
    /// <summary>Import: the picture read and where it is fitted.</summary>
    internal TextBlock PictureInfo { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The import rows, shown once a picture is read.</summary>
    private readonly Control _pictureBox = null!;
    /// <summary>Asks for a picture to import (the window's file picker).</summary>
    internal Func<Task<string?>> PickPicture { get; set; } = () => Task.FromResult<string?>(null);
    /// <summary>
    /// The picture read: its values (0 to 1, row by row), the ground it is fitted to and the box of it (grid points),
    /// the ground points it goes into with their weights, and the selection it was fitted to (null: the whole
    /// area). Dropped when the selection becomes another, and refused for another ground: its points are that
    /// ground's.
    /// </summary>
    private (float[] Values, Ground Ground, int X0, int Z0, int W, int H, List<(int G, float W)> Cells, List<Vector2>? Fit)? _picture;

    /// <summary>Builds the panel.</summary>
    /// <param name="view">The view: its selection, scene and shown kinds.</param>
    /// <param name="session">The open edit session, null when no world is open.</param>
    /// <param name="nameOf">A prefab hash's name, null when unknown.</param>
    public AreaPanel(GlView view, Func<EditSession?> session, Func<int, string?> nameOf)
    {
        _view = view;
        _session = session;
        _nameOf = nameOf;
        Area = view.Area;
        ActionBox = new ComboBox { ItemsSource = Actions.Select(a => a.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 500 };
        var softV = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        SoftSlider = new Slider { Minimum = 0, Maximum = 20, SmallChange = 0.5, TickFrequency = 0.5, IsSnapToTickEnabled = true, Value = Area.Soft };
        softV.Text = $"{Area.Soft:0.#} m";
        SoftSlider.ValueChanged += (_, e) => { Area.Soft = (float)e.NewValue; softV.Text = $"{e.NewValue:0.#} m"; Refresh(); };
        BoxButton.Click += (_, _) => { Area.Box = true; Area.Clear(); ShowShape(); };
        PolyButton.Click += (_, _) => { Area.Box = false; Area.Clear(); ShowShape(); };
        ClearButton.Click += (_, _) => { Area.Clear(); Message?.Invoke("Selection cleared."); };
        ActionBox.SelectionChanged += (_, _) => { ShowRows(); RefreshVolume(); if (Current == Act.Backup) { FillBackups(); } };
        BackupBox.SelectionChanged += async (_, _) =>
        {
            // The last entry: another folder.
            if (BackupBox.ItemCount > 0 && BackupBox.SelectedIndex == _backupPaths.Count)
            {
                if (await PickFolder() is string path)
                {
                    _backupPaths.Add(path);
                    FillBackups(path);
                }
                else
                {
                    BackupBox.SelectedIndex = -1;
                }
            }
        };
        HeightBox.ValueChanged += (_, _) => Refresh();
        AmountBox.ValueChanged += (_, _) => Refresh();
        AverageButton.Click += (_, _) =>
        {
            if (_session()?.Ground is { } g && Area.Average(g) is float avg)
            {
                HeightBox.Value = (decimal)MathF.Round(avg, 1);
            }
        };
        AverageButton.Tip("area.avg");
        ActionBox.Tip("area.action");
        BoxButton.Tip("area.box");
        PolyButton.Tip("area.poly");
        ClearButton.Tip("area.clear");
        SoftSlider.Tip("area.soft");
        HeightBox.Tip("area.height");
        AmountBox.Tip("area.amount");
        PaintBox.Tip("area.paint");
        FromBox.Tip("area.from");
        ToBox.Tip("area.to");
        KeepBuildingsBox.Tip("area.keepBuildings");
        ResetGroundBox.Tip("area.resetGround");
        UnresetButton.Tip("area.unreset");
        BackupBox.Tip("area.backup");
        BackupGroundBox.Tip("area.backupGround");
        BackupObjectsBox.Tip("area.backupObjects");
        CopyButton.Tip("area.copy");
        CutButton.Tip("area.cut");
        PasteButton.Tip("area.paste");
        SaveBlueprintButton.Tip("area.saveBlueprint");
        LibraryButton.Tip("area.library");
        LowestBox.Tip("area.lowest");
        HighestBox.Tip("area.highest");
        PutButton.Tip("area.put");
        CancelPictureButton.Tip("area.cancelPicture");
        var kinds = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        foreach (var k in ObjectKinds.All)
        {
            var b = new ToggleButton { Content = ObjectKinds.Label(k), IsChecked = Area.Kinds.Contains(k), FontSize = 11, Padding = new Thickness(6, 2) };
            ToolTip.SetTip(b, $"{Tips.Kind(k)} {Tips.Of("area.kind")}");
            b.IsCheckedChanged += (_, _) =>
            {
                if (b.IsChecked == true)
                {
                    Area.Kinds.Add(k);
                }
                else
                {
                    Area.Kinds.Remove(k);
                }
            };
            KindButtons[k] = b;
            kinds.Children.Add(b);
        }
        ApplyButton.Click += async (_, _) => await Apply();
        CopyButton.Click += (_, _) => CopyAsked?.Invoke();
        CutButton.Click += (_, _) => CutAsked?.Invoke();
        PasteButton.Click += (_, _) => PasteAsked?.Invoke();
        SaveBlueprintButton.Click += (_, _) => SaveBlueprintAsked?.Invoke();
        LibraryButton.Click += (_, _) => LibraryAsked?.Invoke();
        ClipInfo.Text = "Copies the shown objects inside the selection, with the ground under their buildings.";
        ExportButton.Click += (_, _) => Message?.Invoke(ExportHeightmap() ?? "");
        ExportButton.Tip("area.export");
        ImportButton.Click += async (_, _) => { if (await PickPicture() is string path) { LoadPicture(path); } };
        ImportButton.Tip("area.import");
        CancelPictureButton.Click += (_, _) => { _picture = null; _pictureBox.IsVisible = false; };
        PutButton.Click += (_, _) => PutPicture();
        UnresetButton.Click += (_, _) => CancelReset();
        ToPickButton.Tip("area.pick");
        ToPickButton.Click += (_, _) => PickKindAsked?.Invoke("Replace", UsePicked);
        Area.Changed += Refresh;
        Area.Changed += DropPicture;
        NoLimitBox.Tip("brush.noLimit");
        NoLimitBox.IsCheckedChanged += (_, _) => NoLimitChanged?.Invoke(NoLimitBox.IsChecked == true);

        Control Row(string label, Control input, Control? after = null)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,Auto") };
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
        Control For(Control c, params Act[] acts)
        {
            _rows[c] = acts;
            return c;
        }
        TextBlock Help(string t) => new() { Text = t, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
        Card = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 310,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Area", FontSize = 14, FontWeight = FontWeight.SemiBold },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { BoxButton, PolyButton, ClearButton } },
                    Info,
                    Row("Soft edge", SoftSlider, softV),
                    Row("Action", ActionBox),
                    For(Row("Height", HeightBox, AverageButton), Act.Flatten),
                    For(Row("Amount", AmountBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }), Act.Raise, Act.Lower),
                    For(Row("Paint", PaintBox), Act.Paint),
                    For(NoLimitBox, Act.Flatten, Act.Raise, Act.Lower),
                    For(VolumeText, Act.Flatten, Act.Raise, Act.Lower),
                    For(Help("Evens out bumps inside the selection."), Act.Smooth),
                    For(Help("Natural-looking bumps, with the Bumps and Size of the Naturalize brush."), Act.Natural),
                    For(Help("Back to the ground the game generated, paint removed."), Act.Restore),
                    For(Help("Weathers the ground inside: slopes settle and rain cuts gullies (rest angle from the Erode brush)."), Act.Erode),
                    For(kinds, Act.Remove, Act.Select, Act.Replace, Act.Regrow, Act.Backup),
                    For(Row("Replace", FromBox), Act.Replace),
                    For(Row("with", ToBox, ToPickButton), Act.Replace),
                    For(Help("Puts back what the game grows here: its own trees, rocks, bushes and pickables for the biome, by its vegetation rules, for the kinds chosen above."), Act.Regrow),
                    For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { CopyButton, CutButton, PasteButton } }, Act.Copy),
                    For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { SaveBlueprintButton, LibraryButton } }, Act.Copy),
                    For(ClipInfo, Act.Copy),
                    For(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ExportButton, ImportButton } }, Act.Heightmap),
                    For(_pictureBox = new StackPanel
                    {
                        Spacing = 4,
                        IsVisible = false,
                        Children =
                        {
                            PictureInfo,
                            Row("Lowest", LowestBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
                            Row("Highest", HighestBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }),
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { PutButton, CancelPictureButton } },
                        },
                    }, Act.Heightmap),
                    For(Help("Black is the lowest height, white the highest; north is at the top of the picture, one pixel per metre when exported. An import fits the picture to the selection (or the whole area) and works within the game's ±8 m."), Act.Heightmap),
                    For(Row("Backup", BackupBox), Act.Backup),
                    For(BackupGroundBox, Act.Backup),
                    For(BackupObjectsBox, Act.Backup),
                    For(BackupInfo, Act.Backup),
                    For(KeepBuildingsBox, Act.Reset),
                    For(ResetGroundBox, Act.Reset),
                    For(Help("On save, the zones under the selection lose their trees, rocks, ruins and dungeon entrances, and the game generates them again the next time someone goes there."), Act.Reset),
                    For(UnresetButton, Act.Reset),
                    ApplyButton,
                },
            },
        };
        ShowShape();
        ShowRows();
    }

    /// <summary>Marks the Box or Polygon button as on.</summary>
    private void ShowShape()
    {
        BoxButton.Classes.Set("on", Area.Box);
        PolyButton.Classes.Set("on", !(Area.Box));
        Refresh();
    }

    /// <summary>Shows only the rows of the current action, and names the action on Apply and in the tips.</summary>
    private void ShowRows()
    {
        var act = Current;
        foreach (var (row, acts) in _rows)
        {
            row.IsVisible = acts.Contains(act);
        }
        ApplyButton.IsVisible = act is not (Act.Heightmap or Act.Copy);
        // The action's own explanation, on the list and on its button.
        string what = Tips.Of($"areaAct.{act}");
        ToolTip.SetTip(ActionBox, $"{Tips.Of("area.action")} {what}");
        ToolTip.SetTip(ApplyButton, Lang.T("area.applyTip", what));
        ApplyButton.Content = act switch
        {
            Act.Remove => "Remove the objects (Enter)",
            Act.Select => "Select the objects (Enter)",
            Act.Replace => "Replace (Enter)",
            Act.Reset => "Reset zones… (Enter)",
            Act.Regrow => "Regrow nature (Enter)",
            Act.Backup => "Restore the selection (Enter)",
            Act.Heightmap or Act.Copy => "",
            _ => $"{Actions.First(a => a.Act == act).Label} (Enter)",
        };
        Refresh();
    }

    /// <summary>A number box's value, 0 when empty.</summary>
    private static float Value(NumericUpDown b) => (float)(b.Value ?? 0);

    /// <summary>
    /// The things inside the selection (indices), not deleted, of shown kinds (or added in this session).
    /// </summary>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <param name="chosenKindsOnly">Only the kinds toggled on, and only where the Mask lets the ground through.</param>
    /// <param name="includeHidden">Also kinds switched off in View (a backup restore compares with everything there).</param>
    /// <param name="maskAt">The Mask test to use (one the caller already took), or null to take it here.</param>
    internal List<int> ThingsInside(List<Vector2> poly, bool chosenKindsOnly, bool includeHidden = false, Func<float, float, bool>? maskAt = null)
    {
        var out_ = new List<int>();
        if (_view.Scene is not { } s)
        {
            return out_;
        }
        float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
        // Taken once: each Mask snapshot copies the whole ground.
        maskAt ??= chosenKindsOnly ? MaskTest() : null;
        lock (s.Things)
        {
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (t.Gone || !AreaTool.Inside(poly, t.Position.X - ox, t.Position.Z - oz))
                {
                    continue;
                }
                var k = KindOf(t);
                if ((includeHidden || t.Id < 0 || _view.IsShown(k)) && (!chosenKindsOnly || Area.Kinds.Contains(k) && maskAt!(t.Position.X - ox, t.Position.Z - oz)))
                {
                    out_.Add(i);
                }
            }
        }
        return out_;
    }

    /// <summary>
    /// Whether the Mask lets the ground at a grid point through (the object actions pick only there). The Mask is
    /// taken now, once: each snapshot copies the whole ground, so take one per action, not one per object.
    /// </summary>
    /// <returns>A test by grid point (metres from the scene's corner); always true when the Mask is off.</returns>
    private Func<float, float, bool> MaskTest()
    {
        if (_session() is not { } s || s.MaskNow() is not { } m)
        {
            return static (_, _) => true;
        }
        int w = s.Ground.W, h = s.Ground.H;
        return (gx, gz) =>
        {
            int x = Math.Clamp((int)MathF.Round(gx), 0, w - 1), z = Math.Clamp((int)MathF.Round(gz), 0, h - 1);
            return m(z * w + x) > 0;
        };
    }

    /// <summary>A thing's object kind.</summary>
    private ObjectKind KindOf(WorldScene.Thing t) => ObjectKinds.Of(_nameOf(t.Prefab), t.Piece, t.Tamed);
    /// <summary>A prefab hash's name, or the hash itself when unknown.</summary>
    private string NameOf(int prefab) => _nameOf(prefab) ?? prefab.ToString();

    /// <summary>
    /// Cut and fill: what Flatten, Raise or Lower would move, and how much the ground inside was raised
    /// and dug since generated (again after every change of the ground, as the web editor did).
    /// </summary>
    public void RefreshVolume()
    {
        var g = _session()?.Ground;
        VolumeText.Text = g != null ? Area.Volume(g, Ground(Current) ?? AreaTool.GroundAction.Flatten, Value(HeightBox), Value(AmountBox), _session()?.MaskNow()) : "";
    }

    /// <summary>The selection changed: its size, what is inside, the lists and the volumes.</summary>
    public void Refresh()
    {
        var poly = Area.Polygon();
        RefreshVolume();
        // Before the selection's lists: the "with" list follows the world, selection or not.
        FillCreatable();
        if (poly == null)
        {
            Info.Text = Area.Box ? "Drag on the ground to select a box." : "Click points around the area; double-click or Enter closes it. Backspace removes a point, Esc clears.";
            FromBox.ItemsSource = Array.Empty<string>();
            foreach (var (k, b) in KindButtons)
            {
                b.Content = ObjectKinds.Label(k);
            }
            _view.LassoChanged();
            return;
        }
        var inside = ThingsInside(poly, chosenKindsOnly: false);
        var s = _view.Scene!;
        Info.Text = $"{AreaTool.Area(poly):0} m² selected · {inside.Count} shown object(s) inside. Click outside to start a new selection.";
        var byKind = inside.GroupBy(i => KindOf(s.Things[i])).ToDictionary(x => x.Key, x => x.Count());
        foreach (var (k, b) in KindButtons)
        {
            b.Content = $"{ObjectKinds.Label(k)} {(_view.IsShown(k) ? byKind.GetValueOrDefault(k).ToString() : "hidden")}";
        }
        var names = inside.GroupBy(i => NameOf(s.Things[i].Prefab)).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} ({x.Count()})").ToList();
        FromBox.ItemsSource = names;
        FromBox.SelectedIndex = FromBox.SelectedItem is string keep && names.Contains(keep) ? names.IndexOf(keep) : names.Count > 0 ? 0 : -1;
        _view.LassoChanged();
    }

    /// <summary>
    /// The kinds the world can make, for the "with" list: worked out again for another world (its templates are its
    /// own), when a live merge brings it new kinds, or when the game's data, the mods' manifests or their catalogue
    /// change, keeping the chosen kind when it is still offered.
    /// </summary>
    private void FillCreatable()
    {
        if (_view.Scene?.World is { } w && _creatableFrom != (w, w.TemplatesGeneration, GameData.Generation, ModManifests.Generation, ModCatalogue.Generation))
        {
            _creatableFrom = (w, w.TemplatesGeneration, GameData.Generation, ModManifests.Generation, ModCatalogue.Generation);
            int chosen = ToBox.SelectedIndex >= 0 && ToBox.SelectedIndex < _creatable.Count ? _creatable[ToBox.SelectedIndex] : 0;
            _creatable = [.. w.Creatable.Where(p => _nameOf(p) != null).OrderBy(p => _nameOf(p), StringComparer.OrdinalIgnoreCase)];
            ToBox.ItemsSource = _creatable.Select(p => _nameOf(p)!).ToList();
            ToBox.SelectedIndex = _creatable.IndexOf(chosen);
        }
        else if (_view.Scene?.World == null && _creatableFrom.World != null)
        {
            // No world open: the last one is not kept alive by the list.
            _creatableFrom = default;
            _creatable = [];
            ToBox.ItemsSource = Array.Empty<string>();
        }
    }

    /// <summary>The ground action behind an action, null for the object and other actions.</summary>
    private static AreaTool.GroundAction? Ground(Act a) => a switch
    {
        Act.Flatten => AreaTool.GroundAction.Flatten,
        Act.Raise => AreaTool.GroundAction.Raise,
        Act.Lower => AreaTool.GroundAction.Lower,
        Act.Smooth => AreaTool.GroundAction.Smooth,
        Act.Natural => AreaTool.GroundAction.Natural,
        Act.Restore => AreaTool.GroundAction.Restore,
        Act.Erode => AreaTool.GroundAction.Erode,
        Act.Paint => AreaTool.GroundAction.Paint,
        _ => null,
    };

    /// <summary>The action (Apply, Enter). While one runs, another does nothing (Apply greyed out).</summary>
    public async Task Apply()
    {
        if (_applying)
        {
            return;
        }
        _applying = true;
        ApplyButton.IsEnabled = false;
        try
        {
            await ApplyOnce();
        }
        finally
        {
            _applying = false;
            ApplyButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Whether the session is still the one open after a wait; when it is not (another area opened, the world
    /// closed), says that nothing was applied.
    /// </summary>
    /// <param name="session">The session the action started on.</param>
    private bool StillOpen(EditSession session)
    {
        if (ReferenceEquals(_session(), session))
        {
            return true;
        }
        Message?.Invoke("The area changed while this was running: nothing was applied.");
        return false;
    }

    /// <inheritdoc cref="Apply"/>
    private async Task ApplyOnce()
    {
        if (_session() is not { } session)
        {
            return;
        }
        var poly = Area.Polygon();
        if (poly == null)
        {
            Message?.Invoke("Select an area first.");
            return;
        }
        var act = Current;
        if (Ground(act) is { } ga)
        {
            float[] paint = Brush.PaintOf(Paints[Math.Max(0, PaintBox.SelectedIndex)].Tool)!;
            string name = act == Act.Paint ? $"Paint {Paints[Math.Max(0, PaintBox.SelectedIndex)].Label.ToLowerInvariant()}" : AreaTool.Label(ga);
            var touched = session.EditGround($"Area: {name}", g => Area.Apply(g, session.Brush, ga, Value(HeightBox), Value(AmountBox), paint, session.MaskNow()));
            Message?.Invoke($"{name} applied to {touched.Count} point(s).");
            Refresh();
            return;
        }
        switch (act)
        {
            case Act.Remove:
            {
                var inside = ThingsInside(poly, chosenKindsOnly: true);
                if (inside.Count == 0)
                {
                    Message?.Invoke("No objects of the chosen kinds inside the selection.");
                    return;
                }
                // A linked group's root takes its parts with it (ObjectLinks: a ship).
                var list = session.WithLinked(inside, anyPart: false).Indices;
                session.Commit($"Area: removed {list.Count} object(s)", null, list, []);
                Message?.Invoke($"Removed {list.Count} object(s). Ctrl+Z brings them back.");
                break;
            }
            case Act.Select:
                _view.Select(ThingsInside(poly, chosenKindsOnly: true));
                SwitchToSelect?.Invoke();
                break;
            case Act.Replace:
            {
                var s = _view.Scene!;
                string? from = (FromBox.SelectedItem as string)?.Split(" (")[0];
                int to = ToBox.SelectedIndex >= 0 && ToBox.SelectedIndex < _creatable.Count ? _creatable[ToBox.SelectedIndex] : 0;
                if (from == null || to == 0)
                {
                    Message?.Invoke("Choose what to replace, and with what.");
                    return;
                }
                var list = ThingsInside(poly, chosenKindsOnly: false).Where(i => NameOf(s.Things[i].Prefab) == from).ToList();
                Replace(session, list, to);
                break;
            }
            case Act.Reset:
                await ResetZones(session);
                break;
            case Act.Regrow:
                await RegrowNature(session, poly);
                break;
            case Act.Backup:
                await RestoreBackup(session, poly);
                break;
        }
        Refresh();
    }

    /// <summary>The eyedropper's kind into the "with" list, when the game can make it.</summary>
    internal void UsePicked(int prefab)
    {
        FillCreatable();
        int i = _creatable.IndexOf(prefab);
        if (i < 0)
        {
            Message?.Invoke($"{NameOf(prefab)} cannot be placed: the game has no such kind to copy.");
            return;
        }
        ToBox.SelectedIndex = i;
        Message?.Invoke($"Replace with {NameOf(prefab)}.");
    }

    /// <summary>Replaces things with objects of another kind, where they are and turned as they are.</summary>
    /// <param name="session">The session the change is committed to (one undo step).</param>
    /// <param name="things">The things, as indices into the scene's things.</param>
    /// <param name="prefab">The prefab hash of the new kind.</param>
    public void Replace(EditSession session, IReadOnlyList<int> things, int prefab)
    {
        var s = session.Scene;
        bool piece = PieceCatalog.Get(prefab)?.Tool != null;
        var adds = things.Select(i => s.Things[i]).Select(t => (new NewObject(0, prefab, t.Position, t.Rotation, 0), piece)).ToList();
        session.Commit($"Replaced {things.Count} with {NameOf(prefab)}", null, things, adds);
        Message?.Invoke($"Replaced {things.Count} object(s) with {NameOf(prefab)}.");
    }

    // ---- Heightmaps: the area's ground as a 16-bit picture (with its lowest and highest heights), and a
    // picture back into the ground, fitted to the selection or the whole area.
    /// <summary>Where Export writes (null: the heightmaps folder in the app's data folder). Tests use their own.</summary>
    internal string? HeightmapFolder { get; set; }

    /// <summary>
    /// Writes the whole area's ground as a 16-bit PNG, with its lowest and highest heights in it, named after the
    /// world and its zones.
    /// </summary>
    /// <param name="folder">Where to write it; null: <see cref="HeightmapFolder"/>.</param>
    /// <returns>The message for the status bar, or null when no world is open.</returns>
    internal string? ExportHeightmap(string? folder = null)
    {
        if (_view.Scene is not { World: { } world } s)
        {
            return null;
        }
        int x1 = s.X0 + s.ZonesX - 1, z1 = s.Z0 + s.ZonesZ - 1;
        byte[] png = Heightmaps.Encode(s.W, s.H, s.Heights, $"{world.Name} zones {s.X0},{s.Z0} to {x1},{z1}", out float min, out float max);
        string dir = folder ?? HeightmapFolder ?? Path.Combine(AppSettings.DataDir, "heightmaps");
        string safe = string.Concat(world.Name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        string path = Path.Combine(dir, $"{safe}_{s.X0}_{s.Z0}_{x1}_{z1}.png");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, png);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not write the heightmap to {path}: {ex.Message}";
        }
        return $"Heightmap written to {path} ({s.W} × {s.H}, {min:0.0} to {max:0.0} m).";
    }

    /// <summary>
    /// Reads a picture to import, fitted to the selection's box or else the whole area (as far as the Mask lets it),
    /// and fills Lowest and Highest with the heights it was exported with, else the range of the ground it covers.
    /// </summary>
    internal void LoadPicture(string path)
    {
        if (_session() is not { } session)
        {
            return;
        }
        var g = session.Ground;
        // Where it goes: the selection's box, or the whole area (as far as the Mask lets it).
        List<(int G, float W)> cells;
        int x0, z0, w, h;
        var mask = session.MaskNow();
        if (Area.WeightsIn(g.W, g.H, mask) is { } a)
        {
            (cells, x0, z0, w, h) = (a.Cells, a.X0, a.Z0, a.X1 - a.X0 + 1, a.Z1 - a.Z0 + 1);
        }
        else
        {
            cells = [.. Enumerable.Range(0, g.W * g.H).Select(p => (p, mask?.Invoke(p) ?? 1f)).Where(c => c.Item2 > 0)];
            (x0, z0, w, h) = (0, 0, g.W, g.H);
        }
        Heightmaps.Picture pic;
        try
        {
            pic = Heightmaps.Read(File.ReadAllBytes(path), w, h);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Message?.Invoke($"That picture cannot be used: {ex.Message}");
            return;
        }
        _picture = (pic.Values, g, x0, z0, w, h, cells, Area.Polygon());
        // The heights written in the picture by the editor, else the range of the ground it covers now.
        float lo = cells.Count > 0 ? cells.Min(c => g.HeightOf(c.G)) : 0, hi = cells.Count > 0 ? cells.Max(c => g.HeightOf(c.G)) : 0;
        LowestBox.Value = (decimal)(pic.Min is float mn ? MathF.Round(mn, 3) : MathF.Round(lo, 1));
        HighestBox.Value = (decimal)(pic.Max is float mx ? MathF.Round(mx, 3) : MathF.Round(hi, 1));
        PictureInfo.Text = $"Picture {pic.Width} × {pic.Height} fitted to {w} × {h} m ({(Area.Polygon() != null ? "the selection" : "the whole area")}){(pic.Min != null ? ", with the heights it was exported with" : "")}.";
        _pictureBox.IsVisible = true;
    }

    /// <summary>
    /// Puts the picture read into the ground, from Lowest (black) to Highest (white), faded by the soft edge.
    /// Locked points are skipped; points the game's ±8 m keeps from their height are counted in the message.
    /// </summary>
    internal void PutPicture()
    {
        if (_picture is not { } p || _session() is not { } session)
        {
            return;
        }
        if (p.Ground != session.Ground)
        {
            // Fitted to another area's ground: its points would land anywhere in this one.
            _picture = null;
            _pictureBox.IsVisible = false;
            Message?.Invoke("The picture was fitted to another area: import it again.");
            return;
        }
        float lo = Value(LowestBox), hi = Value(HighestBox);
        int clamped = 0;
        var touched = session.EditGround("Heightmap import", g =>
        {
            var (list, c) = PutHeights(g, p.Values, p.X0, p.Z0, p.W, p.H, p.Cells, lo, hi);
            clamped = c;
            return (list, (0, 0, g.W - 1, g.H - 1));
        });
        _picture = null;
        _pictureBox.IsVisible = false;
        Message?.Invoke(clamped > 0 ? $"Imported, but {clamped} point(s) could not reach their height: the game keeps the ground within ±8 m of the original (red points)."
            : $"Imported the heightmap into {touched.Count} point(s). Ctrl+Z undoes it.");
    }

    /// <summary>
    /// The selection changed: a picture read was fitted to the old one, so it is dropped, unless the outline is still
    /// the same (a polygon closed with Enter, a redraw of the same box).
    /// </summary>
    private void DropPicture()
    {
        if (_picture is not { } p)
        {
            return;
        }
        var now = Area.Polygon();
        if (p.Fit == null ? now == null : now != null && p.Fit.SequenceEqual(now))
        {
            return;
        }
        _picture = null;
        _pictureBox.IsVisible = false;
        Message?.Invoke("The picture read was dropped: the selection changed. Import it again.");
    }

    /// <summary>
    /// A heightmap's values (0 black to 1 white, w × h from grid point x0, z0) into the ground from lo to hi, each
    /// point faded by its weight; locked points and those outside the picture are left alone.
    /// </summary>
    /// <returns>The points changed, and how many fully weighted ones the game's ±8 m kept from their height.</returns>
    internal static (List<int> Touched, int Clamped) PutHeights(Ground g, float[] values, int x0, int z0, int w, int h, List<(int G, float W)> cells, float lo, float hi)
    {
        var list = new List<int>();
        int clamped = 0;
        foreach (var (pt, wt) in cells)
        {
            int gx = pt % g.W, gz = pt / g.W, ix = gx - x0, iz = gz - z0;
            if (g.Locked(gx, gz) || ix < 0 || iz < 0 || ix >= w || iz >= h)
            {
                continue;
            }
            float want = lo + values[iz * w + ix] * (hi - lo), now = g.HeightOf(pt);
            g.SetHeight(pt, now + (want - now) * wt);
            if (wt > 0.999f && MathF.Abs(g.HeightOf(pt) - want) > 0.05f)
            {
                clamped++;
            }
            list.Add(pt);
        }
        return (list, clamped);
    }

    /// <summary>
    /// Regrow nature: the game's own vegetation for the zones under the selection (by its rules, on
    /// the ground as it is now), kept inside the selection, for the chosen kinds, and not where something
    /// already stands (a tree still there is not doubled) or near buildings.
    /// </summary>
    private async Task RegrowNature(EditSession session, List<Vector2> poly)
    {
        var s = session.Scene;
        float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
        // Taken once, when the spots are first judged (after the generator ran), not once per spot.
        var maskAt = new Lazy<Func<float, float, bool>>(MaskTest);
        var adds = await Growth.Spots(session, poly.Min(p => p.X) + ox, poly.Min(p => p.Y) + oz, poly.Max(p => p.X) + ox, poly.Max(p => p.Y) + oz,
            o => AreaTool.Inside(poly, o.X - ox, o.Z - oz) && Area.Kinds.Contains(ObjectKinds.Of(o.Name, false)) && maskAt.Value(o.X - ox, o.Z - oz), _nameOf, m => Message?.Invoke(m));
        if (adds == null || !StillOpen(session))
        {
            return;
        }
        if (adds.Count == 0)
        {
            Message?.Invoke("Nothing to regrow: the game grows none of the chosen kinds here, or it is all still standing.");
            return;
        }
        Growth.Commit(session, $"Area: regrew {adds.Count} object(s)", adds);
        var names = adds.Select(o => o.Name).Distinct().ToList();
        Message?.Invoke($"Regrew {adds.Count} object(s): {string.Join(", ", names.Take(6))}{(names.Count > 6 ? "…" : "")}. Ctrl+Z takes them back; Save writes them.");
    }

    // ---- Restore from a backup (WorldEdit's //restore): the ground and objects inside the selection as
    // they were in a backup of this world. Objects unchanged since then are left alone.
    /// <summary>
    /// Lists the world's backups (the editor's and the game's), then the folders chosen by hand, then
    /// "Another folder…".
    /// </summary>
    /// <param name="select">The folder to select; null keeps the one selected.</param>
    private void FillBackups(string? select = null)
    {
        var s = _view.Scene;
        string? keep = select ?? (BackupBox.SelectedIndex >= 0 && BackupBox.SelectedIndex < _backupPaths.Count ? _backupPaths[BackupBox.SelectedIndex] : null);
        // A world without a folder (live from a game) has no backups of its own; folders chosen by hand stay.
        var found = s?.World != null && !string.IsNullOrEmpty(s.World.Directory) && Directory.Exists(s.World.Directory) ? Backups.Find(s.World.Directory) : [];
        var extra = _backupPaths.Where(p => !found.Any(f => f.Path == p)).ToList();
        _backupPaths = [.. found.Select(b => b.Path), .. extra];
        // The list keeps the selected text across a new source: with "Another folder…" still selected it
        // would ask for a folder again.
        BackupBox.SelectedIndex = -1;
        BackupBox.ItemsSource = found.Select(b => $"{(b.Kind == "game" ? "Game" : "Editor")} · {b.Date:g}").Concat(extra).Append("Another folder…").ToList();
        BackupBox.SelectedIndex = keep != null ? _backupPaths.IndexOf(keep) : -1;
    }

    /// <summary>
    /// A backup's world (read once; the last one is kept, and only ever used for a world of its seed: a
    /// folder chosen by hand stays in the list when another world is opened).
    /// </summary>
    /// <returns>The backup and its edits, or null when it cannot be read or is another world (BackupInfo says why).</returns>
    internal async Task<(WorldSave World, EditStore Edits)?> OpenBackup(string path, WorldSave current)
    {
        if (_backup is { } b && b.Path == path && b.World.Seed == current.Seed && b.World.SeedName == current.SeedName)
        {
            return (b.World, b.Edits);
        }
        BackupInfo.Text = "Reading the backup…";
        try
        {
            var w = await Task.Run(() => WorldSave.Load(path));
            if (w.Seed != current.Seed || w.SeedName != current.SeedName)
            {
                BackupInfo.Text = $"That is another world (seed {w.SeedName}, this one is {current.SeedName}).";
                return null;
            }
            var edits = new EditStore(w);
            _backup = (path, w, edits);
            BackupInfo.Text = $"Backup: save #{w.SaveNumber} of {w.Name}, {w.ObjectCount:N0} objects ({Directory.GetLastWriteTime(path):g}).";
            return (w, edits);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            BackupInfo.Text = $"That folder is not a world save that can be read: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Puts the ground and the objects of the chosen kinds inside the selection back as they were in a backup (each
    /// when its box is ticked), as one undo step. Objects unchanged since then (same kind, place and facing) are left
    /// alone; runestones are not touched.
    /// </summary>
    /// <param name="session">The session the restore is committed to (one undo step).</param>
    /// <param name="poly">The selection, in metres from the scene's corner.</param>
    /// <param name="path">The backup folder; null: the one chosen on the list.</param>
    internal async Task RestoreBackup(EditSession session, List<Vector2> poly, string? path = null)
    {
        var s = session.Scene;
        path ??= BackupBox.SelectedIndex >= 0 && BackupBox.SelectedIndex < _backupPaths.Count ? _backupPaths[BackupBox.SelectedIndex] : null;
        if (path == null)
        {
            Message?.Invoke("Choose a backup first.");
            return;
        }
        if (await OpenBackup(path, s.World) is not var (bw, be))
        {
            Message?.Invoke(BackupInfo.Text ?? "");
            return;
        }
        if (!StillOpen(session))
        {
            return;
        }
        int groundPoints = 0;
        Func<Ground, (List<int>, (int, int, int, int))>? ground = BackupGroundBox.IsChecked != true ? null : g =>
        {
            // The backup's ground edits over the block.
            var bk = new Ground(g.W, g.H, g.X0, g.Z0, g.ZonesX, g.ZonesZ);
            Array.Copy(g.Base, bk.Base, g.Base.Length);
            bk.TakeEdits(be);
            var touched = new List<int>();
            if (Area.WeightsIn(g.W, g.H, session.MaskNow()) is not { } a)
            {
                return (touched, (0, 0, 0, 0));
            }
            foreach (var (p, w) in a.Cells)
            {
                if (g.Locked(p % g.W, p / g.W))
                {
                    continue;
                }
                bool same = g.Mod[p] == bk.Mod[p] && g.PMod[p] == bk.PMod[p] && MathF.Abs(g.HeightOf(p) - bk.HeightOf(p)) < 1e-4f && MathF.Abs(g.Original(p) - bk.Original(p)) < 1e-4f
                    && (g.PMod[p] == 0 || Enumerable.Range(0, 4).All(c => MathF.Abs(g.Paint[p * 4 + c] - bk.Paint[p * 4 + c]) < 1e-4f));
                if (same)
                {
                    continue;
                }
                if (w > 0.999f)
                {
                    // Inside the soft edge: exactly as in the backup.
                    g.Mod[p] = bk.Mod[p];
                    g.Level[p] = bk.Level[p];
                    g.Smooth[p] = bk.Smooth[p];
                    g.PMod[p] = bk.PMod[p];
                    Array.Copy(bk.Paint, p * 4, g.Paint, p * 4, 4);
                    // The backup's original ground (its own ground discs) as a lift on this one's.
                    g.Lift[p] = bk.Original(p) - g.Base[p];
                }
                else
                {
                    float h = g.HeightOf(p);
                    g.SetHeight(p, h + (bk.HeightOf(p) - h) * w);
                    if (bk.PMod[p] != 0 || g.PMod[p] != 0)
                    {
                        if (g.PMod[p] == 0)
                        {
                            g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
                            g.Paint[p * 4 + 3] = 1;
                            g.PMod[p] = 1;
                        }
                        for (int c = 0; c < 4; c++)
                        {
                            float target = bk.PMod[p] != 0 ? bk.Paint[p * 4 + c] : c == 3 ? 1 : 0;
                            g.Paint[p * 4 + c] += (target - g.Paint[p * 4 + c]) * w;
                        }
                    }
                }
                touched.Add(p);
            }
            groundPoints = touched.Count;
            return (touched, (a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1));
        };
        var remove = new List<int>();
        var adds = new List<(NewObject, bool)>();
        if (BackupObjectsBox.IsChecked == true)
        {
            float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
            // Runestones are left as they are on both sides (a location proxy cannot be copied back). Both sides are
            // picked alike (where the Mask lets through too): an unchanged object picked on one side only would be
            // brought back on top of itself.
            var maskAt = MaskTest();
            var wanted = WorldScene.ReadThings(bw, s.X0, s.Z0, s.ZonesX, s.ZonesZ, new HashSet<int>())
                .Where(o => !o.Runestone && AreaTool.Inside(poly, o.Position.X - ox, o.Position.Z - oz) && Area.Kinds.Contains(KindOf(o)) && maskAt(o.Position.X - ox, o.Position.Z - oz)).ToList();
            var current = ThingsInside(poly, chosenKindsOnly: true, includeHidden: true, maskAt).Where(i => !s.Things[i].Runestone).ToList();
            // Unchanged since the backup (same kind, place and facing): kept as they are.
            static bool Same(WorldScene.Thing r, WorldScene.Thing o) => r.Prefab == o.Prefab && MathF.Abs(r.Position.X - o.Position.X) < 0.01f
                && MathF.Abs(r.Position.Y - o.Position.Y) < 0.01f && MathF.Abs(r.Position.Z - o.Position.Z) < 0.01f
                && MathF.Abs(((r.Rotation.Y - o.Rotation.Y) % 360 + 540) % 360 - 180) < 0.6f;
            var kept = new HashSet<int>();
            foreach (var o in wanted)
            {
                int r = current.FirstOrDefault(i => !kept.Contains(i) && Same(s.Things[i], o), -1);
                if (r >= 0)
                {
                    kept.Add(r);
                    continue;
                }
                byte[] raw = bw.ObjectBytes(o.Id);
                var z = ZdoData.Parse(raw);
                adds.Add((new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, raw), o.Piece));
            }
            remove = [.. current.Where(i => !kept.Contains(i))];
        }
        if (ground == null && remove.Count == 0 && adds.Count == 0)
        {
            Message?.Invoke("Nothing to restore: the selection is as it was in the backup.");
            return;
        }
        var added = session.Commit("Restored from backup", ground, remove, adds);
        if (groundPoints == 0 && remove.Count == 0 && added.Count == 0)
        {
            Message?.Invoke("Nothing to restore: the selection is as it was in the backup.");
            return;
        }
        Message?.Invoke($"Restored from the backup: {(groundPoints > 0 ? "the ground, " : "")}{added.Count} object(s) brought back, {remove.Count} removed. Ctrl+Z undoes it.");
    }

    /// <summary>
    /// Marks the zones under the selection for reset when saving (after asking), with the Keep buildings and Reset
    /// ground choices; resetting the ground also clears their ground edits now.
    /// </summary>
    private async Task ResetZones(EditSession session)
    {
        var zones = Area.ZonesUnder(session.Ground);
        if (zones.Count == 0)
        {
            Message?.Invoke("Select an area first.");
            return;
        }
        bool keep = KeepBuildingsBox.IsChecked == true, ground = ResetGroundBox.IsChecked == true;
        if (!await Confirm($"Reset {zones.Count} zone(s) ({string.Join(" · ", zones.Select(z => $"{z.X}, {z.Z}"))}) when you save?\n\n"
            + $"• Every tree, rock, ruin and dungeon entrance in them is removed{(keep ? " (your buildings stay)" : ", your buildings too")}.\n"
            + (ground ? "• Their ground edits are undone.\n" : "") + "• Valheim generates the zones again the next time a player goes there.")
            || !StillOpen(session))
        {
            return;
        }
        session.MarkResets(zones, keep, ground, $"Reset {zones.Count} zone(s)");
        Message?.Invoke($"{zones.Count} zone(s) marked for reset (red outline). Save to apply; Ctrl+Z cancels.");
    }

    /// <summary>Unmarks the zones under the selection that are marked for reset.</summary>
    private void CancelReset()
    {
        if (_session() is not { } session)
        {
            return;
        }
        var marked = session.Resets.Select(r => (r.X, r.Z)).ToHashSet();
        int count = Area.ZonesUnder(session.Ground).Count(marked.Contains);
        if (count == 0)
        {
            Message?.Invoke("No zone marked for reset under the selection.");
            return;
        }
        session.UnmarkResets(Area.ZonesUnder(session.Ground), $"Cancelled reset of {count} zone(s)");
        Message?.Invoke($"Reset cancelled for {count} zone(s).");
    }
}
