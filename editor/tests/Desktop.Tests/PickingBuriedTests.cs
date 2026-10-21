using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What is buried deep under the ground: more than 10 m under it, inside the area only.
/// </summary>
[Collection("World files")]
public class PickingBuriedTests
{
    [AvaloniaFact]
    public void OnlyWhatIsMoreThanTenMetresUnderTheGroundInsideTheAreaIsBuried()
    {
        var s = Open().S.Scene;
        // The test area's ground is flat at 30 m.
        Assert.False(Picking.Buried(s, new Vector3(40, 30, 40)));
        Assert.False(Picking.Buried(s, new Vector3(40, 20.5f, 40)));
        Assert.True(Picking.Buried(s, new Vector3(40, 19.5f, 40)));
        Assert.True(Picking.Buried(s, new Vector3(40, -9962, 40)));
        // Past the area's edge there is no ground to be under.
        Assert.False(Picking.Buried(s, new Vector3(s.Cx + 10_000, -9962, 40)));
    }
}
