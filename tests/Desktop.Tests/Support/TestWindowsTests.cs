using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' window on a made-up area (TestWindows.Editing), tested once here so the tests using it can rely on
/// it: the window is shown and edits the flat area it was given, with its objects.
/// </summary>
public sealed class TestWindowsTests
{
    [AvaloniaFact]
    public void EditingShowsAWindowEditingTheArea()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 0, false);
        var (w, s) = TestWindows.Editing(tree);
        Assert.True(w.IsVisible);
        Assert.Same(s, w.Session);
        Assert.Same(s.Scene, w.View.Scene);
        Assert.Equal(2 * 64 + 1, s.Ground.W);
        Assert.Equal(30, s.Ground.HeightOf(0), 3);
        Assert.Equal(tree, Assert.Single(s.Scene.Things));
    }
}
