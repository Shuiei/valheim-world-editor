using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' window on an area (TestWindows.Editing), tested once here so the tests using it can rely on it: the
/// window is shown and edits the area it was given.
/// </summary>
[Collection("World files")]
public sealed class TestWindowsTests
{
    [AvaloniaFact]
    public void EditingShowsAWindowEditingTheArea()
    {
        var s = TestScenes.Flat(2);
        var w = TestWindows.Editing(s);
        Assert.True(w.IsVisible);
        Assert.Same(s, w.Session);
        Assert.Same(s.Scene, w.View.Scene);
    }

    [AvaloniaFact]
    public async Task EditingWorldOpensTheWorldAndEditsTheAreaGiven()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = await TestWindows.EditingWorld(dir, 0, 0, 2);
            Assert.True(w.IsVisible);
            Assert.NotNull(w.Session);
            Assert.Equal(2, w.Session!.Scene.ZonesX);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
