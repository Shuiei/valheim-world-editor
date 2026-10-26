using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script tool's edges (ScriptHost): a script with nothing to run, a run without debug information, every paint
/// (and one it does not know), locked and outside points left alone, and the Mountain helper's presets and the
/// trees it buries.
/// </summary>
public class ScriptHostEdgeTests
{
    /// <summary>The script compiled (or the test fails with its mistakes).</summary>
    private static (byte[] Image, byte[] Pdb) Compiled(string code)
    {
        var (image, pdb, errors) = ScriptHost.Compile(code);
        Assert.True(image != null, string.Join("\n", errors));
        return (image!, pdb!);
    }

    // No statements at all: a mistake that is not on one of the script's lines.
    [Fact]
    public void AnEmptyScriptHasNothingToRun()
    {
        var (image, pdb, errors) = ScriptHost.Compile("");
        Assert.Null(image);
        Assert.Null(pdb);
        Assert.NotEmpty(errors);
        Assert.DoesNotContain(errors, e => e.StartsWith("line ", StringComparison.Ordinal));
    }

    // Without its debug information a script still runs; its errors then have no line.
    [Fact]
    public void WithoutDebugInformationErrorsHaveNoLine()
    {
        var s = TestScenes.Flat(1);
        var (image, _) = Compiled("Print(\"hi\");\nObjects.Place(\"NoSuchThing\", 1, 1);\n");
        var ex = Assert.Throws<ArgumentException>(() => ScriptHost.Run(image, null, ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf), TestContext.Current.CancellationToken));
        Assert.Equal("", ScriptHost.Where(ex));
    }

    // Each paint's colour at its point; a locked point and one outside the area keep theirs; an unknown paint is a
    // mistake naming the paints.
    [Fact]
    public void EveryPaintAndThePointsItLeaves()
    {
        var s = TestScenes.Flat(1);
        var snap = ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf);
        bool[] locked = (bool[])snap.Locked.Clone();
        int Point(int x, int z) => (z + 32) * snap.W + x + 32;
        locked[Point(4, 4)] = true;
        snap = snap with { Locked = locked };
        var (image, pdb) = Compiled("""
			Ground.Paint(0, 0, "dirt");
			Ground.Paint(1, 0, "Cultivate");
			Ground.Paint(2, 0, "cultivated", 0.5f);
			Ground.Paint(3, 0, "paved");
			Ground.Paint(5, 0, "clear");
			Ground.Paint(6, 0, "none");
			Ground.Paint(4, 4, "paved");
			Ground.Paint(500, 0, "paved");
			""");
        var ch = ScriptHost.Run(image, pdb, snap, TestContext.Current.CancellationToken);
        Assert.Equal(6, ch.Paint.Count);
        Assert.False(ch.Paint.ContainsKey(Point(4, 4)));
        Assert.Equal(ch.Paint[Point(5, 0)], ch.Paint[Point(6, 0)]);
        float[] cultivated = Brush.PaintOf(BrushTool.PaintCultivated)!;
        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(cultivated[i], ch.Paint[Point(1, 0)][i], 4));
        // Half strength: half way from the ground's own paint to cultivated.
        float[] own = [.. Enumerable.Range(0, 4).Select(i => snap.Mask[Point(2, 0) * 4 + i] / 255f)];
        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal(own[i] + (cultivated[i] - own[i]) * 0.5f, ch.Paint[Point(2, 0)][i], 4));

        var (bad, badPdb) = Compiled("Ground.Paint(0, 0, \"lava\");");
        var ex = Assert.Throws<ArgumentException>(() => ScriptHost.Run(bad, badPdb, snap, TestContext.Current.CancellationToken));
        Assert.Contains("no paint “lava”", ex.Message);
    }

    // A preset the tool has not: a mistake listing them. A mountain made with no seed (another each time) still
    // rises; the trees and rocks it buries go, a player's piece stays.
    [Fact]
    public void MountainsBuryTreesAndNameTheirPresets()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 0, false);
        var far = new WorldScene.Thing(6, StableHash.Of("Beech1"), new Vector3(-30, 30, -30), Vector3.Zero, 0, false);
        var wall = new WorldScene.Thing(7, StableHash.Of("woodwall"), new Vector3(33, 31, 32), Vector3.Zero, 0, true);
        var s = TestScenes.Flat(2, tree, far, wall);
        var snap = ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf);
        var (image, pdb) = Compiled("Ground.Mountain(32, 32, \"Lone peak\", height: 40, radius: 30);");
        var ch = ScriptHost.Run(image, pdb, snap, TestContext.Current.CancellationToken);
        Assert.True(ch.Heights[(32 + 32) * snap.W + 32 + 32] > 60);
        Assert.Equal(0, Assert.Single(ch.Remove));

        var (bad, badPdb) = Compiled("Ground.Mountain(32, 32, \"Volcano island\");");
        var ex = Assert.Throws<ArgumentException>(() => ScriptHost.Run(bad, badPdb, snap, TestContext.Current.CancellationToken));
        Assert.Contains("no mountain preset “Volcano island”", ex.Message);
        Assert.Contains("Lone peak", ex.Message);
    }
}
