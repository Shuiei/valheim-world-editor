using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Dungeon tool's panel: which dungeon of the area, going inside and cutting its roofs away (and back
/// down to its entrance), the rooms it can take, and the changes (turn, delete, close the open ends).
/// </summary>
public sealed class DungeonPanel
{
    /// <summary>The panel itself.</summary>
    public Control Card { get; }
    /// <summary>Generate a dungeon, at the top of the panel (open when the area has no dungeon).</summary>
    internal DungeonGenPanel Gen { get; } = new();
    /// <summary>The generator's expander.</summary>
    private readonly Expander _generate;
    /// <summary>The generator's Go to the dungeon and Go to the entrance, under its expander: shown once a dungeon is placed.</summary>
    private readonly StackPanel _go;
    /// <summary>The view: its dungeon tool, camera and roof cut.</summary>
    private readonly GlView _view;
    /// <summary>The view's Dungeon tool the panel drives.</summary>
    private DungeonTool Tool => _view.Dungeon;
    /// <summary>Which of the area's dungeons is edited.</summary>
    internal ComboBox DungeonBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
    /// <summary>The camera into the dungeon (see <see cref="Inside"/>).</summary>
    internal Button GoInside { get; } = new() { Content = "Go inside", FontSize = 12 };
    /// <summary>The camera back down to the dungeon's entrance on the ground (see <see cref="Outside"/>).</summary>
    internal Button GoOutside { get; } = new() { Content = "Go to entrance", FontSize = 12 };
    /// <summary>Build with the hammer's pieces in this dungeon (<see cref="BuildAsked"/>).</summary>
    internal Button BuildHere { get; } = new() { Content = "Build here", FontSize = 12 };
    /// <summary>Build here was clicked: the window switches to building in the dungeon.</summary>
    public event Action? BuildAsked;
    /// <summary>The roof cut, 0 to 30 m above the floor of the dungeon's entrance; the right end (<see cref="CutOff"/>) is off.</summary>
    internal Slider CutSlider { get; } = new() { Minimum = 0, Maximum = CutOff, Value = CutOff, SmallChange = 1, TickFrequency = 1, IsSnapToTickEnabled = true };
    /// <summary>The cut slider's right end: no cut, the roofs shown.</summary>
    internal const double CutOff = 30;
    /// <summary>The cut slider's label.</summary>
    private readonly TextBlock _cutText = new() { FontSize = 12 };
    /// <summary>Filters the rooms by name.</summary>
    internal TextBox Filter { get; } = new() { PlaceholderText = "Find a room…", FontSize = 12 };
    /// <summary>The rooms the dungeon can take; the one chosen is added at the next green opening clicked.</summary>
    internal ListBox Rooms { get; } = new() { MaxHeight = 260, FontSize = 12 };
    /// <summary>Turns the room chosen (R).</summary>
    internal Button TurnButton { get; } = new() { Content = "Turn (R)", FontSize = 12 };
    /// <summary>Closes the dungeon's open ends; enabled while it has some.</summary>
    internal Button CloseEnds { get; } = new() { Content = "Close open ends", FontSize = 12 };
    /// <summary>Deletes the selected room (Del); enabled while one is selected.</summary>
    internal Button DeleteButton { get; } = new() { Content = "Delete room (Del)", FontSize = 12 };
    /// <summary>Rooms, open ends, overlaps and problems of the dungeon, and of the room about to be added.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>Rooms added with what the game puts in them.</summary>
    internal CheckBox Contents { get; } = new() { Content = "With what the game puts in them", IsChecked = true, FontSize = 12 };
    /// <summary>Rooms added with doors where the game might put them.</summary>
    internal CheckBox Doors { get; } = new() { Content = "Doors where the game might put them", IsChecked = true, FontSize = 12 };
    /// <summary>Shown when the area has no dungeon.</summary>
    private readonly TextBlock _empty = new()
    {
        FontSize = 12,
        Foreground = Ui.Muted,
        TextWrapping = TextWrapping.Wrap,
        Text = "No dungeon in this area. Open the area over a Frost Cave, crypt or other dungeon entrance: the game keeps its rooms 5000 m above it."
    };
    /// <summary>The dungeon rows, shown when the area has a dungeon.</summary>
    private readonly StackPanel _body;
    /// <summary>The dungeons on <see cref="DungeonBox"/>, in its order.</summary>
    private List<DungeonRooms.Dungeon> _shown = [];
    /// <summary>While the lists are being filled: their selection changes are not the user's.</summary>
    private bool _filling;

    /// <summary>Builds the panel for the view's Dungeon tool.</summary>
    /// <param name="view">The 3D view.</param>
    public DungeonPanel(GlView view)
    {
        _view = view;
        DungeonBox.Tip("dungeon.which");
        GoInside.Tip("dungeon.inside");
        GoOutside.Tip("dungeon.outside");
        BuildHere.Tip("dungeon.build");
        BuildHere.Click += (_, _) => BuildAsked?.Invoke();
        CutSlider.Tip("dungeon.cut");
        Filter.Tip("dungeon.find");
        Rooms.Tip("dungeon.rooms");
        TurnButton.Tip("dungeon.turn");
        CloseEnds.Tip("dungeon.close");
        DeleteButton.Tip("dungeon.delete");
        Contents.Tip("dungeon.contents");
        Contents.IsCheckedChanged += (_, _) => Tool.WithContents = Contents.IsChecked == true;
        Doors.Tip("dungeon.doors");
        Doors.IsCheckedChanged += (_, _) => Tool.WithDoors = Doors.IsChecked == true;
        DungeonBox.SelectionChanged += (_, _) =>
        {
            if (!_filling && DungeonBox.SelectedIndex >= 0 && DungeonBox.SelectedIndex < _shown.Count)
            {
                Tool.Open(_shown[DungeonBox.SelectedIndex]);
                FillRooms();
            }
        };
        GoInside.Click += (_, _) => Inside();
        GoOutside.Click += (_, _) => Outside();
        CutSlider.ValueChanged += (_, e) => SetCut(e.NewValue);
        Filter.TextChanged += (_, _) => FillRooms();
        Rooms.SelectionChanged += (_, _) =>
        {
            if (!_filling)
            {
                Tool.Choose((Rooms.SelectedItem as ListBoxItem)?.Tag as string);
            }
        };
        TurnButton.Click += (_, _) => Tool.Turn();
        CloseEnds.Click += (_, _) => Tool.CloseOpenEnds();
        DeleteButton.Click += (_, _) => Tool.Delete();
        Tool.Changed += Refresh;
        _body = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                DungeonBox,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { GoInside, GoOutside, BuildHere } },
                new StackPanel { Spacing = 2, Children = { _cutText, CutSlider } },
                new TextBlock { Text = "ROOMS", FontSize = 11, Foreground = Ui.Muted, FontWeight = FontWeight.SemiBold },
                Filter, Rooms, Contents, Doors,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { TurnButton, DeleteButton } },
                CloseEnds, Info,
            },
        };
        _generate = new Expander
        {
            Header = new TextBlock { Text = "Generate a dungeon", FontSize = 12, FontWeight = FontWeight.SemiBold },
            Content = Gen.View,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsExpanded = true,
        };
        _generate.Tip("dungeon.gen");
        _go = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, IsVisible = false, Children = { Gen.ToDungeon, Gen.ToEntrance } };
        Gen.PlacedChanged += yes => _go.IsVisible = yes;
        Card = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ScrollViewer
            {
                MaxHeight = 820,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = new StackPanel
                {
                    Width = 280,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = "Dungeon", FontSize = 14, FontWeight = FontWeight.SemiBold },
                        _generate, _go,
                        new TextBlock { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                            Text = "Editing this area's dungeons: pick a room below, then click a green opening to add it there. Click a room to select it, an orange or blue square (where rooms meet) to put a door there or take it away." },
                        _empty, _body,
                    },
                },
            },
        };
        SetCut(CutOff);
    }

    /// <summary>The area's dungeons again (on entering the tool, and after an edit or undo).</summary>
    public void Reload()
    {
        _filling = true;
        _shown = [.. Tool.All.Where(d => d.Kind.Interior || d.Rooms.Count > 0)];
        DungeonBox.ItemsSource = _shown.Select(DungeonTool.Describe).ToList();
        int i = Tool.Current is { } at ? _shown.FindIndex(d => System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f) : -1;
        if (i < 0 && _shown.Count > 0)
        {
            i = 0;
            Tool.Open(_shown[0]);
        }
        DungeonBox.SelectedIndex = i;
        _filling = false;
        _empty.IsVisible = _shown.Count == 0;
        _body.IsVisible = _shown.Count > 0;
        _generate.IsExpanded = _shown.Count == 0;
        // Made before the game's data was read (nothing to place): made again now.
        if (Gen.Failed)
        {
            Gen.Remake();
        }
        FillRooms();
        Refresh();
        // Back from Build here or another tool: the cut the slider shows.
        SetCut(CutSlider.Value);
    }

    /// <summary>Lists the rooms the dungeon can take that match the filter, the one chosen selected.</summary>
    private void FillRooms()
    {
        _filling = true;
        string f = Filter.Text?.Trim() ?? "";
        var items = Tool.Palette.Where(r => f.Length == 0 || r.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
            .Select(r => new ListBoxItem { Content = r.Name + (r.EndCap ? "  (end cap)" : r.Entrance ? "  (entrance)" : ""), Tag = r.Name }).ToList();
        Rooms.ItemsSource = items;
        Rooms.SelectedItem = items.FirstOrDefault(i => (string?)i.Tag == Tool.Chosen);
        _filling = false;
    }

    /// <summary>Fills <see cref="Info"/> and enables the buttons that apply.</summary>
    private void Refresh()
    {
        if (Tool.Dungeon is not { } d)
        {
            Info.Text = "";
            return;
        }
        int open = Tool.FreeEnds.Count;
        int overlaps = Tool.OverlappingPairs;
        string text = $"{d.Rooms.Count} rooms, {open} open end(s).";
        if (overlaps > 0)
        {
            text += $" {overlaps} overlapping pair(s).";
        }
        if (d.Problem != null)
        {
            text += " " + d.Problem;
        }
        if (Tool.Preview is { } p && Tool.Problem(p) is string problem)
        {
            text += " This one: " + problem;
        }
        Info.Text = text;
        DeleteButton.IsEnabled = Tool.Selected != null;
        CloseEnds.IsEnabled = open > 0;
    }

    /// <summary>
    /// Makes the dungeon whose object stands at <paramref name="at"/> the one edited (listed first when it was not
    /// listed yet); false when the area has none there.
    /// </summary>
    internal bool Select(System.Numerics.Vector3 at)
    {
        if (!_shown.Any(d => System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f))
        {
            Reload();
        }
        int i = _shown.FindIndex(d => System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f);
        if (i < 0)
        {
            return false;
        }
        DungeonBox.SelectedIndex = i;
        if (Tool.Dungeon is not { } d || System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) >= 0.01f)
        {
            Tool.Open(_shown[i]);
            FillRooms();
        }
        return true;
    }

    /// <summary>
    /// The camera back down to the dungeon's entrance on the ground: the dungeon location in its zone (the game lays a
    /// dungeon out over its entrance's zone), else the ground straight under it. Nothing cut away.
    /// </summary>
    internal void Outside()
    {
        if (Tool.Dungeon is not { } d || _view.Scene is not { } s)
        {
            return;
        }
        var p = d.Thing.Position;
        var zone = DungeonEntrances.ZoneCentre(p);
        var entrance = LocationParts.Of(s).FirstOrDefault(l => DungeonGen.DungeonLocations.Contains(l.Location) && DungeonEntrances.ZoneCentre(l.Position) == zone);
        float x = entrance.Location != 0 ? entrance.Position.X : p.X, z = entrance.Location != 0 ? entrance.Position.Z : p.Z;
        ShowRoofs();
        _view.Orbit(x, z, _view.Camera.Yaw * 180f / MathF.PI, 55, 40);
    }

    /// <summary>The cut off: the roofs shown (the slider at its right end).</summary>
    internal void ShowRoofs()
    {
        CutSlider.Value = CutOff;
        SetCut(CutOff);
    }

    /// <summary>
    /// The camera to the middle of the dungeon's rooms (its dungeon object when it has none), looking down into it;
    /// the cut on (4 m when off).
    /// </summary>
    internal void Inside()
    {
        if (Tool.Dungeon is not { } d)
        {
            return;
        }
        var p = d.Rooms.Count > 0 ? d.Rooms.Aggregate(System.Numerics.Vector3.Zero, (a, r) => a + r.Position) / d.Rooms.Count : d.Thing.Position;
        _view.Orbit(p.X, p.Z, 30, 55, 70, p.Y);
        if (CutSlider.Value >= CutOff)
        {
            CutSlider.Value = 4;
        }
        SetCut(CutSlider.Value);
    }

    /// <summary>
    /// Cuts the view's roofs away above a height: from about the floor of the dungeon's entrance (3 m under the
    /// dungeon object); <see cref="CutOff"/> metres: off.
    /// </summary>
    private void SetCut(double metres)
    {
        // From about the floor of the dungeon's entrance (3 m under the dungeon object, as in the game's caves).
        float? y = metres < CutOff && Tool.Dungeon is { } d ? d.Thing.Position.Y - 3 + (float)metres : null;
        _view.CutY = y;
        _cutText.Text = metres < CutOff ? $"Cut {metres:0} m above the floor" : "Cut: off (roofs shown)";
    }

    /// <summary>Leaving the tool: the cut off.</summary>
    public void Leave() => _view.CutY = null;
}
