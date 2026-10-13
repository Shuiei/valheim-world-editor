using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window's Build here (on and off from the window, Ctrl + wheel lifting the piece, ended by another tool or another
/// area), a generated dungeon with pieces this world cannot make, and the Workshop's generated dungeon opened on the plot
/// or not written when its folder cannot be made.
/// </summary>
[Collection("World files")]
public class MainWindowDungeonBuildTests
{
    /// <summary>
    /// A shown window editing a flat 2 × 2 zone area (ground at 30 m).
    /// </summary>
    private static (MainWindow W, EditSession S) Open()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return (w, s);
    }

    [AvaloniaFact]
    public void BuildHereGoesOnOnceLiftsWithTheWheelAndEndsWithAnotherToolOrArea()
    {
        var (w, s) = Open();
        // Off when it is not on: nothing.
        w.BuildInDungeon(false);
        Assert.False(w.BuildingInDungeon);
        w.BuildInDungeon(true);
        Assert.True(w.BuildingInDungeon);
        Assert.Equal(ToolMode.Place, w.Tools.Mode);
        Assert.True(w.PlaceTool.Building);
        Assert.StartsWith("Building in the dungeon", w.MessageText.Text);
        // On again: still on, the settings kept aside once.
        w.BuildInDungeon(true);
        Assert.True(w.BuildingInDungeon);
        // Ctrl + wheel lifts the piece.
        float lift = w.PlaceTool.HeightNudge;
        Assert.True(w.View.CtrlWheel!(1, false));
        Assert.Equal(lift + 0.5f, w.PlaceTool.HeightNudge, 3);
        Assert.True(w.View.CtrlWheel!(-1, true));
        Assert.Equal(lift + 0.4f, w.PlaceTool.HeightNudge, 3);
        // Another tool (not the Dungeon tool): it ends, the cut with it.
        w.Tools.ChooseMode(ToolMode.Select);
        Assert.False(w.BuildingInDungeon);
        Assert.False(w.PlaceTool.Building);
        Assert.Null(w.View.CutY);
        // The Place tool as it was: Ctrl + wheel is the view's.
        w.Tools.ChooseMode(ToolMode.Place);
        Assert.False(w.View.CtrlWheel!(1, false));
        // Another area ends it too.
        w.BuildInDungeon(true);
        w.Edit(s);
        Assert.False(w.BuildingInDungeon);
    }

    [AvaloniaFact]
    public void PiecesTheWorldCannotMakeAreLeftOutAndSaid()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            w.View.Show(scene, null);
            w.Edit(scene.Session!);
            var settings = new DungeonGen.Settings(Size: 1, Levels: 1, Seed: 12345);
            var r = DungeonGen.Make(settings);
            r.Items.Add(new DungeonGen.Item("no_such_piece_xyz", r.Items[0].Position, Quaternion.Identity));
            int things = scene.Things.Count;
            w.PlaceGenerated(settings, r);
            Assert.StartsWith($"Generated {r.Name}:", w.MessageText.Text);
            Assert.Contains("1 could not be made in this world.", w.MessageText.Text);
            Assert.DoesNotContain(scene.Things.Skip(things), t => t.Prefab == TerrainEditor.Save.StableHash.Of("no_such_piece_xyz"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task AGeneratedDungeonOpensOnThePlot()
    {
        using var r = new BlueprintsPanelRun();
        r.W.Ask = (_, _, _, _) => Task.FromResult(true);
        var settings = new DungeonGen.Settings(Size: 1, Levels: 1, Seed: 9, Name: "Plot crypt");
        await r.W.GeneratedOntoPlot(settings, DungeonGen.Make(settings), add: false);
        Assert.True(r.W.InWorkshop);
        Assert.True(File.Exists(Path.Combine(r.Homestead, Homestead.FileName("Plot crypt"))));
        Assert.Contains(r.W.View.Scene!.Things, t => !t.Gone);
    }

    [AvaloniaFact]
    public async Task AGeneratedDungeonThatCannotBeWrittenIsSaid()
    {
        string root = Path.Combine(Path.GetTempPath(), "vwe-dg-ui-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            // The library's folder is a file: it cannot be made.
            string folder = Path.Combine(root, "homestead");
            File.WriteAllText(folder, "not a folder");
            var (w, s) = Open();
            w.Blueprints.FindHomestead = () => new Homestead.Status(true, "1.3.2", ["test profile"], folder);
            var settings = new DungeonGen.Settings(Size: 1, Levels: 1, Seed: 5, Name: "Lost crypt");
            await w.GeneratedOntoPlot(settings, DungeonGen.Make(settings), add: false);
            Assert.StartsWith("Could not write the dungeon's blueprint: ", w.MessageText.Text);
            Assert.False(w.InWorkshop);
            Assert.Same(s, w.Session);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
