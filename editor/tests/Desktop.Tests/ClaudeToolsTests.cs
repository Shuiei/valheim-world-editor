using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.ClaudePieces;
using static TerrainEditor.Desktop.Tests.ClaudeRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Claude tools over the editor: a world looked at and shaped with pending steps, scripts that need the user's leave,
/// live mode changing nothing, pieces snapping, the support check, a house built from rectangles, and the ready tasks.
/// </summary>
[Collection("World files")]
public class ClaudeToolsTests
{
    [AvaloniaFact]
    public async Task AWorldIsLookedAtAndShapedWithPendingSteps()
    {
        using var r = new ClaudeRun();
        var state = J(await r.T.EditorState());
        Assert.Equal("start", state.GetProperty("page").GetString());
        Assert.Equal("map", J(await r.T.OpenWorld(r.Dir)).GetProperty("page").GetString());
        var area = J(await r.T.OpenArea(0, 0, 1)).GetProperty("area");
        Assert.Equal(-32, area.GetProperty("minX").GetSingle());
        Assert.Equal(32, area.GetProperty("maxZ").GetSingle());
        var described = J(await r.T.DescribeArea());
        Assert.Contains(described.GetProperty("objectsByKind").EnumerateArray(), k => k.GetProperty("kind").GetString() == "Trees");
        var trees = J(await r.T.ListObjects(kind: "Trees", limit: 2));
        Assert.Equal(2, trees.GetArrayLength());
        Assert.Contains("Area", ClaudeTools.ScriptReference());
        // A script: one pending step, its prints and what it changed.
        string said = await r.T.RunScript("Ground.Raise(0, 0, 3); Objects.Place(\"Beech1\", 5, 5); Print(\"done\");", "a hill");
        Assert.StartsWith("done", said);
        Assert.Contains("1 object(s) placed", said);
        Assert.Equal((1, 0, 1, 0), (r.W.World!.Pending.Zones, r.W.World.Pending.Deleted, r.W.World.Pending.Added, r.W.World.Pending.Resets));
        Assert.Equal("Claude: a hill", r.W.Session!.UndoLabel);
        // Mistakes: which line, nothing changed.
        string wrong = await r.T.RunScript("Ground.Raise(0, 0);");
        Assert.Contains("line 1", wrong);
        Assert.Equal(1, r.W.World.Pending.Added);
        // Another world is not opened over unsaved changes.
        Assert.Contains("ask the user", await r.T.OpenWorld(r.Dir));
        Assert.StartsWith("Undone. Now pending: nothing", await r.T.Undo());
        Assert.Equal("Nothing to undo.", await r.T.Undo());
        Assert.StartsWith("Redone.", await r.T.Redo());
    }

    // Scripts run with the user's rights: without the user's leave (Settings → Claude → Claude may run scripts, off by
    // default) run_script is refused, dry runs too, and so is the Script tool's Run through set_control; with it,
    // they run. The other tools do not need it.
    [AvaloniaFact]
    public async Task ScriptsNeedTheUsersLeave()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        r.W.Settings.ClaudeScripts = false;
        Assert.Equal(ClaudeTools.ScriptsRefused, await r.T.RunScript("Ground.Raise(0, 0, 3);"));
        Assert.Equal(ClaudeTools.ScriptsRefused, await r.T.RunScript("Ground.Raise(0, 0, 3);", dryRun: true));
        Assert.Contains("not allowed Claude to run scripts", await r.T.SetControl("script.run", "click"));
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        Assert.Contains("ground point", await r.T.Flatten(-10, -10, 10, 10, 31));
        r.W.Settings.ClaudeScripts = true;
        Assert.StartsWith("Dry run:", await r.T.RunScript("Ground.Raise(0, 0, 3);", dryRun: true));
        Assert.Contains("ground point", await r.T.RunScript("Ground.Raise(0, 0, 3);"));
    }

    // With the leave, each script Claude wrote is shown to the user first: refused, nothing runs and Claude is told;
    // the same code is asked once (a dry run, then the run). The Script tool's Run pressed by Claude is asked too.
    // Scripts the editor writes for a tool (flatten…) are not asked.
    [AvaloniaFact]
    public async Task ClaudesScriptsAreShownToTheUserFirst()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var asked = new List<string>();
        bool answer = false;
        r.W.ConfirmClaudeScript = code =>
        {
            asked.Add(code);
            return Task.FromResult(answer);
        };
        Assert.Equal(ClaudeTools.ScriptRefusedByUser, await r.T.RunScript("Ground.Raise(0, 0, 3);"));
        Assert.Equal(["Ground.Raise(0, 0, 3);"], asked);
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        answer = true;
        Assert.StartsWith("Dry run:", await r.T.RunScript("Ground.Raise(0, 0, 3);", dryRun: true));
        Assert.Contains("ground point", await r.T.RunScript("Ground.Raise(0, 0, 3);"));
        Assert.Equal(2, asked.Count);
        Assert.Contains("ground point", await r.T.Flatten(-10, -10, 10, 10, 31));
        Assert.Equal(2, asked.Count);
        // The Script tool's Run, pressed by Claude: its code is shown too, and refused runs nothing.
        answer = false;
        r.W.ScriptPanel.CodeBox.Text = "Ground.Raise(5, 5, 2);";
        var before = r.W.World.Pending;
        await r.T.SetControl("script.run", "click");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Ground.Raise(5, 5, 2);", asked[^1]);
        Assert.Equal(before, r.W.World.Pending);
    }

    [AvaloniaFact]
    public async Task LiveWithAutoOnClaudeChangesNothing()
    {
        using var r = new ClaudeRun();
        await r.OpenLive();
        r.W.Settings.AutoApply = true;
        Assert.Contains("Auto on", await r.T.RunScript("Ground.Raise(0, 0, 3);"));
        Assert.Contains("Auto on", await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 0, Z = 0 }]));
        Assert.Contains("Auto on", await r.T.RemoveObjects([0]));
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        // Scripts not allowed either: that is what is said, since turning Auto off would not be enough.
        r.W.Settings.ClaudeScripts = false;
        Assert.Equal(ClaudeTools.ScriptsRefused, await r.T.RunScript("Ground.Raise(0, 0, 3);"));
        Assert.Contains("not allowed Claude to run scripts", await r.T.SetControl("script.run", "click"));
        r.W.Settings.ClaudeScripts = true;
        // Auto off: Claude's change waits for Apply live.
        r.W.Settings.AutoApply = false;
        Assert.Contains("ground point", await r.T.RunScript("Ground.Raise(0, 0, 3);"));
        Assert.Equal(1, r.W.World.Pending.Zones);
        Assert.Empty(r.Game!.Terrain);
    }

    [AvaloniaFact]
    public async Task PiecesSnapAsTheHammersAndTheSupportCheckNamesWhatFalls()
    {
        using var r = new ClaudeRun();
        var state = J(await r.T.OpenWorkshop());
        Assert.Equal("workshop", state.GetProperty("page").GetString());
        // A wall, then one 0.3 m off where it would join: it snaps to its edge. A floor up in the air
        // (not snapped) would fall. Unknown kinds and points off the plot are said, not placed.
        var placed = J(await r.T.PlacePieces(
        [
            new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 0, Z = 0 },
            new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 2.3f, Z = 0 },
            new ClaudeTools.PieceSpec { Prefab = "wood_floor", X = 10, Z = 10, Y = 40, Snap = false },
            new ClaudeTools.PieceSpec { Prefab = "no_such_piece", X = 0, Z = 0 },
            new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 500, Z = 0 },
        ], "walls"));
        var list = placed.GetProperty("placed").EnumerateArray().ToList();
        Assert.Equal(3, list.Count);
        var first = list[0].GetProperty("piece");
        Assert.Equal(Workshop.Ground + 1, first.GetProperty("y").GetSingle(), 2);
        var second = list[1].GetProperty("piece");
        Assert.True(second.GetProperty("snapped").GetBoolean());
        Assert.Equal(2, second.GetProperty("x").GetSingle(), 2);
        Assert.Equal(2, placed.GetProperty("problems").GetArrayLength());
        Assert.Equal("Claude: walls", r.W.Session!.UndoLabel);
        var support = J(await r.T.SupportCheck());
        Assert.Equal(1, support.GetProperty("wouldFall").GetInt32());
        int floating = list[2].GetProperty("id").GetInt32();
        Assert.Equal(floating, support.GetProperty("falling")[0].GetProperty("id").GetInt32());
        Assert.StartsWith("Took 1 away", await r.T.RemoveObjects([floating]));
        Assert.Equal(0, J(await r.T.SupportCheck()).GetProperty("wouldFall").GetInt32());
        // The plot is not saved as a blueprint: no world is opened over it.
        Assert.Contains("blueprint", await r.T.OpenWorld(r.Dir));
        Assert.Contains("No blueprint", await r.T.AddBlueprint("Nothing like this"));
    }

    // The ids support_check gives (the pieces falling, the weakest) are the plot's: usable at once, even when the
    // ids given before were another area's.
    [AvaloniaFact]
    public async Task SupportCheckGivesTheIdsOfThePlot()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "wood_pole2", X = 3, Z = 3 }]);
        await r.T.Undo();
        // A floor on the ground and a post 6 m above it, held by nothing: it falls.
        var clip = new System.Text.Json.Nodes.JsonObject { ["objects"] = new System.Text.Json.Nodes.JsonArray(Piece("wood_floor", 0, 0), Piece("wood_pole2", 0, 6)) };
        Directory.CreateDirectory(r.Homestead);
        File.WriteAllText(Path.Combine(r.Homestead, TerrainEditor.App.Homestead.FileName("Floating")), TerrainEditor.App.Homestead.Write(clip, "Floating", "Tester", null, DateTime.Now));
        Assert.Equal("workshop", J(await r.T.OpenWorkshop("Floating")).GetProperty("page").GetString());
        Assert.StartsWith("The ids are from another area", await r.T.RemoveObjects([0]));
        var support = J(await r.T.SupportCheck());
        Assert.Equal(1, support.GetProperty("wouldFall").GetInt32());
        int post = support.GetProperty("falling")[0].GetProperty("id").GetInt32();
        Assert.StartsWith("Took 1 away", await r.T.RemoveObjects([post]));
    }

    // Floors, walls and roofs over a rectangle, from the game's pieces: an 8 x 6 m house is 12 floor tiles,
    // 28 wall pieces with its door, 18 roof pieces with its gables (the 2 m under the ridge closed), and
    // it stands.
    [AvaloniaFact]
    public async Task AHouseIsBuiltFromRectanglesAndStands()
    {
        Assert.Equal(12, ClaudeBuilder.Floor(-4, -3, 4, 3, 34, "wood").Count);
        var walls = ClaudeBuilder.Walls(-4, -3, 4, 3, 34, 4, "wood", [new ClaudeBuilder.Door("south", 0)]);
        Assert.Equal(28, walls.Count);
        Assert.Single(walls, p => p.Prefab == "wood_door");
        var roof = ClaudeBuilder.Roof(-4, -3, 4, 3, 38, 45, "thatch", gables: true);
        Assert.Equal(18, roof.Count);
        Assert.Equal(4, roof.Count(p => p.Prefab == "wood_roof_top_45"));
        Assert.Throws<ArgumentException>(() => ClaudeBuilder.Roof(-4, -3, 4, 3, 38, 30, "thatch", true));
        Assert.Throws<ArgumentException>(() => ClaudeBuilder.Walls(-4, -3, 4, 3, 34, 4, "wood", [new ClaudeBuilder.Door("up", 0)]));
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        Assert.Contains("\"problems\":[]", await r.T.BuildFloor(-4, -3, 4, 3));
        Assert.Contains("\"problems\":[]", await r.T.BuildWalls(-4, -3, 4, 3, doors: [new ClaudeTools.DoorSpec { Side = "south", At = 0 }]));
        Assert.Contains("\"problems\":[]", await r.T.BuildRoof(-4, -3, 4, 3));
        var support = J(await r.T.SupportCheck());
        Assert.Equal(58, support.GetProperty("pieces").GetInt32());
        Assert.Equal(0, support.GetProperty("wouldFall").GetInt32());
        Assert.Contains("build the walls first", await r.T.BuildRoof(20, 20, 26, 26));
        // A rectangle going outside the plot (or not a number) is refused before any ground is read under it.
        Assert.StartsWith("Nothing built: the rectangle", await r.T.BuildFloor(0, 0, 5000, 4));
        Assert.StartsWith("Nothing built: the rectangle", await r.T.BuildWalls(-1.7e7f, 0, 1.7e7f, 4));
        Assert.StartsWith("Nothing built: the rectangle", await r.T.BuildRoof(0, 0, float.NaN, 4));
        Assert.Equal(58, J(await r.T.SupportCheck()).GetProperty("pieces").GetInt32());
    }

    // The ready tasks: one pending step each, as asked; a dry run changes nothing; what does not fit is
    // said. Selection both ways; a chest filled (and text only for signs); Claude's changes taken back,
    // the user's kept.
    [AvaloniaFact]
    public async Task ReadyTasksSelectionContentsAndTakingBack()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorld(r.Dir);
        await r.T.OpenArea(0, 0, 1);
        var s = r.W.Session!;
        Assert.StartsWith("Dry run:", await r.T.RunScript("Ground.Raise(0, 0, 2);", dryRun: true));
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        // The user's own change first.
        // (Shape takes the area's grid points, 0 to 64 across.)
        s.Shape(8, 56, Formula.Compile("1", []), 3, 0, "mine");
        await r.T.Flatten(-20, -20, 0, 0, height: 44);
        Assert.Equal(44, s.Scene.Heights[(int)(-10 - s.Scene.Cz + (s.Scene.H - 1) / 2f) * s.Scene.W + (int)(-10 - s.Scene.Cx + (s.Scene.W - 1) / 2f)], 1);
        Assert.Contains("painted", await r.T.Road([[-28f, 20f], [28f, 20f]]));
        Assert.Contains("The paint is", await r.T.PaintArea("lava", x: 0, z: 0, radius: 3));
        Assert.Contains("object(s) placed", await r.T.Forest(5, -30, 30, -5, ["Birch1"], spacing: 7));
        Assert.Contains("not an object", await r.T.Forest(5, -30, 30, -5, ["NoSuchTree"]));
        // Names go into the script escaped: one ending in a backslash, or holding a quote, is just not an object.
        Assert.Contains("Odd\\: not an object", await r.T.Forest(5, -30, 30, -5, ["Odd\\", "Birch1"]));
        Assert.Contains("Quote\"d: not an object", await r.T.Forest(5, -30, 30, -5, ["Quote\"d"]));
        await r.T.SelectObjects([1, 2], focus: false);
        Assert.Equal(2, J(await r.T.GetSelection()).GetArrayLength());
        int chest = J(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "piece_chest_wood", X = -10, Z = -10 }])).GetProperty("placed")[0].GetProperty("id").GetInt32();
        string filled = await r.T.SetContents(chest, [new ClaudeTools.ItemSpec { Item = "Coins", Stack = 50 }]);
        Assert.StartsWith("Changed", filled);
        // Changed, the chest is a new object: its id is the one given back.
        chest = int.Parse(filled[(filled.LastIndexOf(' ') + 1)..].TrimEnd('.'), System.Globalization.CultureInfo.InvariantCulture);
        // Stands: food lying flat, then emptied; an armour stand dressed and posed; what a stand refuses.
        int tray = J(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "itemstandh", X = -12, Z = -10 }])).GetProperty("placed")[0].GetProperty("id").GetInt32();
        string held = await r.T.SetContents(tray, [new ClaudeTools.ItemSpec { Item = "CookedMeat" }]);
        Assert.StartsWith("Changed", held);
        tray = int.Parse(held[(held.LastIndexOf(' ') + 1)..].TrimEnd('.'), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("CookedMeat", TerrainEditor.App.StandData.ReadItemStand(ZdoData.Parse(TerrainEditor.App.ObjectData.Bytes(s.Scene.World!, s.Edits, s.Scene.Things[tray].Id)!)).Item?.Item);
        Assert.Contains("cannot hang", await r.T.SetContents(tray, [new ClaudeTools.ItemSpec { Item = "Wood" }]));
        Assert.Contains("holds one item", await r.T.SetContents(tray, [new ClaudeTools.ItemSpec { Item = "CookedMeat" }, new ClaudeTools.ItemSpec { Item = "FishCooked" }]));
        Assert.StartsWith("Changed", await r.T.SetContents(tray, []));
        int dummy = J(await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "ArmorStand", X = -14, Z = -10 }])).GetProperty("placed")[0].GetProperty("id").GetInt32();
        Assert.StartsWith("Changed", await r.T.SetContents(dummy, [new ClaudeTools.ItemSpec { Item = "HelmetBronze" }, new ClaudeTools.ItemSpec { Item = "SwordIron", Quality = 2 }], pose: 1));
        Assert.Contains("not an item stand", await r.T.SetContents(chest, orientation: 1));
        Assert.Contains("not a sign", await r.T.SetContents(1, text: "hello"));
        Assert.Contains("not a creature", await r.T.SetContents(1, stars: 2));
        // Then the user again; Take back removes only Claude's.
        s.Shape(56, 56, Formula.Compile("1", []), 3, 0, "mine too");
        int before = s.UndoList.Count;
        r.W.History.Toggle();
        Assert.True(r.W.History.TakeBackClaude.IsVisible);
        await r.W.History.TakeBackClaudes();
        Assert.All(s.UndoList.Where(c => c.Label.StartsWith("Claude:", StringComparison.Ordinal)), c => Assert.True(c.Removed));
        Assert.Contains(s.UndoList, c => c.Label == "mine" && !c.Removed);
        Assert.Contains(s.UndoList, c => c.Label == "mine too" && !c.Removed);
        Assert.False(r.W.History.TakeBackClaude.IsVisible);
        Assert.True(s.UndoList.Count >= before);
    }
}
