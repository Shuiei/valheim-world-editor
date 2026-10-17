using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's buttons, its summary, the plan's canvas and the level buttons, with the state the panel keeps.
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>Puts a random seed in.</summary>
    internal Button Randomize { get; } = new() { Content = Lang.T("common.randomize"), FontSize = 12 };
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
}
