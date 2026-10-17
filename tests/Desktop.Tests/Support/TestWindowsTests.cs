using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' windows, tested once here so the tests using them can rely on them: on a made-up area
/// (TestWindows.EditingFlat) the window is shown and edits the flat area it was given, with its objects; on a copy of
/// the test world (TestWindows.ChangedWorld) it has the world open with one zone not saved.
/// </summary>
[Collection("World files")]
public sealed class TestWindowsTests
{
    [AvaloniaFact]
    public void EditingFlatShowsAWindowEditingTheArea()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 0, false);
        var (w, s) = TestWindows.EditingFlat(3, tree);
        Assert.True(w.IsVisible);
        Assert.Same(s, w.Session);
        Assert.Same(s.Scene, w.View.Scene);
        Assert.Equal(3 * 64 + 1, s.Ground.W);
        Assert.Equal(30, s.Ground.HeightOf(0), 3);
        Assert.Equal(tree, Assert.Single(s.Scene.Things));
    }

    [AvaloniaFact]
    public async Task ChangedWorldOpensTheWorldWithOneZoneNotSaved()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = await TestWindows.ChangedWorld(dir);
            Assert.True(w.IsVisible);
            Assert.NotNull(w.World);
            Assert.Equal((1, 0, 0, 0), w.World!.Pending);
            Assert.NotNull(w.Session);
        }
        finally
        {
            string folder = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
