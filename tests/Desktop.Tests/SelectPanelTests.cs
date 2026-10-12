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
public sealed class SelectPanelTests : IDisposable
{
    private readonly string? _old = SavedSelections.PathOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-panel-" + Guid.NewGuid().ToString("N")[..8]);

    public SelectPanelTests()
    {
        Directory.CreateDirectory(_dir);
        SavedSelections.PathOverride = Path.Combine(_dir, "selections.json");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        SavedSelections.PathOverride = _old;
        Directory.Delete(_dir, true);
    }

    private static readonly int Beech = StableHash.Of("Beech1"), Wall = StableHash.Of("wood_wall_half");

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
        p.Message += messages.Add;
        await p.Keep();
        Assert.Equal("Could not keep the selection: the editor's data folder cannot be written.", Assert.Single(messages));
    }
}
