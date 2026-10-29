using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.InspectorControls;
using static TerrainEditor.Desktop.Tests.SharedButtons;
using static TerrainEditor.Desktop.Tests.SharedInspectorRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The inspector panel's edits of an item: the row of a stack's number and slot, the stand's row, the contents of an object of unknown size, spawn points and nests, and an object whose file is gone.
/// </summary>
[Collection("World files")]
public class InspectorPanelEditsTests
{
    [AvaloniaFact]
    public void AnItemsRowNumbersAndThePickedSlotAreEdited()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        r.I.Items!.Clear();
        Click(Labelled(r.I.ItemsBox, "Add item"));
        Click(Labelled(r.I.ItemsBox, "Add item"));
        var (a, b) = (r.I.Items[0], r.I.Items[1]);
        Assert.Equal((0, 0, 1, 0), (a.X, a.Y, b.X, b.Y));
        // The details rows: how many, quality, durability, slot x and y of the first item.
        var nums = r.I.ItemsBox.GetLogicalDescendants().OfType<NumericUpDown>().ToList();
        nums[1].Value = 3;
        nums[2].Value = 50.25m;
        nums[3].Value = 2;
        nums[4].Value = 1;
        // Durability points keep their fraction.
        Assert.Equal((3, 50.25f, 2, 1), (a.Quality, a.Durability, a.X, a.Y));
        Click(Labelled(r.I.ItemsBox, "Tidy slots"));
        Assert.Equal((0, 0), (a.X, a.Y));
        // The first slot picked: how many and how good.
        Click(Slot(r.I, 0, 0));
        // The same most for a stack here as in the details rows.
        Assert.Equal(nums[0].Maximum, SlotNumber(r.I, 90).Maximum);
        SlotNumber(r.I, 90).Value = 5;
        SlotNumber(r.I, 70).Value = 4;
        Assert.Equal((5, 4), (a.Stack, a.Quality));
        Assert.True(r.I.ApplyButton.IsEnabled);
        // Move, then the second slot: the two swap.
        Click(Labelled(r.I.ItemsBox, "Move"));
        Assert.Contains(r.I.ItemsBox.GetLogicalDescendants().OfType<Button>(), x => x.Content as string == "Click a slot…");
        Click(Slot(r.I, 1, 0));
        Assert.Equal((1, 0, 0, 0), (a.X, a.Y, b.X, b.Y));
        // Move to an empty slot: it goes there.
        Click(Labelled(r.I.ItemsBox, "Move"));
        Click(Slot(r.I, 3, 0));
        Assert.Equal((3, 0), (a.X, a.Y));
        // Move onto its own slot: nothing moves.
        Click(Labelled(r.I.ItemsBox, "Move"));
        Click(Slot(r.I, 3, 0));
        Assert.Equal((3, 0), (a.X, a.Y));
        // An empty slot: its numbers change nothing.
        Click(Slot(r.I, 2, 0));
        SlotNumber(r.I, 90).Value = 9;
        SlotNumber(r.I, 70).Value = 9;
        Assert.DoesNotContain(r.I.Items, i => i.Stack == 9 || i.Quality == 9);
        // A slot holding something takes another item; a box left empty changes nothing.
        Click(Slot(r.I, 0, 0));
        var pick = r.I.ItemsBox.GetLogicalDescendants().OfType<AutoCompleteBox>().First(x => x.PlaceholderText == "Find an item…");
        pick.Text = "Coins";
        pick.SelectedItem = "Coins";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Coins", b.Name);
        pick = r.I.ItemsBox.GetLogicalDescendants().OfType<AutoCompleteBox>().First(x => x.PlaceholderText == "Find an item…");
        pick.Text = null;
        pick.RaiseEvent(new Avalonia.Input.FocusChangedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Coins", b.Name);
        // Empty takes it out.
        Click(Slot(r.I, 0, 0));
        Click(Labelled(r.I.ItemsBox, "Empty"));
        Assert.Same(a, Assert.Single(r.I.Items));
    }

    [AvaloniaFact]
    public async Task AnItemStandsRowIsTypedNumberedEmptiedAndItsRefusalShown()
    {
        using var r = new SharedInspectorRun();
        int stand = r.Add("itemstand");
        Assert.True(r.I.Open(stand));
        var row = Assert.Single(r.I.Stand!);
        var name = r.I.ItemsBox.GetLogicalDescendants().OfType<AutoCompleteBox>().Single();
        name.Width = 120;
        name.Text = "SwordIron";
        Assert.Equal("SwordIron", row.Name);
        // Quality, style and the way it hangs.
        var nums = r.I.ItemsBox.GetLogicalDescendants().OfType<NumericUpDown>().ToList();
        nums[0].Value = 2;
        nums[1].Value = 1;
        nums[2].Value = 1;
        Assert.Equal((2, 1, 1), (row.Quality, row.Variant, r.I.StandWay));
        // Drawn again (a value added), its ✕ empties it.
        AddValue(r.I, "zz_note", "1");
        Click(r.I.ItemsBox.GetLogicalDescendants().OfType<Button>().Last(x => x.Content as string == "✕"));
        Assert.Equal("", r.I.Stand![0].Name);
        // Wood it does not take: the reason shows once the card is drawn again.
        r.I.Stand[0].Name = "Wood";
        _ = r.I.Changes();
        AddValue(r.I, "zz_other", "2");
        Assert.Contains("Wood", Texts(r.I.ItemsBox));
        // A panel nobody listens to says nothing, and changes nothing.
        var lone = new InspectorPanel(() => r.S);
        Assert.True(lone.Open(stand));
        lone.Stand![0].Name = "Wood";
        int steps = r.S.UndoList.Count;
        await lone.Apply();
        Assert.Equal(steps, r.S.UndoList.Count);
    }

    [AvaloniaFact]
    public async Task ContentsOfUnknownSizeVectorsAndQuaternionsAreShown()
    {
        using var r = new SharedInspectorRun();
        int tree = r.Scene.Things.FindIndex(t => !t.Piece && !t.Gone && ZdoData.Parse(r.Scene.World.ObjectBytes(t.Id)) is { FloatList.Count: 0, IntList.Count: 0, LongList.Count: 0, StringList.Count: 0, ByteList.Count: 0, Vec3List.Count: 0, QuatList.Count: 0 });
        Assert.True(tree >= 0);
        Assert.True(r.I.Open(tree));
        var inv = new InventoryData();
        inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Wood"), Stack = 3, X = 5 });
        r.I.Added.Add(("bytes", "items", Convert.ToBase64String(inv.Write())));
        r.I.Added.Add(("vec3", "424242", "1 2 3"));
        r.I.Added.Add(("quats", "424243", "0 0 0 1"));
        await r.I.Apply();
        Assert.StartsWith("Changed", r.Said);
        Assert.Contains("size unknown", Texts(r.I.ItemsBox));
        Assert.Contains(r.I.FieldList, f => f.Section == "vec3");
        Assert.Contains(r.I.FieldList, f => f.Section == "quats");
        Assert.Contains(r.I.Fields.GetLogicalDescendants().OfType<TextBox>(), b => b.Text == "see Contents above");
        // Four across while the size is unknown.
        Assert.Equal((0, 0), r.I.FreeSlot()!.Value);
        Click(Labelled(r.I.ItemsBox, "Tidy slots"));
        Assert.Equal((0, 0), (r.I.Items![0].X, r.I.Items[0].Y));
    }

    [AvaloniaFact]
    public void SpawnPointsAndNestsSayWhatTheySpawn()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.Add("Spawner_Boar")));
        Assert.Contains("Spawn point: ", Texts(r.I.Card));
        Assert.True(r.I.Open(r.Add("Spawner_GreydwarfNest")));
        Assert.Contains("Nest: ", Texts(r.I.Card));
    }

    [AvaloniaFact]
    public void AnObjectWhoseFileIsGoneCannotBeRead()
    {
        using var r = new SharedInspectorRun();
        int copy = r.AddCopyOfChest();
        r.DeleteWorldFiles();
        Assert.False(r.I.Open(copy));
        Assert.Equal("That object cannot be read.", r.Said);
    }
}
