using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map page shown again after a save: its header gives the world's new save number; another world shown forgets
/// the pointer's line of the last one.
/// </summary>
[Collection("World files")]
public class MapPageLifecycleTests
{
    [Theory]
    [InlineData("live http://127.0.0.1:5182", "http://127.0.0.1:5182")]
    [InlineData("My server", "My server")]
    [InlineData("lively", "lively")]
    public void TheHeaderNamesALiveWorldWithoutSayingLiveTwice(string label, string shown) =>
        Assert.Equal(shown, MapPage.LiveName(label));

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

    [AvaloniaFact]
    public async Task AnotherWorldShownForgetsThePointersLine()
    {
        using var r = new MapPageRun();
        await r.Open();
        Assert.NotEqual("", r.M.HoverAt(100, -41));
        using var other = WorldSession.Open(r.Dir);
        try
        {
            r.M.Show(other);
            Assert.Equal("", r.M.Cursor.Text);
        }
        finally
        {
            r.M.Forget();
        }
    }
}
