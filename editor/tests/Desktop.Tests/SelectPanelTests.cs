using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select panel on its own (no window listening to it): Replace with its list out of step or nobody asked, the
/// eyedropper and the saved selections with nobody told, and a selection that cannot be kept said so.
/// </summary>
[Collection("Saved selections")]
public sealed class SelectPanelTests : SavedSelectionsFolder
{
    private static readonly int Beech = StableHash.Of("Beech1"), Wall = StableHash.Of("wood_wall_half");

    [AvaloniaFact]
    public void MoveThereMovesOnlyAlongTheAxesChanged()
    {
        // A tree on the ground whose place and turn the boxes cannot show exactly (a centimetre, a tenth of a degree).
        var w = new MainWindow(load: false) { Width = 1200, Height = 800 };
        w.Show();
        var at = new Vector3(20.004f, 30, 40.006f);
        var s = TestScenes.Flat(2, new WorldScene.Thing(10, Beech, at, new Vector3(0, 33.33f, 0), 1, false));
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var p = w.SelectPanel;
        Assert.Equal(20m, p.XBox.Value);
        Assert.Equal(33.3m, p.TurnBox.Value);
        // Nothing changed: Move there is not a move.
        p.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(s.CanUndo);
        Assert.Single(s.Scene.Things);
        // Only Y changed: the tree is lifted, its exact place and turn kept.
        p.YBox.Value = 32;
        p.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(s.CanUndo);
        var moved = s.Scene.Things[^1];
        Assert.Equal(20.004f, moved.Position.X, 4);
        Assert.Equal(32, moved.Position.Y, 4);
        Assert.Equal(40.006f, moved.Position.Z, 4);
        Assert.Equal(33.33f, moved.Rotation.Y, 3);
        // A typed place is reached exactly.
        p.XBox.Value = 25.5m;
        p.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        moved = s.Scene.Things[^1];
        Assert.Equal(25.5f, moved.Position.X, 4);
        Assert.Equal(32, moved.Position.Y, 4);
        Assert.Equal(40.006f, moved.Position.Z, 4);
        // A lift from the keys not committed yet when Y is typed: the typed height is reached, not the lift plus the
        // difference from the height shown before it.
        w.View.SelectTool.Lift(1);
        Assert.True(w.View.SelectTool.Moving);
        p.YBox.Value = 40;
        p.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(w.View.SelectTool.Moving);
        moved = s.Scene.Things[^1];
        Assert.Equal(25.5f, moved.Position.X, 4);
        Assert.Equal(40, moved.Position.Y, 4);
        Assert.Equal(40.006f, moved.Position.Z, 4);
        w.CloseWithoutAsking();
    }

    /// <summary>
    /// A panel for a view of its own, given two trees and a wall, the first two selected, in world "Here"; nobody
    /// listens to its messages.
    /// </summary>
    private static (SelectPanel P, List<int> Selected) Panel()
    {
        var things = new List<WorldScene.Thing>
        {
            new(1, Beech, new Vector3(10, 30, 10), Vector3.Zero, 1, false),
            new(2, Beech, new Vector3(14, 30, 10), Vector3.Zero, 1, false),
            new(3, Wall, new Vector3(18, 30, 10), Vector3.Zero, 0, true),
        };
        var selected = new List<int> { 0, 1 };
        var p = new SelectPanel(new GlView().SelectTool)
        {
            WorldName = () => "Here",
            Things = () => things,
            SelectedIds = () => selected,
            SelectIds = ids =>
            {
                var chosen = ids.ToList();
                selected.Clear();
                selected.AddRange(chosen);
            },
        };
        return (p, selected);
    }

    /// <summary>
    /// Raises the button's click.
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void ReplaceAsksOnlyForAKindInTheList()
    {
        var (p, _) = Panel();
        p.ReplaceKinds = [Beech, Wall];
        // One more name than kinds: the lists out of step.
        p.ReplaceBox.ItemsSource = new List<string> { "Beech", "Wall", "Stray" };
        var asked = new List<int>();
        // Nothing chosen, then a name without a kind: nothing asked.
        Click(p.ReplaceButton);
        p.ReplaceBox.SelectedIndex = 2;
        Click(p.ReplaceButton);
        // Nobody listening: nothing happens either.
        p.ReplaceBox.SelectedIndex = 0;
        Click(p.ReplaceButton);
        p.ReplaceAsked += asked.Add;
        p.ReplaceBox.SelectedIndex = 1;
        Click(p.ReplaceButton);
        Assert.Equal([Wall], asked);
    }

    [AvaloniaFact]
    public void TheEyedropperWithNobodyToldStillChoosesTheKind()
    {
        var (p, _) = Panel();
        p.ReplaceKinds = [Beech, Wall];
        p.ReplaceBox.ItemsSource = new List<string> { "Beech", "Wall" };
        p.UsePicked(12345);
        Assert.Equal(-1, p.ReplaceBox.SelectedIndex);
        p.UsePicked(Wall);
        Assert.Equal(1, p.ReplaceBox.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task SavedSelectionsWorkWithNobodyTold()
    {
        var (p, selected) = Panel();
        // No world: nothing kept.
        p.WorldName = () => null;
        await p.Keep();
        Assert.Empty(SavedSelections.For("Here"));
        p.WorldName = () => "Here";
        await p.Keep();
        Assert.Equal("Selection 1", Assert.Single(SavedSelections.For("Here")).Name);
        Assert.Equal(0, p.SavedBox.SelectedIndex);
        selected.Clear();
        p.UseSaved(false);
        Assert.Equal([0, 1], selected);
        await p.Forget();
        Assert.Empty(SavedSelections.For("Here"));
    }

    [AvaloniaFact]
    public async Task ASelectionThatCannotBeWrittenIsSaidSo()
    {
        var (p, _) = Panel();
        // A folder in the file's place: nothing can be written.
        Directory.CreateDirectory(SavedSelections.PathOverride!);
        await p.Keep();
        Assert.Equal(0, p.SavedBox.ItemCount);
        var messages = new List<string>();
        p.Message += m => messages.Add(m.Shown);
        await p.Keep();
        Assert.Equal("Could not keep the selection: selections.json in the editor's data folder cannot be read or written.", Assert.Single(messages));
    }

    // "A drag draws": each choice shown whole in the panel (beside its label, the longer one was cut off).
    [AvaloniaFact]
    public void TheDragChoicesAreShownWhole()
    {
        var p = new SelectPanel(new GlView().SelectTool);
        double[] natural = [.. Enumerable.Range(0, 2).Select(i => LaidOut.NaturalWidth(new ComboBox { ItemsSource = p.ZoneBox.ItemsSource, SelectedIndex = i, FontSize = 12 }))];
        LaidOut.In(p.Card, 1000, () =>
        {
            for (int i = 0; i < natural.Length; i++)
            {
                p.ZoneBox.SelectedIndex = i;
                p.Card.UpdateLayout();
                Assert.True(p.ZoneBox.Bounds.Width >= natural[i], $"{p.ZoneBox.Bounds.Width} < {natural[i]}");
            }
        });
    }
}
