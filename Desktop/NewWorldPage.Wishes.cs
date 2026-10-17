using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The search's wishes and boxes: what the start and the land should be like, and how many seeds to look at.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// Wish: the start on a large landmass.
    /// </summary>
    internal CheckBox BigLand { get; } = new() { Content = Lang.T("newWorld.label.bigLand"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Wish: land all around the start.
    /// </summary>
    internal CheckBox LandAround { get; } = new() { Content = Lang.T("newWorld.label.landAround"), IsChecked = true, FontSize = 12 };
    /// <summary>
    /// Wish: each biome reachable on foot from the start.
    /// </summary>
    internal CheckBox OnFoot { get; } = new() { Content = Lang.T("newWorld.label.onFoot"), FontSize = 12 };
    /// <summary>
    /// Wish: a Swamp within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Swamp { get; } = Near(1500);
    /// <summary>
    /// Wish: a Mountain within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Mountain { get; } = Near(2000);
    /// <summary>
    /// Wish: Plains within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Plains { get; } = Near(0);
    /// <summary>
    /// Wish: one of Haldor's spots within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Haldor { get; } = Near(0);
    /// <summary>
    /// Wish: one of the Bog Witch's spots within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown BogWitch { get; } = Near(0);
    /// <summary>
    /// Wish: one of Hildir's spots within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Hildir { get; } = Near(0);
    /// <summary>
    /// Wish: the first five bosses' altars within this many metres of the start (0: no wish).
    /// </summary>
    internal NumericUpDown Bosses { get; } = Near(0);
    /// <summary>
    /// How many random seeds to look at: 60, 150 or 400.
    /// </summary>
    internal ComboBox Count { get; } = new() { ItemsSource = new[] { Lang.T("newWorld.count.seeds", 60), Lang.T("newWorld.count.seeds", 150), Lang.T("newWorld.count.seeds", 400) }, SelectedIndex = 1, FontSize = 12 };
    /// <summary>
    /// Starts the search.
    /// </summary>
    internal Button Find { get; } = new Button { Content = Lang.T("newWorld.label.find"), FontSize = 12 }.Classed("primary");
    /// <summary>
    /// Stops the search; the best so far are shown.
    /// </summary>
    internal Button Stop { get; } = new Button { Content = Lang.T("newWorld.label.stop"), FontSize = 12, IsVisible = false }.Classed("primary");
    /// <summary>
    /// The search's progress, 0..1.
    /// </summary>
    internal ProgressBar Progress { get; } = new() { Minimum = 0, Maximum = 1, Height = 6, MinHeight = 6, IsVisible = false };
    /// <summary>
    /// The search's results, as pictures to click.
    /// </summary>
    internal WrapPanel Found { get; } = new() { ItemSpacing = 6, LineSpacing = 6 };

    /// <summary>
    /// The least height of the found seeds' list, in pixels: two rows of pictures.
    /// </summary>
    private const double MinResults = 330;
    /// <summary>
    /// The least space between two found seeds, in pixels.
    /// </summary>
    private const double MinGap = 6;

    /// <summary>
    /// A distance box: 0..8000 m in steps of 250.
    /// </summary>
    private static NumericUpDown Near(double metres) => new()
    {
        Minimum = 0,
        Maximum = 8000,
        Increment = 250,
        Value = (decimal)metres,
        FormatString = "0",
        Width = 110,
        FontSize = 12,
    };

    /// <summary>
    /// The search's wishes as the boxes set them.
    /// </summary>
    internal SeedPreview.Wishes Wishes() => new(
        BigStartLand: BigLand.IsChecked == true, LandAroundStart: LandAround.IsChecked == true,
        MaxSwampDistance: (float)(Swamp.Value ?? 0), MaxMountainDistance: (float)(Mountain.Value ?? 0), MaxPlainsDistance: (float)(Plains.Value ?? 0),
        BiomesOnStartLand: OnFoot.IsChecked == true,
        MaxHaldorDistance: (float)(Haldor.Value ?? 0), MaxBogWitchDistance: (float)(BogWitch.Value ?? 0), MaxHildirDistance: (float)(Hildir.Value ?? 0),
        MaxBossDistance: (float)(Bosses.Value ?? 0));
}
