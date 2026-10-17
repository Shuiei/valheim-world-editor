using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The markers of objects not saved yet: new objects, spawn points and nests carry one until saved while their kind is
/// shown, applied objects lose theirs in live mode, and a kind switched on in view says so.
/// </summary>
[Collection("World files")]
public class NewObjectMarkerTests
{
    /// <summary>
    /// A new object of this kind at grid point (x, z) of the area, on the ground at 30 m.
    /// </summary>
    private static NewObject Tree(EditSession s, string name, float x, float z) => new(0, StableHash.Of(name), new Vector3(x + s.Scene.X0 * 64 - 32, 30, z + s.Scene.Z0 * 64 - 32), Vector3.Zero, 1f);

    [AvaloniaFact]
    public void NewObjectsCarryAMarkerUntilSavedWhenTheirKindIsShown()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var s = r.S;
        Assert.Empty(r.W.View.NewMarkers(s.Scene));
        var added = s.Commit("Placed", null, [], [(Tree(s, "Beech1", 20, 20), false), (Tree(s, "Pickable_Stone", 30, 30), false)]);
        Dispatcher.UIThread.RunJobs();
        var marks = r.W.View.NewMarkers(s.Scene);
        Assert.Equal(2, marks.Count);
        // Just above the object, in the view's frame.
        var t = s.Scene.Things[added[0]];
        Assert.Contains(new Vector3(t.Position.X - s.Scene.Cx, t.Position.Y + 0.4f, -(t.Position.Z - s.Scene.Cz)), marks);
        // A kind switched off in View: its markers go too.
        r.W.KindBoxes[ObjectKind.Pickables].IsChecked = false;
        Assert.Single(r.W.View.NewMarkers(s.Scene));
        r.W.KindBoxes[ObjectKind.Pickables].IsChecked = true;
        // Deleted: no marker.
        s.Delete([added[1]]);
        Assert.Single(r.W.View.NewMarkers(s.Scene));
        // Objects that were in the world already never have one.
        Assert.DoesNotContain(s.Scene.Things.Where(x => x.Id >= 0), x => marks.Contains(new Vector3(x.Position.X - s.Scene.Cx, x.Position.Y + 0.4f, -(x.Position.Z - s.Scene.Cz))));
    }

    [AvaloniaFact]
    public void SpawnPointsAndNestsCarryAMarkerOfTheirColourWhileTheirKindIsShown()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var s = r.S;
        // Spawn points and nests are off at first (spoilers).
        Assert.False(r.W.View.IsShown(ObjectKind.SpawnPoints));
        Assert.False(r.W.View.IsShown(ObjectKind.Nests));
        var added = s.Commit("Placed", null, [], [(Tree(s, "Spawner_Boar", 20, 20), false), (Tree(s, "Spawner_Skeleton_respawn_30", 24, 20), false),
            (Tree(s, "Spawner_GreydwarfNest", 28, 20), false), (Tree(s, "Beech1", 32, 20), false)]);
        Dispatcher.UIThread.RunJobs();
        // Placing them switches their kinds on, so that what was placed shows.
        Assert.True(r.W.View.IsShown(ObjectKind.SpawnPoints));
        Assert.True(r.W.View.IsShown(ObjectKind.Nests));
        var boar = s.Scene.Things[added[0]];
        var at = new Vector3(boar.Position.X - s.Scene.Cx, boar.Position.Y + 2.4f, -(boar.Position.Z - s.Scene.Cz));
        r.W.View.SetCamera(at + new Vector3(0, 20, 20), at, 1.5f);
        var marks = r.W.View.SpawnMarkers(s.Scene);
        Assert.Equal(3, marks.Count);
        Assert.Contains((at, GlView.SpawnColour(false)), marks);
        Assert.Contains(marks, m => m.Colour == GlView.SpawnColour(true));
        Assert.Contains(marks, m => m.Colour == GlView.NestColour);
        // Switched off in View: their markers go.
        r.W.KindBoxes[ObjectKind.Nests].IsChecked = false;
        Assert.Equal(2, r.W.View.SpawnMarkers(s.Scene).Count);
        // Too far from the camera: none.
        r.W.View.SetCamera(at + new Vector3(0, 0, GlView.SpawnMarkerRange + 50), at, 1.5f);
        Assert.Empty(r.W.View.SpawnMarkers(s.Scene));
    }

    [AvaloniaFact]
    public async Task AppliedObjectsLoseTheirMarkerInLiveMode()
    {
        using var game = new FakeGame();
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var scene = WorldScene.Load(world, 0, 0, 1);
        var s = scene.Session!;
        w.View.Show(scene, null);
        w.Edit(s);
        s.Commit("Placed", null, [], [(Tree(s, "Beech1", 20, 20), false)]);
        Assert.Single(w.View.NewMarkers(scene));
        var o = await s.ApplyLive();
        Assert.True(o.Done, o.Message);
        Assert.Empty(w.View.NewMarkers(scene));
        // Another one placed afterwards: marked until it is applied too.
        s.Commit("Placed", null, [], [(Tree(s, "Beech1", 25, 25), false)]);
        Assert.Single(w.View.NewMarkers(scene));
    }

    [Fact]
    public async Task TheLiveSyncKnowsWhichNewObjectsTheGameHas()
    {
        using var game = new FakeGame();
        var (world, s) = await TestScenes.LiveArea(game);
        int i = s.Commit("Placed", null, [], [(Tree(s, "Beech1", 20, 20), false)])[0];
        int id = s.Scene.Things[i].Id;
        Assert.False(world.LiveSync.IsLive(id));
        await s.ApplyLive();
        Assert.True(world.LiveSync.IsLive(id));
        Assert.False(world.LiveSync.IsLive(12345));
    }

    [AvaloniaFact]
    public void PlacingAKindSwitchedOffInViewSwitchesItOnAndSaysSo()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var s = r.S;
        var box = r.W.KindBoxes[ObjectKind.Pickables];
        box.IsChecked = false;
        Assert.False(r.W.View.IsShown(ObjectKind.Pickables));
        r.W.MessageText.Text = "Placed 1 object.";
        s.Commit("Placed", null, [], [(Tree(s, "Pickable_Stone", 20, 20), false)]);
        Dispatcher.UIThread.RunJobs();
        Assert.True(box.IsChecked);
        Assert.True(r.W.View.IsShown(ObjectKind.Pickables));
        Assert.Equal("Placed 1 object. Switched on Pickables in View, so what you placed stays visible.", r.W.MessageText.Text);
    }

    [AvaloniaFact]
    public void KindsAlreadyShownChangeNothingAndSeveralAreNamedTogether()
    {
        using var r = new SharedPlaceWindow.PanelRun();
        var s = r.S;
        r.W.MessageText.Text = "Placed.";
        s.Commit("Placed", null, [], [(Tree(s, "Beech1", 20, 20), false)]);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Placed.", r.W.MessageText.Text);
        // Two kinds off: both named, in one sentence; no message of the tool before it: just this.
        r.W.KindBoxes[ObjectKind.Pickables].IsChecked = false;
        r.W.KindBoxes[ObjectKind.Rocks].IsChecked = false;
        r.W.MessageText.Text = "";
        s.Commit("Pasted", null, [], [(Tree(s, "Pickable_Stone", 20, 20), false), (Tree(s, "Rock_3", 22, 22), false), (Tree(s, "Rock_3", 24, 24), false)]);
        Dispatcher.UIThread.RunJobs();
        Assert.True(r.W.KindBoxes[ObjectKind.Rocks].IsChecked);
        Assert.True(r.W.KindBoxes[ObjectKind.Pickables].IsChecked);
        Assert.StartsWith("Switched on ", r.W.MessageText.Text);
        Assert.Contains("Pickables", r.W.MessageText.Text);
        Assert.Contains("Rocks", r.W.MessageText.Text);
        // Undo and redo bring objects back without switching anything (only new placing does).
        r.W.KindBoxes[ObjectKind.Rocks].IsChecked = false;
        s.Undo();
        s.Redo();
        Dispatcher.UIThread.RunJobs();
        Assert.False(r.W.KindBoxes[ObjectKind.Rocks].IsChecked);
    }
}
