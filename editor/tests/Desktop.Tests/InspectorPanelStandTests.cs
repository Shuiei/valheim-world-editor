using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The object inspector on stands put into the test world: an item stand given a sword (and refusing wood), an armour
/// stand dressed and posed, each written as a copy in one undo step.
/// </summary>
[Collection("World files")]
public class InspectorPanelStandTests
{
    /// <summary>
    /// A window editing the test world's zone 0, 0 with a new, empty stand of that kind in it; the stand's index.
    /// </summary>
    private static (MainWindow W, EditSession S, int Stand) Open(string dir, string stand)
    {
        var scene = WorldScene.Load(dir, 0, 0, 1);
        var w = TestWindows.Editing(scene.Session!);
        var s = scene.Session!;
        int i = s.Commit("Stand", null, [], [(new NewObject(0, StableHash.Of(stand), new Vector3(3, scene.Heights[32 * scene.W + 32], 3), Vector3.Zero, 0), true)])[0];
        return (w, s, i);
    }

    [AvaloniaFact]
    public async Task AnItemStandIsGivenASwordAndRefusesWood()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var (w, s, stand) = Open(dir, "itemstand");
            var ins = w.Inspector;
            var said = new List<string>();
            ins.Message += t => said.Add(t.Shown);
            Assert.True(ins.Open(stand));
            // Empty: one row, nothing in it.
            var row = Assert.Single(ins.Stand!);
            Assert.Equal("", row.Name);
            Assert.Empty(ins.Changes().Set);
            // Wood cannot hang on it: said, nothing changed.
            row.Name = "Wood";
            int steps = s.UndoList.Count;
            await ins.Apply();
            Assert.Contains(said, t => t.Contains("Wood", StringComparison.Ordinal));
            Assert.Equal(steps, s.UndoList.Count);
            // A sword of quality 3, hanging the other way.
            row.Name = "SwordIron";
            row.Quality = 3;
            ins.StandWay = 1;
            Assert.NotEmpty(ins.Changes().Set);
            await ins.Apply();
            Assert.Equal(steps + 1, s.UndoList.Count);
            Assert.StartsWith("Changed", said[^1]);
            Assert.True(s.Scene.Things[stand].Gone);
            // The copy is open, holding the sword.
            Assert.NotEqual(stand, ins.Index);
            Assert.Equal("SwordIron", Assert.Single(ins.Stand!).Name);
            Assert.Equal(3, ins.Stand![0].Quality);
            Assert.Equal(1, ins.StandWay);
            var z = ZdoData.Parse(ObjectData.Bytes(s.Scene.World, s.Edits, s.Scene.Things[ins.Index!.Value].Id)!);
            Assert.Equal((new StandData.Held("SwordIron", 3, 0), 1), StandData.ReadItemStand(z));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task AnArmourStandIsDressedAndPosed()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var (w, s, stand) = Open(dir, "ArmorStand");
            var ins = w.Inspector;
            var said = new List<string>();
            ins.Message += t => said.Add(t.Shown);
            Assert.True(ins.Open(stand));
            // A row per kind of slot, each empty.
            var rows = ins.Stand!;
            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.Equal("", r.Name));
            Assert.All(rows, r => Assert.NotEmpty(r.Types));
            // A helmet in the first slot that takes one, and a pose.
            var helmet = rows.First(r => r.Types.Contains(Terrain.PrefabCatalog.ItemKindOf(StableHash.Of("HelmetBronze"))!.Type));
            helmet.Name = "HelmetBronze";
            helmet.Quality = 2;
            ins.StandWay = 3;
            await ins.Apply();
            Assert.StartsWith("Changed", said[^1]);
            Assert.Contains(ins.Stand!, r => r.Name == "HelmetBronze" && r.Quality == 2);
            Assert.Equal(3, ins.StandWay);
            // Food on an armour stand: said, nothing more changed.
            int steps = s.UndoList.Count;
            ins.Stand!.First(r => r.Name == "").Name = "CookedMeat";
            await ins.Apply();
            Assert.Contains("CookedMeat", said[^1]);
            Assert.Equal(steps, s.UndoList.Count);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
