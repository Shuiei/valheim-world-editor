using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A generated dungeon put into a world, 5000 m above the view's middle: behind the entrance of its kind on the ground
/// there (a burial chamber's mound for a Black Forest dungeon of pieces, a Frost Cave's mouth for the game's Mountain
/// rooms, the dungeon laid out where the game lays that entrance's own), or with a linked pair of portals when its biome
/// has no entrance (the Plains); the view stays at the entrance, and goes up and back on asking. A dungeon blueprint
/// pasted goes the same way, its portal marking where players arrive. And a generated dungeon written as a blueprint
/// opened in the Workshop, numbered when one of its name is there, tagged a dungeon with that portal.
/// </summary>
[Collection("World files")]
public class MainWindowDungeonTests
{
    private static readonly int Portal = StableHash.Of("portal_wood"), Crypt = StableHash.Of("Crypt2"), Cave = StableHash.Of("MountainCave02");

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

    /// <summary>The location proxies added in the session, by location.</summary>
    private static List<int> Proxies(EditSession s) => [.. s.Edits.Added.Where(n => n.Prefab == WorldSave.LocationProxyPrefab && n.Raw != null)
        .Select(n => ZdoData.Parse(n.Raw!).IntList.First(i => i.Key == WorldSave.LocationKey).Value)];

    [AvaloniaFact]
    public void WithoutAnAreaTheDungeonGoesNowhere()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var settings = new DungeonGen.Settings(Size: 1, Levels: 1, Seed: 4);
        w.PlaceGenerated(settings, DungeonGen.Make(settings));
        Assert.Equal("Open an area of a world first: the dungeon goes above the middle of the view.", w.MessageText.Text);
        Assert.Null(w.LastPlaced);
    }

    [AvaloniaFact]
    public void ADungeonOfPiecesGoesHighAboveBehindABurialChambersEntrance()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var (w, s) = Open(dir);
            Assert.False(w.DungeonPanel.Gen.ToDungeon.IsEnabled);
            var settings = new DungeonGen.Settings(Biome: "Black Forest", Size: 1, Levels: 1, Seed: 12345);
            var r = DungeonGen.Make(settings);
            int things = s.Scene.Things.Count, steps = s.UndoList.Count;
            w.PlaceGenerated(settings, r);
            Assert.StartsWith($"Generated {r.Name}:", w.MessageText.Text);
            Assert.Contains("Its burial chamber entrance stands here, facing you.", w.MessageText.Text);
            Assert.Equal(steps + 1, s.UndoList.Count);
            var added = s.Scene.Things.Skip(things).ToList();
            Assert.NotEmpty(added);
            // No portals: the entrance's doors lead in and out. The pieces are high up; the entrance's own objects
            // (its skeletons' spawners, its crows) stand at the ground, and its proxy is drawn there.
            Assert.DoesNotContain(added, t => t.Prefab == Portal);
            Assert.True(added.Count(t => t.Position.Y > 3000) > added.Count / 2);
            Assert.Contains(added, t => t.Position.Y < 3000);
            Assert.Equal([Crypt], Proxies(s));
            var drawn = Assert.Single(LocationParts.Of(s.Scene));
            Assert.Equal(Crypt, drawn.Location);
            Assert.True(drawn.Position.Y < 3000);
            // The pieces stand around where a player arrives, high up, within the zone's box of the entrance.
            var placed = w.LastPlaced;
            Assert.NotNull(placed);
            Assert.True(placed.Arrival.Y > 5000 && placed.Entrance.Y < 3000);
            Assert.Equal(placed.Entrance.X, drawn.Position.X, 0.01f);
            Assert.Null(placed.Dungeon);
            Assert.All(added.Where(t => t.Position.Y > 3000), t => Assert.True(MathF.Abs(t.Position.X - placed.Arrival.X) < 160 && MathF.Abs(t.Position.Z - placed.Arrival.Z) < 160));
            // The view stays at the entrance, nothing cut; Go to the dungeon takes it up and cuts; Go to the entrance back.
            Assert.True(w.View.Camera.Target.Y < 3000);
            Assert.Null(w.View.CutY);
            Assert.True(w.DungeonPanel.Gen.ToDungeon.IsEnabled && w.DungeonPanel.Gen.ToEntrance.IsEnabled);
            w.GoToDungeon();
            Assert.True(w.View.Camera.Target.Y > 5000);
            Assert.True(w.View.CutY > 5000);
            w.GoToEntrance();
            Assert.True(w.View.Camera.Target.Y < 3000);
            Assert.Null(w.View.CutY);
            // Another in the same zone: the space up there is taken.
            int before = s.Scene.Things.Count;
            w.PlaceGenerated(settings, r);
            Assert.StartsWith("Something is already up there", w.MessageText.Text);
            Assert.Equal(before, s.Scene.Things.Count);
            // Ctrl+Z takes it all back, the proxy too.
            Assert.True(s.Undo());
            Assert.All(s.Scene.Things.Skip(things), t => Assert.True(t.Gone));
            Assert.Empty(LocationParts.Of(s.Scene));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void ABiomeWithoutAnEntranceGetsTwoLinkedPortals()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var (w, s) = Open(dir);
            // The Plains' only dungeon is Hildir's fortress: no entrance serves.
            var settings = new DungeonGen.Settings(Biome: "Plains", Size: 1, Levels: 1, Seed: 12345);
            var r = DungeonGen.Make(settings);
            int things = s.Scene.Things.Count;
            w.PlaceGenerated(settings, r);
            Assert.Contains("A portal “dg12345” on the ground here leads in.", w.MessageText.Text);
            var added = s.Scene.Things.Skip(things).ToList();
            var portals = added.Where(t => t.Prefab == Portal).ToList();
            Assert.Equal(2, portals.Count);
            Assert.Contains(portals, p => p.Position.Y > 3000);
            Assert.Contains(portals, p => p.Position.Y < 3000);
            Assert.True(added.Count(t => t.Position.Y > 3000) > portals.Count);
            Assert.Empty(Proxies(s));
            // The view stays on the ground here too.
            Assert.True(w.View.Camera.Target.Y < 3000);
            Assert.NotNull(w.LastPlaced);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void ADungeonOfTheGamesRoomsIsOneObjectBehindAFrostCavesMouth()
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
            Assert.Contains("Its Frost Cave entrance stands here", w.MessageText.Text);
            var dungeon = Assert.Single(s.Scene.Things.Skip(things), t => t.Prefab == r.Kind!.Hash);
            // Where the game puts a Frost Cave's dungeon for an entrance in zone 0, 0: the zone's middle, 30 m back,
            // 5000 m over the entrance's ground.
            Assert.Equal((0f, -30f), (dungeon.Position.X, dungeon.Position.Z));
            Assert.Equal(w.LastPlaced!.Entrance.Y + 5000, dungeon.Position.Y, 0.01f);
            var z = ZdoData.Parse(TerrainEditor.App.ObjectData.Bytes(s.Scene.World, s.Edits, dungeon.Id)!);
            var rooms = Dungeons.Read(z.GetBytes(Dungeons.RoomDataKey)!);
            Assert.True(rooms.Count > 5);
            var first = rooms[0];
            Assert.True(Vector3.Distance(dungeon.Position, first.At(first.Room!.Openings.First(o => o.Entrance)).Position) < 0.01f);
            Assert.Equal([Cave], Proxies(s));
            Assert.Equal(dungeon.Position, w.LastPlaced.Dungeon);
            // Go to the dungeon: the Dungeon panel opens it and looks inside.
            w.GoToDungeon();
            Assert.True(w.View.Camera.Target.Y > 5000);
            Assert.NotNull(w.View.CutY);
            Assert.Equal(dungeon.Position, w.View.Dungeon.Dungeon?.Thing.Position);
            // Go to entrance from the Dungeon panel: down to the cave's mouth, nothing cut.
            w.DungeonPanel.Outside();
            Assert.True(w.View.Camera.Target.Y < 3000);
            Assert.Null(w.View.CutY);
            Assert.Equal(w.LastPlaced.Entrance.X, w.View.Camera.Target.X + s.Scene.Cx, 0.01f);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void APastedDungeonBlueprintGoesUpBehindAnEntranceItsPortalMarkingTheArrival()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var (w, s) = Open(dir);
            int floor = StableHash.Of("blackmarble_floor_large"), wall = StableHash.Of("stone_wall_2x1");
            var clip = BlueprintsPanelRun.Clip("Deep hall", "blackmarble_floor_large", "stone_wall_2x1", "portal_wood");
            // The portal 3 m north of the floor, half a metre up, facing east: players arrive there, facing east; the
            // wall 4 m east of it.
            clip.Objects[2] = clip.Objects[2] with { Dx = 0, Dz = 3, Dy = 0.5f, Rotation = new Vector3(0, 90, 0) };
            clip.Objects[1] = clip.Objects[1] with { Dx = 4, Dz = 3, Dy = 0.5f };
            clip.Dungeon = true;
            int things = s.Scene.Things.Count;
            w.PasteDungeon(clip, new Vector2(40, 40));
            Assert.StartsWith("Pasted Deep hall:", w.MessageText.Text);
            Assert.Contains("entrance stands here", w.MessageText.Text);
            var added = s.Scene.Things.Skip(things).ToList();
            // The floor and the wall high up, no portal (the entrance's door out stands where it was); the entrance at
            // the point clicked (grid 40, 40 of zone 0, 0: world 8, 8).
            Assert.DoesNotContain(added, t => t.Prefab == Portal);
            var high = added.Where(t => t.Position.Y > 3000).ToList();
            Assert.Equal(2, high.Count);
            Assert.Contains(high, t => t.Prefab == floor);
            Assert.Contains(high, t => t.Prefab == wall);
            var placed = w.LastPlaced!;
            Assert.Equal((8f, 8f), (placed.Entrance.X, placed.Entrance.Z));
            Assert.Single(Proxies(s));
            // Moved and turned as one: the wall stays 4 m from the arrival at its height, the floor 3 m away and half a
            // metre under it.
            var f = high.First(t => t.Prefab == floor);
            var wl = high.First(t => t.Prefab == wall);
            Assert.Equal(4f, Vector3.Distance(wl.Position, placed.Arrival), 0.01f);
            Assert.Equal(MathF.Sqrt(9 + 0.25f), Vector3.Distance(f.Position, placed.Arrival), 0.01f);
            Assert.Equal(-0.5f, f.Position.Y - placed.Arrival.Y, 0.01f);
            // Through the Paste tool's click, mirrored as the tool shows it: the portal faces west instead of east, so
            // the dungeon is turned round and the floor lands on the other side of the way in (the entrance faces the
            // same way, the camera not having moved); a second portal among the pieces is an ordinary piece (only the
            // first marks the arrival).
            Assert.True(s.Undo());
            w.View.Paste.Clip = clip;
            w.View.Paste.Mirror = true;
            clip.Objects.Add(clip.Objects[2] with { Dx = 2, Dz = 5 });
            w.PasteAt(new Vector2(40, 40));
            Assert.StartsWith("Pasted Deep hall:", w.MessageText.Text);
            var mirrored = s.Scene.Things.Skip(things).Where(t => !t.Gone && t.Position.Y > 3000).ToList();
            Assert.Equal(3, mirrored.Count);
            Assert.Single(mirrored, t => t.Prefab == Portal);
            var wall2 = mirrored.First(t => t.Prefab == wall);
            Assert.Equal(4f, Vector3.Distance(wall2.Position, w.LastPlaced!.Arrival), 0.01f);
            Assert.True(Vector3.Cross(wl.Position - f.Position, wall2.Position - mirrored.First(t => t.Prefab == floor).Position).Y > 1f);
            w.View.Paste.Mirror = false;
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
        string path = Path.Combine(r.Homestead, TerrainEditor.App.Homestead.FileName("Test crypt"));
        Assert.True(File.Exists(path));
        Assert.Contains(r.W.View.Scene!.Things, t => !t.Gone);
        // Tagged a dungeon, with a portal marking where players arrive.
        var entry = TerrainEditor.App.Homestead.Read(path)!;
        Assert.True(entry.Details.Dungeon);
        Assert.Equal(1, entry.Kinds["portal_wood"]);
        await r.W.OpenGeneratedInWorkshop(settings);
        Assert.True(File.Exists(Path.Combine(r.Homestead, TerrainEditor.App.Homestead.FileName("Test crypt (2)"))));
    }
}
