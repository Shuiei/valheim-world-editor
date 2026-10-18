using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world map page, like the web editor's (index.html): the world drawn like the game's map, what is waiting to be
/// saved (Save, or Apply live, and Discard), the map's options, search across the world, zones picked by filter to
/// reset, the edited zones, and the spot picked with a click, opened in the 3D editor with the size chosen.
/// </summary>
public sealed partial class MapPage : IDisposable
{
    /// <summary>
    /// The page: the map with the side panel, the cursor readout and the status over it.
    /// </summary>
    public Control View { get; }
    /// <summary>
    /// The map itself.
    /// </summary>
    public MapView Map { get; } = new();
    /// <summary>
    /// The world shown, or null before one is.
    /// </summary>
    private WorldSession? _session;
    /// <summary>
    /// Where a newly shown world's map looks first (the place remembered from the last run; null: the whole world).
    /// </summary>
    internal Func<(float X, float Z, float Mpp)?> StartView { get; set; } = () => null;

    /// <summary>
    /// The Worlds button: back to the world list.
    /// </summary>
    public event Action? BackToWorlds;
    /// <summary>
    /// Edit in 3D: the zone and the size in zones (Target: an object found to select there).
    /// </summary>
    public event Action<int, int, int>? EditRequested;
    /// <summary>
    /// Save, apply live, discard, and reload from the game: the window does them (saving and discarding ask first).
    /// </summary>
    public event Action? SaveRequested, DiscardRequested, ReloadRequested;
    /// <summary>
    /// Asks the user a yes/no question; the window sets it (tests answer yes).
    /// </summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>
    /// Tells the user something; the window sets it.
    /// </summary>
    internal Func<string, Task> Tell { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// The picked zone, or null when none is.
    /// </summary>
    public (int X, int Z)? Spot { get; private set; }
    /// <summary>
    /// An object found by the search: selected (and, for items and texts, inspected) once the area opens.
    /// </summary>
    public (int Id, bool Inspect)? Target { get; private set; }
    /// <summary>
    /// The area to open, in zones a side.
    /// </summary>
    public int Size => MainWindow.AreaSizes[Math.Max(0, SizeBox.SelectedIndex)];
    /// <summary>
    /// Live: asks the game for the players online every five seconds.
    /// </summary>
    private readonly DispatcherTimer _players = new() { Interval = TimeSpan.FromSeconds(5) };
    /// <summary>
    /// The players' name labels over the map.
    /// </summary>
    private readonly Canvas _labels = new() { IsHitTestVisible = false };
    /// <summary>
    /// The side panel's background and border brushes.
    /// </summary>
    private static readonly IBrush PanelBg = Ui.Panel, Line = Ui.Line;

    /// <summary>
    /// A section heading.
    /// </summary>
    private static TextBlock H2(string t) => Ui.Heading(t, 4);
    /// <summary>
    /// Builds the page; a world is shown with <c>Show</c>.
    /// </summary>
    public MapPage()
    {
        var worlds = new Button { Content = Icons.With("back", Lang.T("map.label.worlds")), FontSize = 12.5 }.Classed("ghost");
        worlds.Tip("map.worlds");
        SaveButton.Tip("map.save");
        DiscardButton.Tip("map.discard");
        GridBox.Tip("map.grid");
        EditedBox.Tip("map.edited");
        PaintBox.Tip("map.paint");
        CloudsBox.Tip("map.clouds");
        EditButton.Tip("map.edit");
        SearchWhat.Tip("map.searchWhat");
        DistBox.Tip("map.dist");
        RMinBox.Tip("map.rmin");
        RMaxBox.Tip("map.rmax");
        WireButtons(worlds);
        WireMap();
        _liveBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { LiveText, ReloadButton } };
        _changesBox = new StackPanel { Spacing = 6, Children = { Pending, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { SaveButton, DiscardButton } } } };
        var search = BuildSearch();
        var zones = BuildZones();
        // The spot picked, and its edits up close.
        _pickBox = new StackPanel { Spacing = 6, IsVisible = false, Children = { PickTitle, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { EditButton, SizeBox } } } };
        _editedSection = BuildEdited();
        var side = BuildSide(worlds, search, zones);
        var surface = new Border { Background = Brushes.Transparent };
        Map.Attach(surface);
        var cursor = new Border { Background = PanelBg, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = Ui.Pad, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Child = Cursor };
        var status = new Border
        {
            Background = PanelBg,
            BorderBrush = Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _status,
            IsVisible = false
        };
        Map.Status += t => status.IsVisible = !string.IsNullOrEmpty(t);
        cursor.IsVisible = false;
        Cursor.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) { cursor.IsVisible = !string.IsNullOrEmpty(Cursor.Text); } };
        View = new Grid { Background = new SolidColorBrush(Color.FromRgb(20, 23, 28)), Children = { Map, surface, _labels, side, cursor, status } };
        _players.Tick += async (_, _) => await PollPlayers();
    }
}
