using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The 3D editor's world controls, as the web editor had them: the area moved by one zone (or
// following the view), Discard and (live) Reload and Auto apply in the top bar, the players of a live
// world in the view with their names and Go to, and a note about the locations of the area.
public sealed partial class MainWindow
{
    /// <summary>Moves the area one zone west.</summary>
    internal Button AreaWest { get; } = NavButton("◀", "West");
    /// <summary>Moves the area one zone north.</summary>
    internal Button AreaNorth { get; } = NavButton("▲", "North");
    /// <summary>Moves the area one zone south.</summary>
    internal Button AreaSouth { get; } = NavButton("▼", "South");
    /// <summary>Moves the area one zone east.</summary>
    internal Button AreaEast { get; } = NavButton("▶", "East");
    /// <summary>Turns Follow on or off (the area moves by itself near its edge).</summary>
    internal Button FollowButton { get; } = new Button { Content = Lang.T("top.label.follow"), FontSize = 12.5 }.Classed("ghost");
    /// <summary>Discard: undoes what is not saved or applied yet; enabled while something is.</summary>
    internal Button DiscardButton { get; } = new Button { Content = Lang.T("common.discard"), FontSize = 12.5, IsEnabled = false }.Classed("ghost");
    /// <summary>Live: loads the world again from the game; shown for live worlds only.</summary>
    internal Button ReloadButton { get; } = new Button { Content = Icons.With("reload", Lang.T("top.label.reload")), FontSize = 12.5, IsVisible = false }.Classed("ghost");
    /// <summary>Live: Auto apply, every change sent to the game at once; shown for live worlds only.</summary>
    internal CheckBox AutoApplyBox { get; } = new CheckBox { Content = Lang.T("top.label.auto"), FontSize = 12, IsVisible = false, VerticalAlignment = VerticalAlignment.Center }.Classed("switch");
    /// <summary>
    /// The top bar's area controls: the arrows, the size and Follow; shown when the area belongs to a world.
    /// </summary>
    internal StackPanel AreaNav { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>The area's size, changed on the fly (around the same middle zone).</summary>
    internal ComboBox AreaSizeBox { get; } = new() { ItemsSource = AreaSizes.Select(n => $"{n} × {n}").ToArray(), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) };
    /// <summary>The sizes the area can have, in zones across.</summary>
    internal static readonly int[] AreaSizes = [3, 5, 7, 9, 13, 17];
    /// <summary>
    /// The most zones Follow grows the area to, each way (about 1 km, the largest area size); there it stops growing
    /// that way. It never leaves zones behind.
    /// </summary>
    internal const int MaxFollowZones = 17;
    /// <summary>True while <c>ShowAreaSize</c> sets the size box, so its change is not taken for the user's.</summary>
    private bool _fillingSize;
    /// <summary>The note about the area's locations.</summary>
    internal TextBlock LocationText { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#f3d29b")) };
    /// <summary>
    /// The card holding <c>LocationText</c>, over the view's lower left; made in <c>SetUpEditorWorld</c>.
    /// </summary>
    internal Border LocationNote { get; private set; } = null!;
    /// <summary>The live players to go to, by name.</summary>
    internal ComboBox PlayerBox { get; } = new() { FontSize = 12, MinWidth = 130 };
    /// <summary>Goes to the player chosen in <c>PlayerBox</c>.</summary>
    internal Button GoPlayerButton { get; } = new Button { Content = Lang.T("live.label.go"), FontSize = 12 };
    /// <summary>The View panel's players section; shown for live worlds only.</summary>
    internal StackPanel PlayersRow { get; } = new() { Spacing = 2, IsVisible = false };
    /// <summary>The players' names over the view (<c>PlaceLabels</c>).</summary>
    internal Canvas PlayerLabels { get; } = new() { IsHitTestVisible = false };
    /// <summary>The live players: name and world position, as the game last said.</summary>
    internal IReadOnlyList<(string Name, Vector3 World)> LivePlayers { get; private set; } = [];
    /// <summary>Live: asks the game for its players every 2 s.</summary>
    private readonly DispatcherTimer _playersTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    /// <summary>Live: places the players' names over the view ten times a second, following the camera.</summary>
    private readonly DispatcherTimer _labelsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    /// <summary>Checks Follow (<c>CheckFollow</c>) while the window is open.</summary>
    private readonly DispatcherTimer _followTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    /// <summary>Live: the game asked every 2 s what changed in the zones shown (WorldSession.FollowGame).</summary>
    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    /// <summary>Why Follow last waited, so the same reason is not said again at every tick.</summary>
    private Said _followSaid = Said.Empty;
    /// <summary>True while Follow opens the grown area, so the ticks in between do not open it again.</summary>
    private bool _followBusy;
    /// <summary>The editor's settings, loaded when the window is made.</summary>
    internal AppSettings Settings { get; } = AppSettings.Load();
    /// <summary>Writes the settings (Auto, Follow); tests keep them in memory, so no other window reads them.</summary>
    internal Action<AppSettings> SaveSettings { get; set; } = s => s.Save();
    /// <summary>An apply is under way; another was asked for meanwhile (run when this one ends).</summary>
    private bool _applying, _applyAgain;

    /// <summary>An arrow button of the area controls; <paramref name="where"/> names its tip (top.west…).</summary>
    private static Button NavButton(string arrow, string where)
    {
        var b = new Button { Content = arrow, FontSize = 11, Padding = new Thickness(6, 3) }.Classed("ghost");
        b.Tip($"top.{where.ToLowerInvariant()}");
        return b;
    }
}
