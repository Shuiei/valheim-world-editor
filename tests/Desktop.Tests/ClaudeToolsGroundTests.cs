using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's Area tool ground actions, regrowing nature and reading the ground (ClaudeTools.Ground): what each refuses,
/// a rectangle raised and restored, smoothed and eroded as one step each, only the kinds asked regrown inside the
/// rectangle, the ground's height at points, and rivers and caves along a line; and the helpers they use
/// (GroundActions, BoxOver, PathActions, LineOver).
/// </summary>
[Collection("World files")]
public class ClaudeToolsGroundTests
{
    // The actions by the names Claude gives, whatever the case.
    [Fact]
    public void TheActionsAreKnownByName()
    {
        Assert.Equal(AreaTool.GroundAction.Natural, ClaudeTools.GroundActions["Naturalize"]);
        Assert.Equal(AreaTool.GroundAction.Erode, ClaudeTools.GroundActions["erode"]);
        Assert.False(ClaudeTools.GroundActions.ContainsKey("paint"));
        Assert.Equal(6, ClaudeTools.GroundActions.Count);
    }

    // BoxOver: a world rectangle given corner to corner either way becomes the Area tool's box in grid points, with
    // its soft edge (none below 0).
    [AvaloniaFact]
    public async Task ARectangleBecomesTheAreaToolsBox()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var box = ClaudeTools.BoxOver(r.W.Session!.Scene, 10, 5, -10, -5, -2);
        Assert.Equal(0, box.Soft);
        Assert.Equal([new(22, 27), new(42, 27), new(42, 37), new(22, 37)], box.Polygon()!);
    }

    // A wrong action and a rectangle outside the area are said; raised then restored, the ground is back where it
    // was; smoothing and eroding are each one step named for Claude.
    [AvaloniaFact]
    public async Task GroundIsRaisedRestoredSmoothedAndEroded()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        Assert.Equal("The action is smooth, naturalize, erode, restore, raise or lower.", await r.T.ShapeGround("paint", 0, 0, 4, 4));
        Assert.StartsWith("The rectangle is outside the open area", await r.T.ShapeGround("smooth", 100, 100, 120, 120));
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);

        float H() => s.Ground.HeightOf(32 * s.Ground.W + 32);
        float was = H();
        Assert.StartsWith("Raise: ", await r.T.ShapeGround("raise", -10, -10, 10, 10, edge: 0, amount: 3));
        Assert.Equal(was + 3, H(), 2);
        Assert.Equal("Claude: raise", s.UndoLabel);
        await r.T.ShapeGround("restore", -10, -10, 10, 10, edge: 0);
        Assert.Equal(was, H(), 2);
        Assert.Equal("Claude: restore the ground", s.UndoLabel);

        Assert.StartsWith("Smooth: ", await r.T.ShapeGround("smooth", -20, -20, 20, 20));
        Assert.Equal("Claude: smooth", s.UndoLabel);
        // A steep block, 6 m high: its sides settle.
        await r.T.ShapeGround("raise", -2, -2, 2, 2, edge: 0, amount: 6);
        Assert.StartsWith("Erode: ", await r.T.ShapeGround("erode", -12, -12, 12, 12, edge: 0, restAngle: 20, seed: 4));
        Assert.Equal("Claude: erode", s.UndoLabel);
    }

    // The Workshop and a wrong kind are said; trees taken away come back as trees only, inside the rectangle.
    [AvaloniaFact]
    public async Task OnlyTheKindsAskedRegrowInside()
    {
        using var w = new ClaudeRun();
        await w.T.OpenWorkshop();
        Assert.Equal("Nature regrows in a world, not on the Workshop's plot.", await w.T.RegrowNature(0, 0, 10, 10));
        await w.OpenArea();
        Assert.Equal("Houses: the kinds are Trees, Rocks, Bushes and Pickables.", await w.T.RegrowNature(0, 0, 10, 10, ["Houses"]));
        int[] trees = [.. ClaudeRun.J(await w.T.ListObjects(kind: "Trees", limit: 2000)).EnumerateArray().Select(t => t.GetProperty("id").GetInt32())];
        Assert.NotEmpty(trees);
        await w.T.RemoveObjects(trees);
        int before = w.W.Session!.Scene.Things.Count;

        Assert.StartsWith("Regrew ", await w.T.RegrowNature(-25, -25, 0, 0, ["trees"]));
        var grown = w.W.Session.Scene.Things.Skip(before).ToList();
        Assert.NotEmpty(grown);
        Assert.All(grown, t => Assert.Equal(TerrainEditor.App.ObjectKind.Trees, TerrainEditor.App.ObjectKinds.Of(Terrain.PrefabCatalog.DisplayName(t.Prefab), false)));
        Assert.All(grown, t => Assert.True(t.Position.X is >= -25 and <= 0 && t.Position.Z is >= -25 and <= 0));
        Assert.StartsWith("Claude: regrew", w.W.Session.UndoLabel);
    }

    // ground_at: no area, no points or too many are said; a point outside is named; inside, the height now (raised
    // 2 m) against the generated one, and the biome.
    [AvaloniaFact]
    public async Task TheGroundIsReadAtPoints()
    {
        using var r = new ClaudeRun();
        Assert.StartsWith("No area is open", await r.T.GroundAtPoints([[0f, 0f]]));
        await r.OpenArea();
        Assert.Equal("Give 1 to 500 points, each [x, z].", await r.T.GroundAtPoints([]));
        Assert.Equal("Give 1 to 500 points, each [x, z].", await r.T.GroundAtPoints([[0f]]));
        await r.T.ShapeGround("raise", -5, -5, 5, 5, edge: 0, amount: 2);
        var at = ClaudeRun.J(await r.T.GroundAtPoints([[0f, 0f], [500f, 0f]]));
        var p = at[0];
        Assert.Equal(p.GetProperty("original").GetSingle() + 2, p.GetProperty("height").GetSingle(), 2);
        Assert.False(string.IsNullOrEmpty(p.GetProperty("biome").GetString()));
        Assert.True(p.GetProperty("water").GetSingle() >= 0);
        Assert.Equal("outside the area", at[1].GetProperty("error").GetString());
    }

    // The Path tool's actions by the names Claude gives; painting is not one of them.
    [Fact]
    public void ThePathActionsAreKnownByName()
    {
        Assert.Equal(PathTool.Action.River, ClaudeTools.PathActions["River"]);
        Assert.Equal(PathTool.Action.Cave, ClaudeTools.PathActions["cave"]);
        Assert.False(ClaudeTools.PathActions.ContainsKey("paintdirt"));
        Assert.Equal(7, ClaudeTools.PathActions.Count);
    }

    // LineOver: the world points in grid points; a ramp from the ground at its ends unless one height is given; a
    // cave's depth is the cave's, a river's the river's, each clamped.
    [AvaloniaFact]
    public async Task ALineBecomesThePathTool()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        float[][] line = [[-10f, 0f], [10f, 5f]];
        var ramp = ClaudeTools.LineOver(scene, line, PathTool.Action.Ramp, 6, 2, null, 4.5f, null, 2, false, true, PathTool.CaveRock.Auto, 3);
        Assert.Equal([new(22, 32), new(42, 37)], ramp.Points);
        Assert.Equal(ClaudeRun.J(await r.T.GroundAtPoints(line))[0].GetProperty("height").GetSingle(), ramp.Start, 0);
        Assert.Equal((6f, 2f, 3), (ramp.Width, ramp.Soft, ramp.Seed));
        var level = ClaudeTools.LineOver(scene, line, PathTool.Action.Ramp, 6, 2, null, 4.5f, 40, 2, false, true, PathTool.CaveRock.Auto, 3);
        Assert.Equal((40f, 40f), (level.Start, level.End));
        var cave = ClaudeTools.LineOver(scene, line, PathTool.Action.Cave, 6, 2, 100, 4.5f, null, 2, false, true, PathTool.CaveRock.Heath, 3);
        Assert.Equal((40f, PathTool.CaveRock.Heath), (cave.CaveDepth, cave.Rock));
        var river = ClaudeTools.LineOver(scene, line, PathTool.Action.River, 6, 2, null, 4.5f, null, 2, false, true, PathTool.CaveRock.Auto, 3);
        Assert.Equal(2, river.Depth);
    }

    // dig_path: wrong actions, lines and rocks are said; a river bed is dug down, one step named for Claude; a cave
    // is one step too.
    [AvaloniaFact]
    public async Task RiversAndCavesAreDugAlongALine()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        float[][] line = [[-20f, 0f], [20f, 0f]];
        Assert.Equal("The action is river, cave, ramp, flatten, raise, lower or smooth.", await r.T.DigPath("paint", line));
        Assert.Equal("A line needs at least 2 points, each [x, z].", await r.T.DigPath("river", [[0f, 0f]]));
        Assert.Equal("The rock is auto, forest, coast, heath or mountain.", await r.T.DigPath("cave", line, rock: "lava"));
        Assert.StartsWith("(500, 0) is outside the open area", await r.T.DigPath("river", [[0f, 0f], [500f, 0f]]));
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);

        float before = ClaudeRun.J(await r.T.GroundAtPoints([[0f, 0f]]))[0].GetProperty("height").GetSingle();
        Assert.Contains("Now pending:", await r.T.DigPath("river", line, width: 6, depth: 3));
        Assert.Equal("Claude: river along a line", s.UndoLabel);
        Assert.True(ClaudeRun.J(await r.T.GroundAtPoints([[0f, 0f]]))[0].GetProperty("height").GetSingle() < before);

        await r.T.DigPath("cave", [[-20f, 15f], [20f, 15f]], depth: 8, headroom: 4);
        Assert.Equal("Claude: cave along a line", s.UndoLabel);
    }
}
