using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D view following its area's session: an area read again from the game (after zone resets were applied) drops
/// the selection at once, as its objects have new indices; another area shown, the last one's session is not followed.
/// </summary>
[Collection("World files")]
public class GlViewSessionTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    [AvaloniaFact]
    public async Task AnAreaReadAgainDropsTheSelection()
    {
        using var game = new FakeGame();
        var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
        var scene = WorldScene.Load(world, 0, 0, 1);
        var s = scene.Session!;
        var w = new MainWindow(load: false);
        w.Show();
        w.View.Show(scene, null);
        int first = scene.Things.FindIndex(t => !t.Gone);
        Assert.True(first >= 0);
        w.View.Select([first]);
        Assert.Single(w.View.Selected);
        s.Shape(32, 32, Two, 3, 0, "raise");
        world.Edits.SetReset(new ZoneReset(5, 5, true, false), true);
        var o = await s.ApplyLive();
        Assert.True(o.Reloaded, o.Message);
        Assert.Empty(w.View.Selected);
    }

    // Another area shown: the last one's session is not followed any more. Its change to a thing the new scene does not
    // have (index 2 of 3) threw from the old session's step, and it touched the new scene's selection.
    [AvaloniaFact]
    public void TheLastAreasSessionIsNotFollowedAnyMore()
    {
        static WorldScene.Thing Rock(int id) => new(id, StableHash.Of("Rock_4"), new System.Numerics.Vector3(id, 30, 0), System.Numerics.Vector3.Zero, 1, false);
        var before = TestScenes.Flat(1, Rock(1), Rock(2), Rock(3));
        var now = TestScenes.Flat(1, Rock(4));
        var w = new MainWindow(load: false);
        w.Show();
        w.View.Show(before.Scene, null);
        w.View.Show(now.Scene, null);
        w.View.Select([0]);
        before.Delete([2]);
        Assert.Equal([0], w.View.Selected);
    }
}
