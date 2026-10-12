using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D view following its area's session: an area read again from the game (after zone resets were applied) drops
/// the selection at once, as its objects have new indices.
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
}
