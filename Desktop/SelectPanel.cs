using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Select tool's options, next to the rail: what to do with the selection (delete, select more), how moves land,
/// the exact place, and the keys.
/// </summary>
public sealed class SelectPanel
{
    /// <summary>The panel itself, shown next to the rail while the Select tool is on.</summary>
    public Control Card { get; }
    /// <summary>The Select tool the panel drives.</summary>
    private readonly SelectTool _tool;
    /// <summary>Explains the exact place boxes (where the selection is, or what Move by means).</summary>
    private readonly TextBlock _info = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The exact place: X, Y, Z and Turn, with Move there and Move by; shown while something is selected.
    /// </summary>
    private readonly Control _exact;
    /// <summary>Move by: the boxes are offsets from where the selection is, not a place.</summary>
    private bool _by;
    /// <summary>
    /// Where the selection was when <see cref="Refresh"/> last filled the boxes, unrounded: Move there moves only
    /// along the axes whose box was changed since, by the difference from this (the boxes show rounded values).
    /// </summary>
    private (float X, float Y, float Z, float Turn)? _shown;

    // For tests.
    /// <summary>Deletes the selection.</summary>
    internal Button DeleteButton { get; }
    /// <summary>Adds every building piece connected to the selected ones to the selection.</summary>
    internal Button BuildingButton { get; }
    /// <summary>Selects every shown object of the selected kinds.</summary>
    internal Button SameButton { get; }
    /// <summary>Selects the shown objects that are not selected, and only those.</summary>
    internal Button InvertButton { get; }
    /// <summary>Puts each object on the ground when moving.</summary>
    internal CheckBox GroundBox { get; }
    /// <summary>Snaps moved pieces to other pieces.</summary>
    internal CheckBox SnapBox { get; }
    /// <summary>The exact place's X (east), in metres.</summary>
    internal NumericUpDown XBox { get; } = Num(0.1m);
    /// <summary>The exact place's Y (height), in metres.</summary>
    internal NumericUpDown YBox { get; } = Num(0.1m);
    /// <summary>The exact place's Z (north), in metres.</summary>
    internal NumericUpDown ZBox { get; } = Num(0.1m);
    /// <summary>The exact turn, in degrees clockwise seen from above.</summary>
    internal NumericUpDown TurnBox { get; } = Num(1m);
    /// <summary>Moves the selection there (or by the boxes' values with Move by).</summary>
    internal Button ApplyButton { get; } = new() { Content = Lang.T("select.label.moveThere"), FontSize = 12 };
    /// <summary>Opens the data of the one selected object (the window handles it).</summary>
    internal Button InspectButton { get; } = new() { Content = Lang.T("select.label.inspect"), FontSize = 12, IsEnabled = false };
    /// <summary>Make player built: gives the selected pieces the chosen builder (the window handles it).</summary>
    internal Button ClaimButton { get; } = new() { Content = Lang.T("select.label.claim"), FontSize = 12, IsEnabled = false };
    /// <summary>
    /// Replace with: the kinds this world can make (filled by the window, in <see cref="ReplaceKinds"/> order).
    /// </summary>
    internal ComboBox ReplaceBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };
    /// <summary>Replaces the selection with the kind chosen (raises <see cref="ReplaceAsked"/>).</summary>
    internal Button ReplaceButton { get; } = new() { Content = Lang.T("select.label.replace"), FontSize = 12 };
    /// <summary>The prefab hashes of <see cref="ReplaceBox"/>'s entries, in its order.</summary>
    public List<int> ReplaceKinds { get; set; } = [];
    /// <summary>Replace the selection with this prefab.</summary>
    public event Action<int>? ReplaceAsked;
    /// <summary>Eyedropper: the "Replace with" kind picked from the world.</summary>
    internal Button ReplacePickButton { get; } = new() { Content = Lang.T("select.label.pick"), FontSize = 11, Padding = new Thickness(6, 2) };
    /// <summary>
    /// Asks the window to take the next click on an object: the action's name for the prompt, and what to do with the
    /// picked prefab hash.
    /// </summary>
    public event Action<string, Action<int>>? PickKindAsked;
    /// <summary>A line for the status bar, in both languages.</summary>
    public event Action<Said>? Message;
    /// <summary>Switches Move by on or off.</summary>
    internal Button ByButton { get; } = new() { Content = Lang.T("select.label.moveBy"), FontSize = 12 };

    /// <summary>Saved selections (per world): the open world's, with their object counts.</summary>
    internal ComboBox SavedBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 300 };
    /// <summary>Keeps the selection under a name.</summary>
    internal Button KeepButton { get; } = new() { Content = Lang.T("select.label.keep"), FontSize = 12 };
    /// <summary>Selects the saved selection.</summary>
    internal Button LoadButton { get; } = new() { Content = Lang.T("rail.label.select"), FontSize = 12 };
    /// <summary>Adds the saved selection to what is selected.</summary>
    internal Button AddSavedButton { get; } = new() { Content = Lang.T("common.add"), FontSize = 12 };
    /// <summary>Forgets the saved selection.</summary>
    internal Button ForgetButton { get; } = new() { Content = Lang.T("select.label.forget"), FontSize = 12 };
    /// <summary>Select, Add and Forget, shown while a saved selection is chosen.</summary>
    private readonly StackPanel _savedRow;
    /// <summary>The open world's saved selections, in the list's order.</summary>
    private List<SavedSelections.Saved> _saved = [];
    /// <summary>The window's: the open world's name (null while none).</summary>
    internal Func<string?> WorldName { get; set; } = () => null;
    /// <summary>The window's: the scene's things (null while none).</summary>
    internal Func<IReadOnlyList<WorldScene.Thing>?> Things { get; set; } = () => null;
    /// <summary>The window's: the selection, as indices into the things.</summary>
    internal Func<IReadOnlyCollection<int>> SelectedIds { get; set; } = Array.Empty<int>;
    /// <summary>The window's: selects these things.</summary>
    internal Action<IEnumerable<int>> SelectIds { get; set; } = _ => { };
    /// <summary>The window's: asks for a name, suggesting one; null or empty: cancelled.</summary>
    internal Func<string, Task<string?>> AskName { get; set; } = Task.FromResult<string?>;
    /// <summary>The window's: asks a yes/no question.</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);

    /// <summary>A number box that steps by <c>step</c>.</summary>
    private static NumericUpDown Num(decimal step) => new() { Increment = step, FormatString = "0.##", FontSize = 12, MinWidth = 90 };

    /// <summary>An action button with its tip.</summary>
    private static Button Act(string text, string tip)
    {
        var b = new Button { Content = text, FontSize = 12, Padding = new Thickness(8, 3) };
        ToolTip.SetTip(b, tip);
        return b;
    }

    /// <summary>Builds the panel for the Select tool.</summary>
    public SelectPanel(SelectTool tool)
    {
        _tool = tool;
        // Keyed: Claude is kept from it by its key, whatever its label reads as in the language in use.
        DeleteButton = Act(Lang.T("common.delete"), Tips.Of("select.delete")).Tip("select.delete");
        BuildingButton = Act(Lang.T("select.label.building"), Tips.Of("select.building"));
        SameButton = Act(Lang.T("select.label.same"), Tips.Of("select.same"));
        InvertButton = Act(Lang.T("select.label.invert"), Tips.Of("select.invert"));
        DeleteButton.Click += (_, _) => tool.Delete();
        ReplaceButton.Click += (_, _) =>
        {
            if (ReplaceBox.SelectedIndex >= 0 && ReplaceBox.SelectedIndex < ReplaceKinds.Count)
            {
                ReplaceAsked?.Invoke(ReplaceKinds[ReplaceBox.SelectedIndex]);
            }
        };
        ReplacePickButton.Tip("select.pick");
        ReplaceBox.Tip("select.to");
        ReplaceButton.Tip("select.replace");
        ApplyButton.Tip("select.apply");
        InspectButton.Tip("select.inspect");
        ClaimButton.Tip("select.claim");
        SavedBox.Tip("select.saved");
        ForgetButton.Tip("select.forget");
        XBox.Tip("select.x");
        YBox.Tip("select.y");
        ZBox.Tip("select.z");
        TurnBox.Tip("select.turn");
        ReplacePickButton.Click += (_, _) => PickKindAsked?.Invoke("Replace", UsePicked);
        BuildingButton.Click += (_, _) => tool.WholeBuilding();
        SameButton.Click += (_, _) => tool.SameKind();
        InvertButton.Click += (_, _) => tool.Invert();
        GroundBox = new CheckBox { Content = Lang.T("select.label.ground"), IsChecked = tool.OnGround, FontSize = 12 }.Tip("select.ground");
        GroundBox.IsCheckedChanged += (_, _) => tool.OnGround = GroundBox.IsChecked == true;
        SnapBox = new CheckBox { Content = Lang.T("select.label.snap"), IsChecked = tool.SnapToPieces, FontSize = 12 }.Tip("select.snap");
        SnapBox.IsCheckedChanged += (_, _) => tool.SnapToPieces = SnapBox.IsChecked == true;

        KeepButton.Tip("select.keep");
        LoadButton.Tip("select.load");
        AddSavedButton.Tip("select.add");
        KeepButton.Click += async (_, _) => await Keep();
        LoadButton.Click += (_, _) => UseSaved(false);
        AddSavedButton.Click += (_, _) => UseSaved(true);
        ForgetButton.Click += async (_, _) => await Forget();
        SavedBox.SelectionChanged += (_, _) => _savedRow!.IsVisible = SavedBox.SelectedIndex >= 0;
        _savedRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, IsVisible = false, Children = { LoadButton, AddSavedButton, ForgetButton } };

        Control Cell(string label, Control box)
        {
            var l = new TextBlock { Text = label, Width = 34, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Tips.Label(l, box);
            return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { l, box } };
        }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 6, RowSpacing = 4 };
        void Put(Control c, int row, int col)
        {
            Grid.SetRow(c, row);
            Grid.SetColumn(c, col);
            grid.Children.Add(c);
        }
        Put(Cell(Lang.T("select.label.x"), XBox), 0, 0);
        Put(Cell(Lang.T("select.label.y"), YBox), 0, 1);
        Put(Cell(Lang.T("select.label.z"), ZBox), 1, 0);
        Put(Cell(Lang.T("brush.label.turn"), TurnBox), 1, 1);
        ByButton.Tip("select.by");
        ByButton.Click += (_, _) => SetBy(!_by);
        ApplyButton.Click += (_, _) => Apply();
        _exact = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = Lang.T("select.head.exact"), FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 6, 0, 0) },
                grid,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, ByButton } },
                _info,
            },
        };
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 290,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("rail.label.select"), FontSize = 14, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = Lang.T("select.hint.click"), FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
                    new WrapPanel { Children = { DeleteButton, BuildingButton, SameButton, InvertButton, InspectButton, ClaimButton }, ItemSpacing = 4, LineSpacing = 4 },
                    new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6, Children = { new TextBlock { Text = Lang.T("select.label.replaceWith"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Ui.Col(ReplaceBox, 1), Ui.Col(ReplacePickButton, 2) } },
                    ReplaceButton,
                    GroundBox,
                    SnapBox,
                    new TextBlock { Text = Lang.T("select.head.saved"), FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 6, 0, 0) },
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { SavedBox, Ui.Col(KeepButton, 1) } },
                    _savedRow,
                    _exact,
                    new TextBlock
                    {
                        Text = Lang.T("select.hint.keys"),
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
        Refresh();
    }

    /// <summary>The eyedropper's kind into "Replace with", when the game can make it.</summary>
    internal void UsePicked(int prefab)
    {
        int i = ReplaceKinds.IndexOf(prefab);
        string name = ReplaceBox.ItemsSource is IList<string> names && i >= 0 && i < names.Count ? names[i] : NameOf?.Invoke(prefab) ?? prefab.ToString();
        if (i < 0)
        {
            Message?.Invoke(Said.Of("select.msg.cannotPlace", Said.Raw(name)));
            return;
        }
        ReplaceBox.SelectedIndex = i;
        Message?.Invoke(Said.Of("select.msg.replaceWith", Said.Raw(name)));
    }

    /// <summary>The open world's saved selections into the list (the chosen one kept when it is still there).</summary>
    internal void FillSaved()
    {
        string? world = WorldName();
        int keep = SavedBox.SelectedIndex;
        _saved = world == null ? [] : SavedSelections.For(world);
        SavedBox.ItemsSource = _saved.Select(s => Lang.T("select.savedEntry", s.Name, s.Items.Count)).ToList();
        SavedBox.PlaceholderText = _saved.Count > 0 ? Lang.T("select.savedSome") : Lang.T("select.savedNone");
        SavedBox.SelectedIndex = keep >= 0 && keep < _saved.Count ? keep : -1;
        _savedRow.IsVisible = SavedBox.SelectedIndex >= 0;
    }

    /// <summary>Keeps the selected objects under a name asked for (one of that name is replaced).</summary>
    internal async Task Keep()
    {
        string? world = WorldName();
        var things = Things();
        var items = things == null ? new List<SavedSelections.Item>() : [.. SelectedIds().Where(i => i >= 0 && i < things.Count && !things[i].Gone).Select(i => new SavedSelections.Item(things[i].Prefab, things[i].Position.X, things[i].Position.Y, things[i].Position.Z))];
        if (world == null || items.Count == 0)
        {
            Message?.Invoke(Said.Of("select.msg.keepFirst"));
            return;
        }
        string? name = (await AskName(Lang.T("select.savedName", SavedSelections.For(world).Count + 1)))?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        if (!SavedSelections.Keep(world, name, items))
        {
            Message?.Invoke(Said.Of("select.msg.keepFailed"));
            return;
        }
        FillSaved();
        SavedBox.SelectedIndex = _saved.FindIndex(s => s.Name == name);
        Message?.Invoke(Said.Of("select.msg.kept", name, items.Count));
    }

    /// <summary>
    /// Selects the chosen saved selection's objects (with <c>add</c>, as well as what is selected), and says how many
    /// were not found.
    /// </summary>
    internal void UseSaved(bool add)
    {
        int i = SavedBox.SelectedIndex;
        var things = Things();
        if (i < 0 || i >= _saved.Count || things == null)
        {
            return;
        }
        var s = _saved[i];
        var (ids, missing) = SavedSelections.Find(s, things);
        SelectIds(add ? [.. SelectedIds().Concat(ids).Distinct()] : ids);
        Message?.Invoke(missing > 0 ? Said.Of("select.msg.savedMissing", ids.Count, s.Name, missing) : Said.Of("select.msg.saved", ids.Count, s.Name));
    }

    /// <summary>Forgets the chosen saved selection, once confirmed.</summary>
    internal async Task Forget()
    {
        int i = SavedBox.SelectedIndex;
        string? world = WorldName();
        if (world == null || i < 0 || i >= _saved.Count || !await Confirm(Lang.T("select.ask.forget.text", _saved[i].Name)))
        {
            return;
        }
        if (!SavedSelections.Forget(world, _saved[i].Name))
        {
            Message?.Invoke(Said.Of("select.msg.forgetFailed"));
            return;
        }
        SavedBox.SelectedIndex = -1;
        FillSaved();
    }

    /// <summary>The name of a kind (the window's models), for messages.</summary>
    public Func<int, string?>? NameOf { get; set; }

    /// <summary>Switches Move by: on, the boxes start at 0; off, they show where the selection is.</summary>
    private void SetBy(bool by)
    {
        _by = by;
        ApplyButton.Content = by ? Lang.T("select.label.moveByThese") : Lang.T("select.label.moveThere");
        ByButton.Classes.Set("on", by);
        if (by)
        {
            XBox.Value = YBox.Value = ZBox.Value = TurnBox.Value = 0;
            _info.Text = Lang.T("select.hint.by");
        }
        else
        {
            Refresh();
        }
    }

    /// <summary>
    /// Moves the selection to (or by) the boxes' values, then back to showing where it is. Move there moves only along
    /// the axes whose box was changed, from the unrounded place the boxes were filled from: a box left as Refresh put
    /// it (rounded to a centimetre or a tenth of a degree) is not a move.
    /// </summary>
    private void Apply()
    {
        static float V(NumericUpDown b) => (float)(b.Value ?? 0);
        if (_by)
        {
            // An empty or zero box is no move along that axis.
            static float? By(NumericUpDown b) => b.Value is { } v && v != 0 ? (float)v : null;
            _tool.PlaceAt(By(XBox), By(YBox), By(ZBox), By(TurnBox), by: true);
            SetBy(false);
            return;
        }
        if (_shown is not { } was)
        {
            return;
        }
        // Which boxes were changed, and to what, taken before a move in progress (a turn or lift from the keys, not
        // committed yet) is committed: the boxes were filled before it, and the place to move from is after it.
        static bool Changed(NumericUpDown b, float from, int digits) => b.Value != (decimal)MathF.Round(from, digits);
        var (x, y, z, turn) = (V(XBox), V(YBox), V(ZBox), V(TurnBox));
        var (cx, cy, cz, ct) = (Changed(XBox, was.X, 2), Changed(YBox, was.Y, 2), Changed(ZBox, was.Z, 2), Changed(TurnBox, was.Turn, 1));
        if (_tool.Moving)
        {
            _tool.Commit();
            Refresh();
        }
        if (_shown is { } at)
        {
            _tool.PlaceAt(cx ? x - at.X : null, cy ? y - at.Y : null, cz ? z - at.Z : null, ct ? turn - at.Turn : null, by: true);
        }
        SetBy(false);
    }

    /// <summary>The selection changed or moved: the exact place shows where it is now.</summary>
    public void Refresh()
    {
        var w = _tool.Where();
        _exact.IsVisible = w != null;
        DeleteButton.IsEnabled = SameButton.IsEnabled = w != null;
        InspectButton.IsEnabled = w is { Count: 1 };
        ClaimButton.IsEnabled = w != null;
        if (w is not { } at)
        {
            _shown = null;
            return;
        }
        if (_by)
        {
            return;
        }
        _shown = (at.X, at.Y, at.Z, at.Turn);
        XBox.Value = (decimal)MathF.Round(at.X, 2);
        YBox.Value = (decimal)MathF.Round(at.Y, 2);
        ZBox.Value = (decimal)MathF.Round(at.Z, 2);
        TurnBox.Value = (decimal)MathF.Round(at.Turn, 1);
        _info.Text = at.Count > 1
            ? Lang.T("select.hint.middle", at.Count)
            : Lang.T("select.hint.one");
    }
}
