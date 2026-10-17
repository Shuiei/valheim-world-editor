using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;
using static TerrainEditor.Desktop.Tests.InspectorControls;
using static TerrainEditor.Desktop.Tests.SharedInspectorRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The inspector panel with the object it shows: a key added once, an object no longer at hand, an object replaced meanwhile, and a panel nobody listens to.
/// </summary>
[Collection("World files")]
public class InspectorPanelObjectStateTests
{
    // Apply on an object no longer at hand: another area open meanwhile (fewer things), or its file gone. Each is said,
    // nothing changes and nothing throws.
    // A key added twice, or one the object has already, is refused (Apply would set one over the other); the chest's
    // contents, edited above, cannot be removed from the list (Apply writes them again).
    [AvaloniaFact]
    public async Task AKeyIsAddedOnceAndTheContentsAreNotRemovable()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        AddStack(r, "Resin", 7);
        AddValue(r.I, "zz_note", "1");
        AddValue(r.I, "zz_note", "2");
        Assert.Equal("zz_note is there already: change its value in the list.", r.Said);
        Assert.Equal(("strings", "zz_note", "1"), Assert.Single(r.I.Added));
        await r.I.Apply();
        Assert.StartsWith("Changed", r.Said);
        Assert.Contains(r.I.FieldList, f => f.Section == "strings" && f.Key == StableHash.Of("zz_note"));
        r.Said = null;
        AddValue(r.I, "zz_note", "3");
        Assert.Equal("zz_note is there already: change its value in the list.", r.Said);
        Assert.Empty(r.I.Added);
        var items = r.I.Fields.GetLogicalDescendants().OfType<TextBox>().First(b => b.Text == "see Contents above");
        Assert.False(((Grid)items.Parent!).Children.OfType<Button>().Single().IsEnabled);
    }

    [AvaloniaFact]
    public async Task AnObjectNoLongerAtHandIsNotApplied()
    {
        using var r = new SharedInspectorRun();
        EditSession session = r.S;
        var panel = new InspectorPanel(() => session);
        string? said = null;
        panel.Message += t => said = t.Shown;
        Assert.True(panel.Open(r.ChestIndex));
        session = TestScenes.Flat(1);
        panel.Added.Add(("strings", "text", "Elsewhere"));
        await panel.Apply();
        Assert.Equal("This object is not in the open area any more: nothing changed.", said);
        Assert.False(panel.IsOpen);
        Assert.Empty(session.UndoList);
        // Its file gone since it was opened.
        session = r.S;
        int copy = r.AddCopyOfChest();
        Assert.True(panel.Open(copy));
        r.DeleteWorldFiles();
        int steps = r.S.UndoList.Count;
        said = null;
        panel.Added.Add(("strings", "text", "Gone"));
        await panel.Apply();
        Assert.NotNull(said);
        Assert.Equal(steps, r.S.UndoList.Count);
    }

    [AvaloniaFact]
    public async Task AnObjectReplacedMeanwhileIsClosed()
    {
        using var r = new SharedInspectorRun();
        int opened = r.ChestIndex;
        var mine = new InspectorPanel(() => r.S);
        string? said = null;
        mine.Message += t => said = t.Shown;
        Assert.True(mine.Open(opened));
        // Another panel changes it first: the copy replaces it.
        var other = new InspectorPanel(() => r.S);
        Assert.True(other.Open(opened));
        other.Added.Add(("strings", "text", "First"));
        await other.Apply();
        Assert.True(r.Scene.Things[opened].Gone);
        mine.Added.Add(("strings", "text", "Second"));
        await mine.Apply();
        // Offline: not blamed on the game.
        Assert.Equal("This object is gone (deleted, replaced or undone since you opened it): nothing changed.", said);
        Assert.False(mine.IsOpen);
    }

    [AvaloniaFact]
    public async Task APanelNobodyListensToSaysNothing()
    {
        using var r = new SharedInspectorRun();
        EditSession? session = r.S;
        var lone = new InspectorPanel(() => session);
        r.Scene.Things.Add(new WorldScene.Thing(-12345, StableHash.Of("Beech1"), Vector3.Zero, Vector3.Zero, 1, false));
        Assert.False(lone.Open(r.Scene.Things.Count - 1));
        Assert.True(lone.Open(r.ChestIndex));
        // Add without a name.
        AddValue(lone, null, "1");
        Assert.Empty(lone.Added);
        // A value with no text, and one of a name with no plain label.
        AddValue(lone, "text", null);
        AddValue(lone, "zz_unlabelled", "1");
        Assert.Equal(2, lone.Added.Count);
        Assert.Contains("zz_unlabelled (new)", Texts(lone.Fields));
        // A full chest.
        lone.Items!.Clear();
        for (int i = 0; i < 200 && lone.FreeSlot() != null; i++)
        {
            Click(Labelled(lone.ItemsBox, "Add item"));
        }
        int full = lone.Items.Count;
        Click(Labelled(lone.ItemsBox, "Add item"));
        Assert.Equal(full, lone.Items.Count);
        // A value that does not fit: nothing changed.
        int steps = r.S.UndoList.Count;
        lone.Added.Add(("ints", "health", "not a number"));
        await lone.Apply();
        Assert.Equal(steps, r.S.UndoList.Count);
        // Written, with nobody told.
        Assert.True(lone.Open(r.ChestIndex));
        lone.Added.Add(("strings", "text", "Hi"));
        await lone.Apply();
        Assert.Equal(steps + 1, r.S.UndoList.Count);
        var box = lone.Fields.GetLogicalDescendants().OfType<TextBox>().First(b => b.Text == "Hi");
        box.Text = null;
        Assert.Equal("", lone.FieldList.Single(f => f.Name == "text").Now);
        // The area closed: the card draws as object 0, and Apply does nothing.
        session = null;
        AddValue(lone, "zz_late", "1");
        Assert.Contains("Object 0 ", Texts(lone.Card));
        await lone.Apply();
        Assert.Equal(steps + 1, r.S.UndoList.Count);
    }
}
