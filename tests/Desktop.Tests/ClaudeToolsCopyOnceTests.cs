using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// One copy of a group of objects (ClaudeTools.CopyOnce): the k-th copy sits k steps from its original, and a copy that
/// would land outside the area names the copy and the object and refuses the whole copy.
/// </summary>
[Collection("World files")]
public class ClaudeToolsCopyOnceTests
{
    /// <summary>The id of the first object of the open area, as a one-item list (list_objects).</summary>
    private static async Task<List<int>> FirstObject(ClaudeRun r)
    {
        var ids = ClaudeRun.J(await r.T.ListObjects(limit: 1)).EnumerateArray().Select(e => e.GetProperty("id").GetInt32());
        return [.. ids];
    }

    /// <summary>The open area's scene, and the id of its first object.</summary>
    private static async Task<(WorldScene Scene, List<int> Ids)> OpenFirstObject(ClaudeRun r)
    {
        await r.OpenArea();
        return (r.W.Session!.Scene, await FirstObject(r));
    }

    [AvaloniaTheory]
    [InlineData(1, 1f)]
    [InlineData(2, 2f)]
    public async Task TheKthCopyIsKStepsFromItsOriginal(int k, float steps)
    {
        using var r = new ClaudeRun();
        var (scene, ok) = await OpenFirstObject(r);

        var (copy, problem) = r.T.CopyOnce(r.W.Session!, scene, ok, k, new(0f, 0f, 1f, 0f, 0f, 0f, false), ClaudeTools.Bounds(scene));

        Assert.Null(problem);
        var (obj, _, from) = Assert.Single(copy!);
        var original = scene.Things[from];
        Assert.Equal(original.Position.X + steps, obj.Position.X, 3);
        Assert.Equal(original.Position.Z, obj.Position.Z, 3);
    }

    [AvaloniaFact]
    public async Task ACopyOutsideTheAreaNamesItsCopyAndRefusesIt()
    {
        using var r = new ClaudeRun();
        var (scene, ok) = await OpenFirstObject(r);

        var (copy, problem) = r.T.CopyOnce(r.W.Session!, scene, ok, 1, new(0f, 0f, 1000f, 0f, 0f, 0f, false), ClaudeTools.Bounds(scene));

        Assert.Null(copy);
        Assert.StartsWith("Nothing copied: copy 1 of ", problem);
    }
}
