using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The new objects' green dots leave out the selected ones (the selection's box marks them) and come back once they
/// are no longer selected.
/// </summary>
[Collection("World files")]
public class GlViewNewMarkersSelectionTests
{
    [AvaloniaFact]
    public void SelectedNewObjectsHaveNoDotUntilTheSelectionLeavesThem()
    {
        var v = Open(T(-1, Beech, 40, 30, 40, false), T(-2, Beech, 50, 30, 50, false));
        var view = v.W.View;
        var s = v.S.Scene;
        Assert.Equal(2, view.NewMarkers(s).Count);
        view.Select([0]);
        var dot = Assert.Single(view.NewMarkers(s));
        Assert.Equal(50 - s.Cx, dot.X, 2);
        view.Select([]);
        Assert.Equal(2, view.NewMarkers(s).Count);
    }
}
