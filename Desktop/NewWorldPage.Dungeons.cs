using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The dungeon switches: which kinds show on the map, the seed's dungeons found, and the dots they make.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// One switch a kind of dungeon, by name (off at first: they are looked for only when one is on, a few seconds).
    /// </summary>
    internal Dictionary<string, CheckBox> ShowDungeons { get; } = SeedPreview.Dungeons.ToDictionary(d => d.Name, d => DungeonToggle(d.Name));
    /// <summary>
    /// The shown seed's dungeons (null: not looked for yet).
    /// </summary>
    internal List<SeedPreview.Landmark>? DungeonMarks { get; private set; }
    /// <summary>
    /// The dungeons of the seeds seen, by seed (cleared past 20).
    /// </summary>
    private readonly Dictionary<string, List<SeedPreview.Landmark>> _dungeonsSeen = [];
    /// <summary>
    /// The Dungeons list beside the map.
    /// </summary>
    private readonly StackPanel _dungeonList = new() { Spacing = 2 };
    /// <summary>
    /// A dungeon kind was switched on while they are found: the loading sign stays until they are.
    /// </summary>
    private bool _dungeonWait;
    /// <summary>
    /// Why finding the dungeons failed, or null.
    /// </summary>
    private string? _dungeonError;
    /// <summary>Whether the shown seed's dungeons are being looked for.</summary>
    internal bool FindingDungeons => _dungeons != null;

    /// <summary>
    /// Stops looking for dungeons.
    /// </summary>
    private void StopDungeons()
    {
        _dungeonWait = false;
        // Cancelled only: its work may still read the token; it is disposed when that work ends.
        _dungeons?.Cancel();
        _dungeons = null;
    }
    /// <summary>
    /// The dungeons the switches show, as dots on the map.
    /// </summary>
    private void PlaceDots() => Map.Dots = DungeonMarks == null
        ? Array.Empty<(float, float, IBrush)>()
        : DungeonMarks.Where(m => ShowDungeons[m.Name].IsChecked == true).Select(m => (m.X, m.Z, (IBrush)DungeonFill(m.Name))).ToList();
    /// <summary>
    /// A dungeon kind's square mark.
    /// </summary>
    private static Border DungeonMark(string name) => new() { Width = 8, Height = 8, Background = DungeonFill(name), BorderBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(20, 20, 24)), BorderThickness = new Thickness(1) };

    /// <summary>
    /// A switch for one dungeon kind, off at first.
    /// </summary>
    private static CheckBox DungeonToggle(string name) => new CheckBox
    {
        IsChecked = false,
        FontSize = 12.5,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { DungeonMark(name), new TextBlock { Text = DungeonName(name), VerticalAlignment = VerticalAlignment.Center } } },
    }.Classed("switch");
}
