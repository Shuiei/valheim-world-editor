using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Generate panel's buttons and choices: a style gives its own decay, Place and the plot's buttons hand the dungeon
/// made over, a dungeon of several levels shows a button per level, and one of the game's rooms has no plan to draw.
/// </summary>
public class DungeonGenPanelButtonsTests
{
    /// <summary>
    /// Waits (15 s at most) until the panel's newest dungeon is made, after its pause. A making that failed ends the wait
    /// too (the panel says why): told apart from one that never ended, and failed with the panel's reason.
    /// </summary>
    private static async Task Settled(DungeonGenPanel p)
    {
        await TestWindows.Until(() => p.Pending.IsCompleted && (p.Place.IsEnabled || p.Summary.Text?.StartsWith("Could not make it", StringComparison.Ordinal) == true));
        Assert.True(p.Place.IsEnabled, p.Summary.Text);
    }

    [AvaloniaFact]
    public async Task TheDungeonMadeIsHandedOverAndItsLevelsHaveButtons()
    {
        var p = new DungeonGenPanel();
        var placed = new List<DungeonGen.Settings>();
        var plotted = new List<bool>();
        p.PlaceAsked += (s, _) => placed.Add(s);
        p.PlotAsked += (_, _, add) => plotted.Add(add);
        // A style of its own: its decay.
        p.Style.SelectedIndex = 1;
        Assert.Equal(Math.Round(DungeonKit.StyleOf((string)p.Style.SelectedItem!).Decay / 0.05) * 0.05, p.Decay.Value, 3);
        p.Levels.SelectedIndex = 2;
        await Settled(p);
        p.Place.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(3, Assert.Single(placed).Levels);
        p.OpenOnPlot.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        p.AddToPlot.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal([false, true], plotted);
        // A button per level; the second shown alone.
        var levels = p.View.GetLogicalDescendants().OfType<ToggleButton>().Where(b => (b.Content as string)?.StartsWith("Level ", StringComparison.Ordinal) == true).ToList();
        Assert.True(levels.Count > 1);
        levels[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(levels[1].IsChecked);
        Assert.All(levels.Where(b => b != levels[1]), b => Assert.False(b.IsChecked));
    }

    [AvaloniaFact]
    public void LevelsGoUpToTheMostAndTheGoToButtonsWaitForAPlacedDungeon()
    {
        var p = new DungeonGenPanel();
        Assert.Equal(DungeonGen.MaxLevels, p.Levels.ItemCount);
        Assert.Equal("16 levels", (string)p.Levels.Items[^1]!);
        p.Levels.SelectedIndex = DungeonGen.MaxLevels - 1;
        Assert.Equal(DungeonGen.MaxLevels, p.Settings().Levels);
        // Nowhere to go until a dungeon is placed; then both buttons ask the window.
        Assert.False(p.ToDungeon.IsEnabled);
        Assert.False(p.ToEntrance.IsEnabled);
        var asked = new List<string>();
        var placed = new List<bool>();
        p.DungeonAsked += () => asked.Add("dungeon");
        p.EntranceAsked += () => asked.Add("entrance");
        p.PlacedChanged += placed.Add;
        p.Placed(true);
        Assert.True(p.ToDungeon.IsEnabled && p.ToEntrance.IsEnabled);
        Assert.Equal([true], placed);
        p.ToDungeon.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        p.ToEntrance.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(["dungeon", "entrance"], asked);
        p.Placed(false);
        Assert.False(p.ToDungeon.IsEnabled);
        // The buttons are the Dungeon panel's to show (under the generator, once a dungeon is placed).
        Assert.DoesNotContain(p.ToDungeon, p.View.GetLogicalDescendants());
    }
}
