using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map page shown again after a save: its header gives the world's new save number.
/// </summary>
[Collection("World files")]
public class MapPageLifecycleTests
{
    [AvaloniaFact]
    public async Task TheHeaderGivesTheSaveNumberAfterSaving()
    {
        using var r = new MapPageRun();
        await r.Open();
        var world = r.W.World!;
        int opened = world.World.SaveNumber;
        var s = WorldScene.Load(world, 0, 0, 1).Session!;
        s.Shape(10, 10, Formula.Compile("2", []), 3, 0, "raise");
        Assert.True(world.Save().Done);
        r.M.Show(world);
        var w = world.World;
        Assert.NotEqual(opened, world.SaveNumber);
        Assert.Equal(Lang.T("map.meta.file", w.Name, w.SeedName, world.SaveNumber, w.ObjectCount, w.ChunkCount), r.M.Meta.Text);
    }
}
