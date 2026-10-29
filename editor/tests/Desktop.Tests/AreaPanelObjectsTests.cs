using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The objects inside the Area panel's selection: those buried deep under the ground are left out, but for a backup
/// restore, which compares with everything there.
/// </summary>
[Collection("World files")]
public class AreaPanelObjectsTests
{
    [AvaloniaFact]
    public void ObjectsBuriedDeepUnderTheGroundAreLeftOutButForARestore()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Wall, 41, -9962, 40));
        List<Vector2> all = [new(-1000, -1000), new(1000, -1000), new(1000, 1000), new(-1000, 1000)];
        Assert.Equal([0], v.W.AreaPanel.ThingsInside(all, chosenKindsOnly: false));
        Assert.Equal([0, 1], v.W.AreaPanel.ThingsInside(all, chosenKindsOnly: false, includeHidden: true));
    }
}
