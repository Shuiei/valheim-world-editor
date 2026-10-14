using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's tools on a ship (TestScenes.AddShip): a part given brings the whole ship, moved, copied (each
/// copy a ship of its own) or taken away.
/// </summary>
[Collection("World files")]
public class ClaudeToolsVehiclesTests
{
    [AvaloniaFact]
    public async Task APartGivenBringsTheWholeShip()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        var ship = TestScenes.AddShip(s, 0, 0);

        var copied = ClaudeRun.J(await r.T.CopyObjects([ship[1]], dx: 10, copies: 2)).GetProperty("copied");
        Assert.Equal(10, copied.GetArrayLength());
        // Two copies, each with ids of its own: the ship's two (root, swivel) and two more each.
        Assert.Equal(6, s.ReadLinks().Ids.Count);

        var moved = ClaudeRun.J(await r.T.MoveObjects([ship[2]], dz: 3));
        Assert.Equal(5, moved.GetProperty("moved").GetArrayLength());
        Assert.Equal(1, moved.GetProperty("wholeGroups").GetInt32());
        int part = moved.GetProperty("moved")[0].GetProperty("id").GetInt32();

        Assert.StartsWith("Took 5 away, with 1 whole vehicle(s).", await r.T.RemoveObjects([part]));
        Assert.All(moved.GetProperty("moved").EnumerateArray(), m => Assert.True(s.Scene.Things[m.GetProperty("id").GetInt32()].Gone));
    }
}
