using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The object inspector's less common edits on the test world: an item row's numbers, the picked slot's count,
/// quality, item, Empty and Move; an item stand's row typed, emptied and numbered, and its refusal shown; contents of
/// an object whose size is unknown, with vectors and quaternions; spawn points and nests; an object whose file is
/// gone; one replaced meanwhile; and a panel of its own that nobody listens to.
/// </summary>
[Collection("World files")]
public class InspectorPanelEditsTests
{
    private static readonly int Chest = StableHash.Of("piece_chest_wood");

    /// <summary>
    /// A main window on its own copy of the test world, opened on the zone with the chest; deleted when disposed.
    /// </summary>
    private sealed class Run : IDisposable
    {
        public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
        public string Dir { get; } = Fixtures.CopyFixture();
        public WorldScene Scene { get; }
        public EditSession S => Scene.Session!;
        public InspectorPanel I => W.Inspector;
        public string? Said;

        public Run()
        {
            var world = WorldSave.Load(Dir);
            var at = world.Objects.First(o => o.Prefab == Chest).Position;
            Scene = WorldScene.Load(Dir, (int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64), 1);
            W.Show();
            W.View.Show(Scene, null);
            W.Edit(Scene.Session!);
            I.Message += t => Said = t;
        }

        public int ChestIndex => Scene.Things.FindLastIndex(t => t.Prefab == Chest && !t.Gone);

        /// <summary>
        /// A new object of this kind beside the chest; its index.
        /// </summary>
        public int Add(string prefab)
        {
            var p = Scene.Things[ChestIndex].Position + new Vector3(2, 0, 2);
            return S.Commit("Add", null, [], [(new NewObject(0, StableHash.Of(prefab), p, Vector3.Zero, 0), true)])[0];
        }

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
    }

    /// <summary>
    /// Clicks the button and runs the posted work it starts.
    /// </summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The first button with this label under the control.
    /// </summary>
    private static Button Labelled(Control c, string label) => c.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == label);

    /// <summary>
    /// Every text block's text under the control, joined with " | ".
    /// </summary>
    private static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    /// <summary>
    /// The contents grid's button for slot (x, y).
    /// </summary>
    private static Button Slot(InspectorPanel i, int x, int y) =>
        i.ItemsBox.Children.OfType<Grid>().First(g => g.RowDefinitions.Count > 0).Children.OfType<Button>().First(b => Grid.GetColumn(b) == x && Grid.GetRow(b) == y);

    /// <summary>
    /// The picked slot's number box of this width (90: how many, 70: quality).
    /// </summary>
    private static NumericUpDown SlotNumber(InspectorPanel i, double width) =>
        i.ItemsBox.GetLogicalDescendants().OfType<NumericUpDown>().First(n => n.Width == width);

    /// <summary>
    /// Adds a value through the Add row, which draws the card again.
    /// </summary>
    private static void AddValue(InspectorPanel i, string? key, string? value)
    {
        i.AddSection.SelectedIndex = 0;
        i.AddKey.Text = key;
        i.AddValue.Text = value;
        Click(i.AddButton);
    }

    [AvaloniaFact]
    public void AnItemsRowNumbersAndThePickedSlotAreEdited()
    {
        using var r = new Run();
        Assert.True(r.I.Open(r.ChestIndex));
        r.I.Items!.Clear();
        Click(Labelled(r.I.ItemsBox, "Add item"));
        Click(Labelled(r.I.ItemsBox, "Add item"));
        var (a, b) = (r.I.Items[0], r.I.Items[1]);
        Assert.Equal((0, 0, 1, 0), (a.X, a.Y, b.X, b.Y));
        // The details rows: how many, quality, durability, slot x and y of the first item.
        var nums = r.I.ItemsBox.GetLogicalDescendants().OfType<NumericUpDown>().ToList();
        nums[1].Value = 3;
        nums[2].Value = 50;
        nums[3].Value = 2;
        nums[4].Value = 1;
        Assert.Equal((3, 50f, 2, 1), (a.Quality, a.Durability, a.X, a.Y));
        Click(Labelled(r.I.ItemsBox, "Tidy slots"));
        Assert.Equal((0, 0), (a.X, a.Y));
        // The first slot picked: how many and how good.
        Click(Slot(r.I, 0, 0));
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
        using var r = new Run();
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
        using var r = new Run();
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
        using var r = new Run();
        Assert.True(r.I.Open(r.Add("Spawner_Boar")));
        Assert.Contains("Spawn point: ", Texts(r.I.Card));
        Assert.True(r.I.Open(r.Add("Spawner_GreydwarfNest")));
        Assert.Contains("Nest: ", Texts(r.I.Card));
    }

    [AvaloniaFact]
    public void AnObjectWhoseFileIsGoneCannotBeRead()
    {
        using var r = new Run();
        int copy = r.S.Commit("Add", null, [], [(new NewObject(0, r.Scene.Things[r.ChestIndex].Prefab, Vector3.Zero, Vector3.Zero, 0), true)])[0];
        foreach (string f in Directory.GetFiles(r.Dir))
        {
            File.Delete(f);
        }
        Assert.False(r.I.Open(copy));
        Assert.Equal("That object cannot be read.", r.Said);
    }

    // Apply on an object no longer at hand: another area open meanwhile (fewer things), or its file gone. Each is said,
    // nothing changes and nothing throws.
    [AvaloniaFact]
    public async Task AnObjectNoLongerAtHandIsNotApplied()
    {
        using var r = new Run();
        EditSession session = r.S;
        var panel = new InspectorPanel(() => session);
        string? said = null;
        panel.Message += t => said = t;
        Assert.True(panel.Open(r.ChestIndex));
        session = TestScenes.Flat(1);
        panel.Added.Add(("strings", "text", "Elsewhere"));
        await panel.Apply();
        Assert.Equal("This object is not in the open area any more: nothing changed.", said);
        Assert.False(panel.IsOpen);
        Assert.Empty(session.UndoList);
        // Its file gone since it was opened.
        session = r.S;
        int copy = r.S.Commit("Add", null, [], [(new NewObject(0, r.Scene.Things[r.ChestIndex].Prefab, Vector3.Zero, Vector3.Zero, 0), true)])[0];
        Assert.True(panel.Open(copy));
        foreach (string f in Directory.GetFiles(r.Dir))
        {
            File.Delete(f);
        }
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
        using var r = new Run();
        int opened = r.ChestIndex;
        var mine = new InspectorPanel(() => r.S);
        string? said = null;
        mine.Message += t => said = t;
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
        using var r = new Run();
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
