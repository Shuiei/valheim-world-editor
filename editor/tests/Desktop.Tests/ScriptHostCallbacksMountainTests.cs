using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.ScriptHostRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A script's Ground.Mountain (ScriptHost.Callbacks.Mountain): the trees it buries go, not those high above the ground
/// in a dungeon.
/// </summary>
public class ScriptHostCallbacksMountainTests
{
    [Fact]
    public void AMountainTakesAwayTheTreesItBuriesNotThoseInADungeonAbove()
    {
        var probe = TestScenes.Flat(3);
        float cx = probe.Scene.Cx, cz = probe.Scene.Cz;
        var s = TestScenes.Flat(3, TestScenes.Beech(1, new Vector3(cx, 30, cz)), TestScenes.Beech(2, new Vector3(cx, 5030, cz)));
        var ch = Run(s, "Ground.Mountain(Area.CenterX, Area.CenterZ, \"Mesa\", height: 30, radius: 60, seed: 4);");
        Assert.Contains(0, ch.Remove);
        Assert.DoesNotContain(1, ch.Remove);
    }
}
