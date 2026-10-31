using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window's side of building's clicks: Shift + right click takes the piece away as one undo step and says so; a
/// middle click builds with its kind (first among those picked last), or says a kind the dock does not list is not
/// one to build.
/// </summary>
public class MainWindowBuildClicksTests
{
    [AvaloniaFact]
    public async Task APieceClickedIsTakenAwayOrItsKindTaken()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        await w.OpenWorkshop(null);
        var s = w.Session!;
        var at = new Vector3(s.Scene.X0 * 64f, Workshop.Ground, s.Scene.Z0 * 64f);
        var added = s.Commit("pieces", null, [], [
            (new NewObject(0, StableHash.Of("stone_wall_2x1"), at, Vector3.Zero, 0f), true),
            (new NewObject(0, StableHash.Of("Beech1"), at + new Vector3(6, 0, 0), Vector3.Zero, 0f), false)]);
        int wall = added[0], tree = added[1];

        w.KindClicked(wall);
        Assert.Equal(["stone_wall_2x1"], w.PlaceTool.Chosen);
        Assert.Equal("stone_wall_2x1", w.BuildPanel.Recent[0]);
        Assert.Equal($"Building with {PieceCost.PieceName("stone_wall_2x1")}.", w.MessageText.Text);
        w.KindClicked(tree);
        Assert.Equal(["stone_wall_2x1"], w.PlaceTool.Chosen);
        Assert.EndsWith("is not a piece to build: pick one in the Build dock.", w.MessageText.Text);

        w.RemoveClicked(wall);
        Assert.True(s.Scene.Things[wall].Gone);
        Assert.Equal($"Took away {PieceCost.PieceName("stone_wall_2x1")}. Ctrl+Z brings it back.", w.MessageText.Text);
        // Gone already, or no such object: nothing.
        w.RemoveClicked(wall);
        w.RemoveClicked(-1);
        w.KindClicked(s.Scene.Things.Count);
        s.Undo();
        Assert.False(s.Scene.Things[wall].Gone);
    }
}
