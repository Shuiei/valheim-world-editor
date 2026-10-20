using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Clicking again at the same spot takes the next object behind (as Unity's Scene view): looking straight down on a
/// floor with another 4 m above it, clicks there take the upper, the lower, the upper again; a click elsewhere starts
/// over from the nearest.
/// </summary>
[Collection("World files")]
public class GlViewPickingCycleTests
{
    [AvaloniaFact]
    public void ClicksAtTheSameSpotGoThroughWhatIsUnderIt()
    {
        var v = Open(T(1, Floor, 40, 30, 40), T(2, Floor, 40, 34, 40), T(3, Beech, 46, 30, 46, false));
        Assert.Equal([1, 0], v.W.View.ObjectsAt(v.At(40, 30, 40), v.Size));
        v.Click(40, 40);
        Assert.Equal([1], v.Selected);
        v.Click(40, 40);
        Assert.Equal([0], v.Selected);
        v.Click(40, 40);
        Assert.Equal([1], v.Selected);
        // Elsewhere, then back: the nearest again.
        v.Click(46, 46);
        Assert.Equal([2], v.Selected);
        v.Click(40, 40);
        Assert.Equal([1], v.Selected);
        // With a key held, no cycling: Ctrl toggles the nearest.
        v.Click(40, 40, KeyModifiers.Control);
        Assert.Empty(v.Selected);
    }

    [AvaloniaFact]
    public void NothingUnderThePointerIsNoObject()
    {
        var v = Open(T(1, Floor, 40, 30, 40));
        Assert.Empty(v.W.View.ObjectsAt(v.At(60, 30, 60), v.Size));
        v.Click(40, 40);
        v.Click(60, 60);
        Assert.Empty(v.Selected);
    }
}
