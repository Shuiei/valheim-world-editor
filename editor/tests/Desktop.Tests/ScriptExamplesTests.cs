using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.ScriptHostRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script tool's examples (ScriptExamples): Terraced hill takes away the trees, rocks and bushes it buries, and
/// nothing else.
/// </summary>
public class ScriptExamplesTests
{
    [Fact]
    public void TerracedHillTakesAwayOnlyTheNatureItBuries()
    {
        var probe = TestScenes.Flat(3);
        float cx = probe.Scene.Cx, cz = probe.Scene.Cz;
        var boar = new WorldScene.Thing(3, StableHash.Of("Boar"), new Vector3(cx + 5, 30, cz), Vector3.Zero, 1, false);
        var s = TestScenes.Flat(3, TestScenes.Beech(1, new Vector3(cx, 30, cz)), TestScenes.Beech(2, new Vector3(cx, 5030, cz)), boar);
        var ch = Run(s, ScriptExamples.All.Single(e => e.Name == "Terraced hill").Code);
        // The tree on the ground; not the one in a dungeon high above, nor the boar.
        Assert.Equal(0, Assert.Single(ch.Remove));
    }
}
