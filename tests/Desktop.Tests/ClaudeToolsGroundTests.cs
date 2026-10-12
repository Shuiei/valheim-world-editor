using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's Area tool ground actions, regrowing nature and reading the ground (ClaudeTools.Ground): what each refuses,
/// a rectangle raised and restored, smoothed and eroded as one step each, only the kinds asked regrown inside the
/// rectangle, and the ground's height at points; and the helpers they use (GroundActions, BoxOver).
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
}
