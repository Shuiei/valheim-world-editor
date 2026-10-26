using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;
using static TerrainEditor.Desktop.Tests.SharedInspectorRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The object inspector through its own controls, on the test world's chest and a tree: typing a value then Revert, ✕
/// and ↺ on a value, Add (with and without a name), the contents table (Add item until the chest is full, Tidy slots,
/// ✕, an item outside the slots and its question), unreadable contents, an object with no data, one that cannot be
/// read, and closing.
/// </summary>
[Collection("World files")]
public class InspectorPanelTests
{
    /// <summary>
    /// Gives the chest a value of each kind the panel shows (through Apply), and reopens it.
    /// </summary>
    private static async Task<int> WithValues(SharedInspectorRun r)
    {
        Assert.True(r.I.Open(r.ChestIndex));
        r.I.Added.Add(("strings", "text", "Hello"));
        r.I.Added.Add(("ints", "item", StableHash.Of("Wood").ToString(CultureInfo.InvariantCulture)));
        r.I.Added.Add(("floats", "health", "250.5"));
        await r.I.Apply();
        Assert.True(r.I.IsOpen);
        return r.I.Index!.Value;
    }

    // The slot grid: an empty slot clicked, an item picked; the box losing focus after the grid is
    // drawn again does not put it in a second time.
    [AvaloniaFact]
    public void APickedItemGoesInItsSlotOnce()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        var grid = r.I.ItemsBox.Children.OfType<Grid>().First(g => g.RowDefinitions.Count > 0);
        var (x, y) = r.I.FreeSlot()!.Value;
        var slot = grid.Children.OfType<Button>().First(b => Grid.GetColumn(b) == x && Grid.GetRow(b) == y);
        Click(slot);
        var pick = r.I.ItemsBox.GetLogicalDescendants().OfType<AutoCompleteBox>().First(b => b.PlaceholderText == "Find an item…");
        pick.Text = "Coins";
        pick.SelectedItem = "Coins";
        Dispatcher.UIThread.RunJobs();
        pick.RaiseEvent(new Avalonia.Input.FocusChangedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Single(r.I.Items!, i => i.X == x && i.Y == y);
        Assert.Equal("Coins", r.I.Items!.Single(i => i.X == x && i.Y == y).Name);
        Assert.True(r.I.ApplyButton.IsEnabled);
    }

    [AvaloniaFact]
    public async Task TypingAValueEnablesApplyAndRevertPutsItBack()
    {
        using var r = new SharedInspectorRun();
        await WithValues(r);
        var text = r.I.FieldList.Single(f => f.Name == "text");
        var box = r.I.Fields.GetLogicalDescendants().OfType<TextBox>().First(b => b.Text == "Hello");
        Assert.False(r.I.ApplyButton.IsEnabled);
        box.Text = "Goodbye";
        Assert.Equal("Goodbye", text.Now);
        Assert.True(r.I.ApplyButton.IsEnabled && r.I.RevertButton.IsEnabled);
        Click(r.I.RevertButton);
        Assert.Equal("Hello", r.I.FieldList.Single(f => f.Name == "text").Now);
        Assert.False(r.I.ApplyButton.IsEnabled);
        // An int that is a kind of item shows its name beside it.
        Assert.Contains("Wood", Texts(r.I.Fields));
    }

    [AvaloniaFact]
    public async Task AValueIsRemovedAndKeptAgainWithItsButton()
    {
        using var r = new SharedInspectorRun();
        await WithValues(r);
        var health = r.I.FieldList.Single(f => f.Name == "health");
        int rows = r.I.Fields.Children.Count;
        Click(Labelled(r.I.Fields, "✕"));
        var removed = r.I.FieldList.Single(f => f.Removed);
        Assert.Contains(r.I.Changes().Set, c => c.Value == null);
        Click(Labelled(r.I.Fields, "↺"));
        Assert.False(removed.Removed);
        Assert.Empty(r.I.Changes().Set);
        Assert.Equal(rows, r.I.Fields.Children.Count);
        Assert.Equal("250.5", health.Value);
    }

    [AvaloniaFact]
    public void AddNeedsANameAndANewValueCanBeTakenBack()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        r.I.AddKey.Text = "  ";
        Click(r.I.AddButton);
        Assert.Equal("Type the name of the value to add (for example text for a sign).", r.Said);
        Assert.Empty(r.I.Added);
        r.I.AddSection.SelectedIndex = 0;
        r.I.AddKey.Text = "text";
        r.I.AddValue.Text = "Odin was here";
        Click(r.I.AddButton);
        Assert.Single(r.I.Added);
        Assert.Equal("", r.I.AddKey.Text);
        Assert.Contains("(new)", Texts(r.I.Fields));
        Assert.True(r.I.ApplyButton.IsEnabled);
        // Its ✕ is the last one.
        r.I.Fields.GetLogicalDescendants().OfType<Button>().Last(b => b.Content as string == "✕").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(r.I.Added);
    }

    [AvaloniaFact]
    public async Task TheContentsTableAddsTidiesAndTakesOutItems()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        Assert.NotNull(r.I.Items);
        Assert.Contains("CONTENTS", Texts(r.I.ItemsBox));
        // Add items until the chest is full: then it says so.
        for (int i = 0; i < 200 && r.Said == null; i++)
        {
            Click(Labelled(r.I.ItemsBox, "Add item"));
        }
        Assert.Equal("No free slot left in this container.", r.Said);
        int full = r.I.Items!.Count;
        Assert.Null(r.I.FreeSlot());
        // Take the first out, then tidy: every item in the first slots, row by row.
        Click(Labelled(r.I.ItemsBox, "✕"));
        Assert.Equal(full - 1, r.I.Items.Count);
        r.I.Items[0].X = 4;
        r.I.Items[0].Y = 1;
        Click(Labelled(r.I.ItemsBox, "Tidy slots"));
        Assert.Equal((0, 0), (r.I.Items[0].X, r.I.Items[0].Y));
        Assert.Equal((1, 0), (r.I.Items[1].X, r.I.Items[1].Y));
        // Editing a row's numbers and name marks the contents as changed.
        var stack = r.I.ItemsBox.GetLogicalDescendants().OfType<NumericUpDown>().First();
        stack.Value = 42;
        Assert.Equal(42, r.I.Items[0].Stack);
        var name = r.I.ItemsBox.GetLogicalDescendants().OfType<AutoCompleteBox>().First();
        name.Text = "Resin";
        Assert.Equal("Resin", r.I.Items[0].Name);
        Assert.NotNull(r.I.Changes().Inventory);
        await r.I.Apply();
        Assert.StartsWith("Changed", r.Said);
    }

    [AvaloniaFact]
    public async Task AnItemOutsideTheSlotsIsAskedAbout()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        Click(Labelled(r.I.ItemsBox, "Add item"));
        r.I.Items![^1].X = 99;
        // Shown with a warning border once the table is drawn again (adding another item draws it).
        Click(Labelled(r.I.ItemsBox, "Add item"));
        Assert.Contains(r.I.ItemsBox.GetLogicalDescendants().OfType<NumericUpDown>(), n => n.BorderBrush == Avalonia.Media.Brushes.Orange);
        string? question = null;
        r.I.Confirm = (_, q, _) => { question = q; return Task.FromResult(false); };
        int pending = r.Scene.Session!.Pending.Added;
        await r.I.Apply();
        Assert.Contains("outside the", question);
        Assert.Equal(pending, r.Scene.Session.Pending.Added);
        r.I.Confirm = (_, _, _) => Task.FromResult(true);
        await r.I.Apply();
        Assert.Equal(pending + 1, r.Scene.Session.Pending.Added);
    }

    // Contents in an item format older than the editor reads, and contents whose crafter name has a length prefix that
    // never ends: both said, neither written back.
    [AvaloniaFact]
    public async Task UnreadableContentsAreSaidAndLeftAlone()
    {
        byte[] older = [255, 255, 255, 255, 1, 2];
        byte[] badPrefix = [108, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0x20, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        foreach (byte[] items in new[] { older, badPrefix })
        {
            using var r = new SharedInspectorRun();
            Assert.True(r.I.Open(r.ChestIndex));
            r.I.Added.Add(("bytes", "items", Convert.ToBase64String(items)));
            await r.I.Apply();
            Assert.Contains("The contents cannot be read", Texts(r.I.ItemsBox));
            Assert.Null(r.I.Changes().Inventory);
        }
    }

    [AvaloniaFact]
    public void AnObjectWithNoDataAndOneThatCannotBeRead()
    {
        using var r = new SharedInspectorRun();
        int tree = r.Scene.Things.FindIndex(t => !t.Piece && ZdoData.Parse(r.Scene.World.ObjectBytes(t.Id)) is { FloatList.Count: 0, IntList.Count: 0, LongList.Count: 0, StringList.Count: 0, ByteList.Count: 0, Vec3List.Count: 0, QuatList.Count: 0 });
        Assert.True(tree >= 0);
        Assert.True(r.I.Open(tree));
        Assert.Contains("This object holds no data", Texts(r.I.Fields));
        Assert.Null(r.I.Items);
        // A new object the session does not know: nothing to read.
        r.Scene.Things.Add(new WorldScene.Thing(-12345, StableHash.Of("Beech1"), System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 1, false));
        Assert.False(r.I.Open(r.Scene.Things.Count - 1));
        Assert.Equal("That object cannot be read.", r.Said);
    }

    [AvaloniaFact]
    public void WithoutAnAreaNothingOpensAndCloseHidesIt()
    {
        var lone = new InspectorPanel(() => null);
        Assert.False(lone.Open(0));
        using var r = new SharedInspectorRun();
        bool closed = false;
        r.I.Closed += () => closed = true;
        Assert.True(r.I.Open(r.ChestIndex));
        Click(r.I.Card.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == "✕"));
        Assert.False(r.I.IsOpen);
        Assert.True(closed);
    }

    // A stack larger than the item's largest is cut to it, and the message says so.
    [AvaloniaFact]
    public async Task AStackTooLargeIsCutAndSaid()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        var (x, y) = r.I.FreeSlot()!.Value;
        r.I.Items!.Add(new InspectorPanel.ItemRow { Name = "Resin", Stack = 70, X = x, Y = y });
        await r.I.Apply();
        Assert.EndsWith(" Resin: stack of 70 cut to 50 (the item's maximum).", r.Said);
    }
}
