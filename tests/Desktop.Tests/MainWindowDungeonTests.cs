using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A generated dungeon put into a world, 5000 m above the view's middle with a linked pair of portals (of pieces and of
/// the game's rooms), or nowhere when no area is open; and written as a blueprint opened in the Workshop, numbered when
/// one of its name is there.
/// </summary>
[Collection("World files")]
public class MainWindowDungeonTests
{
    private static readonly int Portal = StableHash.Of("portal_wood");

    /// <summary>
    /// A window editing the test world's zone 0, 0.
    /// </summary>
    private static (MainWindow W, EditSession S) Open(string dir)
    {
        var scene = WorldScene.Load(dir, 0, 0, 1);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        w.View.Show(scene, null);
        w.Edit(scene.Session!);
        return (w, scene.Session!);
    }

    [AvaloniaFact]
    public void WithoutAnAreaTheDungeonGoesNowhere()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var settings = new DungeonGen.Settings(Size: 1, Levels: 1, Seed: 4);
        w.PlaceGenerated(settings, DungeonGen.Make(settings));
        Assert.Equal("Open an area of a world first: the dungeon goes above the middle of the view.", w.MessageText.Text);
    }

    [AvaloniaFact]
    public void ADungeonOfPiecesGoesHighAboveWithTwoLinkedPortals()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var (w, s) = Open(dir);
            var settings = new DungeonGen.Settings(Size: 1, Levels: 1, Seed: 12345);
            var r = DungeonGen.Make(settings);
            int things = s.Scene.Things.Count, steps = s.UndoList.Count;
            w.PlaceGenerated(settings, r);
            Assert.StartsWith($"Generated {r.Name}:", w.MessageText.Text);
            Assert.Contains("A portal “dg12345” on the ground here leads in.", w.MessageText.Text);
            Assert.Equal(steps + 1, s.UndoList.Count);
            var added = s.Scene.Things.Skip(things).ToList();
            Assert.NotEmpty(added);
            // The pieces are high up; one portal is with them, the other on the ground.
            var portals = added.Where(t => t.Prefab == Portal).ToList();
            Assert.Equal(2, portals.Count);
            Assert.Contains(portals, p => p.Position.Y > 3000);
            Assert.Contains(portals, p => p.Position.Y < 3000);
            Assert.True(added.Count(t => t.Position.Y > 3000) > portals.Count);
            // The view looks at the way in, cut above it.
            Assert.True(w.View.CutY > 3000);
            // Ctrl+Z takes it all back.
            Assert.True(s.Undo());
            Assert.All(s.Scene.Things.Skip(things), t => Assert.True(t.Gone));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void ADungeonOfTheGamesRoomsIsOneObjectHoldingItsRooms()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var (w, s) = Open(dir);
            var settings = new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3);
            var r = DungeonGen.Make(settings);
            Assert.NotNull(r.Kind);
            int things = s.Scene.Things.Count;
            w.PlaceGenerated(settings, r);
            Assert.StartsWith($"Generated {r.Name}:", w.MessageText.Text);
            var dungeon = Assert.Single(s.Scene.Things.Skip(things), t => t.Prefab == r.Kind!.Hash);
            Assert.True(dungeon.Position.Y > 3000);
            var z = ZdoData.Parse(TerrainEditor.App.ObjectData.Bytes(s.Scene.World, s.Edits, dungeon.Id)!);
            Assert.NotNull(z.GetBytes(Dungeons.RoomDataKey));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task AGeneratedDungeonOpensInTheWorkshopAsABlueprintNumberedWhenItsNameIsTaken()
    {
        using var r = new BlueprintsPanelRun();
        r.W.Ask = (_, _, _, _) => Task.FromResult(true);
        var settings = new DungeonGen.Settings(Size: 1, Levels: 1, Seed: 7, Name: "Test crypt");
        await r.W.OpenGeneratedInWorkshop(settings);
        Assert.True(r.W.Tools.Workshop);
        Assert.True(File.Exists(Path.Combine(r.Homestead, TerrainEditor.App.Homestead.FileName("Test crypt"))));
        Assert.Contains(r.W.View.Scene!.Things, t => !t.Gone);
        await r.W.OpenGeneratedInWorkshop(settings);
        Assert.True(File.Exists(Path.Combine(r.Homestead, TerrainEditor.App.Homestead.FileName("Test crypt (2)"))));
    }
}
