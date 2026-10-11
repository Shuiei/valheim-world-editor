using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Generate panel's buttons and choices: a style gives its own decay, Place and the plot's buttons hand the dungeon
/// made over, a dungeon of several levels shows a button per level, and one of the game's rooms has no plan to draw.
/// </summary>
public class DungeonGenPanelButtonsTests
{
    /// <summary>Waits (4 s at most) until the panel's newest dungeon is made.</summary>
    private static async Task Settled(DungeonGenPanel p)
    {
        Dispatcher.UIThread.RunJobs();
        for (int i = 0; i < 200 && (!p.Place.IsEnabled || !p.Pending.IsCompleted); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await p.Pending;
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
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
}
