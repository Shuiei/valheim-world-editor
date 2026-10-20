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
public sealed partial class SelectPanel
{
    /// <summary>The panel itself, shown next to the rail while the Select tool is on.</summary>
    public Control Card { get; }
    /// <summary>The Select tool the panel drives.</summary>
    private readonly SelectTool _tool;
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

    /// <summary>Select, Add and Forget, shown while a saved selection is chosen.</summary>
    private readonly StackPanel _savedRow;
    /// <summary>The open world's saved selections, in the list's order.</summary>
    private List<SavedSelections.Saved> _saved = [];

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
        AllButton = Act(Lang.T("select.label.all"), Tips.Of("select.all"));
        HideButton = Act(Lang.T("select.label.hide"), Tips.Of("select.hide"));
        UnhideButton = Act(Lang.T("select.label.unhide"), Tips.Of("select.unhide"));
        AllButton.Click += (_, _) => tool.SelectAll();
        HideButton.Click += (_, _) => tool.HideSelected();
        UnhideButton.Click += (_, _) => tool.ShowHidden();
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
        ReplacePickButton.Click += (_, _) => PickKindAsked?.Invoke(Lang.T("common.replace"), UsePicked);
        BuildingButton.Click += (_, _) => tool.WholeBuilding();
        SameButton.Click += (_, _) => tool.SameKind();
        InvertButton.Click += (_, _) => tool.Invert();
        GroundBox = new CheckBox { Content = Lang.T("select.label.ground"), IsChecked = tool.OnGround, FontSize = 12 }.Tip("select.ground");
        GroundBox.IsCheckedChanged += (_, _) => tool.OnGround = GroundBox.IsChecked == true;
        SnapBox = new CheckBox { Content = Lang.T("select.label.snap"), IsChecked = tool.SnapToPieces, FontSize = 12 }.Tip("select.snap");
        SnapBox.IsCheckedChanged += (_, _) => tool.SnapToPieces = SnapBox.IsChecked == true;
        ZoneBox = new ComboBox { ItemsSource = new[] { Lang.T("select.zone.screen"), Lang.T("select.zone.ground") }, SelectedIndex = tool.ZoneOnGround ? 1 : 0, FontSize = 12 }.Tip("select.zone");
        ZoneBox.SelectionChanged += (_, _) => tool.ZoneOnGround = ZoneBox.SelectedIndex == 1;
        ThroughBox = new CheckBox { Content = Lang.T("select.label.through"), IsChecked = tool.SelectThrough, FontSize = 12 }.Tip("select.through");
        ThroughBox.IsCheckedChanged += (_, _) => tool.SelectThrough = ThroughBox.IsChecked == true;

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
                    new WrapPanel { Children = { DeleteButton, AllButton, BuildingButton, SameButton, InvertButton, HideButton, UnhideButton, InspectButton, ClaimButton }, ItemSpacing = 4, LineSpacing = 4 },
                    new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6, Children = { new TextBlock { Text = Lang.T("select.label.replaceWith"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Ui.Col(ReplaceBox, 1), Ui.Col(ReplacePickButton, 2) } },
                    ReplaceButton,
                    new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6, Children = { new TextBlock { Text = Lang.T("select.label.zone"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Ui.Col(ZoneBox, 1) } },
                    ThroughBox,
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
