using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tests' window on an area (TestWindows.Editing), tested once here so the tests using it can rely on it: the
/// window is shown and edits the area it was given.
/// </summary>
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
}
