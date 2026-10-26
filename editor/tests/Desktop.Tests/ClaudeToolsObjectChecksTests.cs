using System.Text.Json;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The checks the tools that change objects by their ids make before they do (ClaudeTools.Objects, Ids, TransformPlan,
/// Copies, ObjectInspect, ObjectValues, SearchResults): the start of a change, an id's refusal, a transform's move, a
/// copy's places, and what get_object reads of an object.
/// </summary>
[Collection("World files")]
public class ClaudeToolsObjectChecksTests
{
    // Opens the area and gives its scene with the id of its first object (list_objects).
    private static async Task<(WorldScene Scene, int Id)> OpenAreaWithFirstObject(ClaudeRun r)
    {
        await r.OpenArea();
        return (r.W.Session!.Scene, await FirstObjectId(r));
    }

    // The id of the first object the area lists (list_objects), which is then the id the ids given to a tool are.
    private static async Task<int> FirstObjectId(ClaudeRun r) =>
        ClaudeRun.J(await r.T.ListObjects(limit: 1))[0].GetProperty("id").GetInt32();

    // StartObjectsChange refuses first what no change may do now (no area open), then a pivot given with one coordinate,
    // then ids that are none of the area's objects; else the start of the change with the usable ids.
    [AvaloniaFact]
    public async Task AChangeStartsOnlyWithAnAreaAWholePivotAndUsableIds()
    {
        using var r = new ClaudeRun();
        Assert.Equal("No area is open: open_area (a world) or open_workshop first.", r.T.StartObjectsChange([0], null, null, "Nothing moved").Problem);
        await r.OpenArea();
        int id = await FirstObjectId(r);
        Assert.Equal("Give pivotX and pivotZ together, or neither: only pivotX was given.", r.T.StartObjectsChange([id], 1, null, "Nothing moved").Problem);
        var (none, nothing) = r.T.StartObjectsChange([-1, 1_000_000], null, null, "Nothing moved");
        Assert.Null(none);
        Assert.StartsWith("Nothing moved: ", nothing);
        var (start, problem) = r.T.StartObjectsChange([id, -1], null, null, "Nothing moved");
        Assert.Null(problem);
        Assert.Same(r.W.Session!.Scene, start!.Scene);
        Assert.Equal([id], start.Usable);
        Assert.NotEmpty(start.Problems);
    }

    // ObjectProblem: an id is refused when no object of the area has it (out of range, or taken away); a listed one is not.
    [AvaloniaFact]
    public async Task AnIdIsRefusedOnlyWhenNoObjectOfTheAreaHasIt()
    {
        using var r = new ClaudeRun();
        var (scene, id) = await OpenAreaWithFirstObject(r);
        const string Missing = "No object with that id in the area.";
        Assert.Null(r.T.ObjectProblem(scene, id));
        Assert.Equal(Missing, r.T.ObjectProblem(scene, -1));
        Assert.Equal(Missing, r.T.ObjectProblem(scene, scene.Things.Count));
        await r.T.RemoveObjects([id]);
        Assert.Equal(Missing, r.T.ObjectProblem(r.W.Session!.Scene, id));
    }

    // PlanTransform: an object's move keeps what the spec leaves out; a place outside the area or a scale out of range is refused.
    [AvaloniaFact]
    public async Task ATransformKeepsWhatItLeavesOutAndRefusesAreaAndScale()
    {
        using var r = new ClaudeRun();
        var (scene, id) = await OpenAreaWithFirstObject(r);
        var t = scene.Things[id];
        var area = ClaudeTools.Bounds(scene);
        var (keep, none) = r.T.PlanTransform(scene, new ClaudeTools.TransformSpec { Id = id }, area);
        Assert.Null(none);
        Assert.Equal(id, keep!.Value.Index);
        Assert.Equal(t.Position.X, keep.Value.Position.X);
        Assert.Equal(t.Position.Y, keep.Value.Position.Y);
        Assert.Equal(t.Scale, keep.Value.Scale);
        var (moved, outside) = r.T.PlanTransform(scene, new ClaudeTools.TransformSpec { Id = id, X = area.MaxX + 100 }, area);
        Assert.Null(moved);
        Assert.Contains("is outside the area", outside);
        var (small, bad) = r.T.PlanTransform(scene, new ClaudeTools.TransformSpec { Id = id, Scale = 0.01f }, area);
        Assert.Null(small);
        Assert.Equal($"{id}: a scale of 0.01 is not between 0.05 and 20", bad);
    }

    // PlanCopies: each copy is a step further, the originals in order for each; a copy outside the area refuses them all.
    [AvaloniaFact]
    public async Task CopiesStepFurtherAndOneOutsideTheAreaRefusesThemAll()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        var ok = ClaudeRun.J(await r.T.ListObjects(limit: 2)).EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        var area = ClaudeTools.Bounds(scene);
        var (adds, from, problem) = r.T.PlanCopies(r.W.Session!, scene, ok, 2, new(0f, 0f, 1f, 0f, 0f, 0f, false), area);
        Assert.Null(problem);
        Assert.Equal(2 * ok.Count, adds!.Count);
        Assert.Equal(ok.Concat(ok), from);
        var (none, _, far) = r.T.PlanCopies(r.W.Session!, scene, ok, 1, new(0f, 0f, 1000f, 0f, 0f, 0f, false), area);
        Assert.Null(none);
        Assert.StartsWith("Nothing copied: copy 1 of ", far);
        var (empty, _, nothing) = r.T.PlanCopies(r.W.Session!, scene, ok, 0, new(0f, 0f, 1f, 0f, 0f, 0f, false), area);
        Assert.Empty(empty!);
        Assert.Null(nothing);
    }

    // HeadOf: an object's name, kind, place, size and whether a player built it, keyed as get_object gives them.
    [AvaloniaFact]
    public async Task AnObjectsHeadNamesItsIdKindAndPlace()
    {
        using var r = new ClaudeRun();
        var (scene, id) = await OpenAreaWithFirstObject(r);
        var t = scene.Things[id];
        var head = r.T.HeadOf(id, t);
        Assert.Equal(id, head["id"]);
        Assert.NotNull(head["kind"]);
        Assert.Equal(t.Piece, head["built"]);
        Assert.Equal(MathF.Round(t.Position.X, 3), head["x"]);
    }

    // DataOf: a byte value is given by its size only, and a text value as it is.
    [Fact]
    public void AByteValueIsGivenBySizeAndATextValueAsItIs()
    {
        string bytes = JsonSerializer.Serialize(ClaudeTools.DataOf([("bytes", 12345, "AAAA")]));
        Assert.Contains("\"bytes\":3", bytes);
        string text = JsonSerializer.Serialize(ClaudeTools.DataOf([("strings", 1, "text")]));
        Assert.Contains("\"value\":\"text\"", text);
    }

    // AreaIdsOf: the things of the area by their world id, each with its index; none without an area.
    [AvaloniaFact]
    public async Task AreaIdsOfMapsEachThingsWorldIdToItsIndex()
    {
        Assert.Empty(ClaudeTools.AreaIdsOf(null));
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        var map = ClaudeTools.AreaIdsOf(scene);
        Assert.NotEmpty(map);
        Assert.All(map, kv => Assert.Equal(kv.Key, scene.Things[kv.Value].Id));
    }
}
