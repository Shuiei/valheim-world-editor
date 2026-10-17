using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The overlay builder's steps one by one: the zone borders, the location posts, the wards and stations, the flatten
/// rings, and the lines it hands over.
/// </summary>
public class OverlayBuilderTests
{
    [Fact]
    public void ZoneBordersGoOnlyToTheBordersLayer()
    {
        var b = new OverlayBuilder(TestScenes.FlatScene());
        b.AddZoneBorders();
        var lines = b.Finish();
        Assert.NotEmpty(lines[Overlays.Layer.Borders]);
        Assert.Empty(lines[Overlays.Layer.Markers]);
        Assert.Empty(lines[Overlays.Layer.Wards]);
    }

    [Fact]
    public void NoWorldMeansNoLocationMarkers()
    {
        var b = new OverlayBuilder(TestScenes.FlatScene());
        Assert.Equal(0, b.AddLocationMarkers());
        Assert.Empty(b.Finish()[Overlays.Layer.Markers]);
    }

    [Fact]
    public void AWardAndAWorkbenchAreRingedAndCounted()
    {
        var things = new[]
        {
            new WorldScene.Thing(1, StableHash.Of("guard_stone"), new Vector3(0, 35, 0), Vector3.Zero, 0, true),
            new WorldScene.Thing(2, StableHash.Of("piece_workbench"), new Vector3(10, 35, 10), Vector3.Zero, 0, true),
        };
        string?[] names = ["guard_stone", "piece_workbench"];
        var b = new OverlayBuilder(TestScenes.FlatScene(things));
        Assert.Equal((1, 1), b.AddWardsAndStations(i => names[i]));
        var lines = b.Finish();
        Assert.NotEmpty(lines[Overlays.Layer.Wards]);
        Assert.NotEmpty(lines[Overlays.Layer.Stations]);
    }

    [Fact]
    public void UnknownNamesRingNothing()
    {
        var things = new[] { new WorldScene.Thing(1, StableHash.Of("guard_stone"), new Vector3(0, 35, 0), Vector3.Zero, 0, true) };
        var b = new OverlayBuilder(TestScenes.FlatScene(things));
        Assert.Equal((0, 0), b.AddWardsAndStations(_ => null));
        Assert.Empty(b.Finish()[Overlays.Layer.Wards]);
    }

    [Fact]
    public void NoModifiersFlattenNothing()
    {
        var b = new OverlayBuilder(TestScenes.FlatScene());
        Assert.Equal(0, b.AddFlatten(null));
        Assert.Empty(b.Finish()[Overlays.Layer.Flatten]);
    }

    [Fact]
    public void FinishGivesEveryLayerEvenEmptyOnes()
    {
        var lines = new OverlayBuilder(TestScenes.FlatScene()).Finish();
        Assert.Equal(Overlays.All.Length, lines.Count);
        Assert.All(lines.Values, Assert.Empty);
    }
}
