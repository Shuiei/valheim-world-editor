using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The object inspector on the test world's chest: its contents and data, edited and saved.
/// </summary>
[Collection("World files")]
public class InspectorPanelChestTests
{
    private static readonly int Chest = StableHash.Of("piece_chest_wood");

    [AvaloniaFact]
    public async Task AChestsContentsAreEditedAndSaved()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = TestScenes.ZoneHolding(dir, Chest);
            var w = TestWindows.Editing(scene.Session!);
            w.Tools.ChooseSelect();
            int chest = scene.Things.FindIndex(t => t.Prefab == Chest);
            w.View.Select([chest]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(w.SelectPanel.InspectButton.IsEnabled);
            w.KeyPress(Key.I, RawInputModifiers.None, PhysicalKey.I, "i");
            var ins = w.Inspector;
            Assert.True(ins.IsOpen);
            Assert.Equal(chest, ins.Index);
            Assert.NotNull(ins.Items);
            int before = ins.Items!.Count;
            Assert.False(ins.ApplyButton.IsEnabled);
            // A stack of 7 resin in the first free slot.
            var (x, y) = ins.FreeSlot()!.Value;
            ins.Items.Add(new InspectorPanel.ItemRow { Name = "Resin", Stack = 7, X = x, Y = y });
            Assert.NotNull(ins.Changes().Inventory);
            await ins.Apply();
            var s = scene.Session!;
            Assert.True(scene.Things[chest].Gone);
            Assert.Equal((0, 1, 1, 0), s.Pending);
            Assert.StartsWith("Edited", s.UndoLabel);
            // The inspector shows the copy.
            Assert.Equal(before + 1, ins.Items!.Count);
            Assert.True(s.Save().Saved);
            var again = WorldSave.Load(dir);
            // The fixture's chest has no builder: an object, not a player's piece.
            var inv = again.Objects.Where(o => o.Prefab == Chest).Select(o => ZdoData.Parse(again.ObjectBytes(o.Id)).GetBytes(ObjectData.ItemsKey))
                .Where(b => b != null).Select(b => InventoryData.Read(b!)).Single(i => i.Items.Any(it => it.Prefab == StableHash.Of("Resin") && it.Stack == 7));
            Assert.Contains(inv.Items, i => i.Prefab == StableHash.Of("Resin") && i.Stack == 7 && i.X == x && i.Y == y);
            Assert.Equal(before + 1, inv.Items.Count);
            // Saved: nothing pending (the area and its history stay, as after Apply live).
            Assert.Equal((0, 0, 0, 0), s.Pending);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ValuesAreChangedAddedAndRemoved()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = TestScenes.ZoneHolding(dir, Chest);
            var w = TestWindows.Editing(scene.Session!);
            var ins = w.Inspector;
            int chest = scene.Things.FindIndex(t => t.Prefab == Chest);
            Assert.True(ins.Open(chest));
            ins.Added.Add(("strings", "text", "Hello"));
            var creator = ins.FieldList.FirstOrDefault(f => f.Name == "creator");
            creator?.Removed = true;
            var (set, inv) = ins.Changes();
            Assert.Contains(set, f => f.Key == "text" && f.Value == "Hello");
            Assert.Null(inv);
            await ins.Apply();
            Assert.Contains(ins.FieldList, f => f.Name == "text" && f.Value == "Hello");
            if (creator != null)
            {
                Assert.DoesNotContain(ins.FieldList, f => f.Name == "creator");
            }
            // A value that does not fit is refused, with nothing changed.
            ins.FieldList.First(f => f.Section == "strings").Now = "x";
            ins.Added.Add(("ints", "health", "not a number"));
            int pending = scene.Session!.Pending.Added;
            await ins.Apply();
            Assert.StartsWith("Could not change the object", w.MessageText.Text);
            Assert.Equal(pending, scene.Session.Pending.Added);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
