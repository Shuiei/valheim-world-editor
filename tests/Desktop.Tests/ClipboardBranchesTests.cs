using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Copy and paste, remaining branches: an object with no known name, ground-only copies stacked upwards, and a scaled
/// building clearing its site under a mask.
/// </summary>
public class ClipboardBranchesTests
{
    [Fact]
    public void AnObjectWithNoKnownNameIsNamedByItsHash()
    {
        int prefab = StableHash.Of("Beech1");
        var s = TestScenes.Flat(1, new WorldScene.Thing(1, prefab, new Vector3(0, 31, 0), Vector3.Zero, 1, false));
        var c = CopyData.FromThings(s.Scene, [0], _ => null, (_, _) => 30);
        Assert.NotNull(c);
        var o = Assert.Single(c.Objects);
        Assert.Equal(prefab.ToString(System.Globalization.CultureInfo.InvariantCulture), o.Name);
        Assert.Equal(1, o.Dy, 3);
    }

    [Fact]
    public void GroundOnlyCopiesStackTwoMetresApart()
    {
        var clip = GroundOnlyClip(1, 1, [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)], "Bit");
        var paste = new PasteTool { Clip = clip, Copies = 2, Gap = 1, Direction = PasteTool.Along.Up };
        var (objs, outlines) = paste.Preview(new Vector2(30, 30), _ => 30);
        Assert.Empty(objs);
        Assert.Equal(2, outlines.Count);
    }

    [Fact]
    public void AScaledBuildingClearsItsSiteUnderAMask()
    {
        var s = TestScenes.Flat(2);
        s.Shape(60, 60, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), 6, 3, "mound");
        float before = s.Ground.HeightOf(60 * s.Ground.W + 60), foot = s.Ground.HeightOf(60 * s.Ground.W + 66);
        var wall = SingleObjectClip(new CopyData.Obj(StableHash.Of("woodwall"), "woodwall", -6, 0, 1, new Vector3(0, 90, 0), 1, null), [new(-8, -2), new(2, -2), new(2, 2), new(-8, 2)], "Wall");
        var (touched, _, add) = new PasteTool { Clip = wall }.Apply(s.Ground, new Vector2(66, 60), _ => 1f);
        Assert.Single(add);
        Assert.NotEmpty(touched);
        Assert.True(before > foot + 2);
        Assert.Equal(foot, s.Ground.HeightOf(60 * s.Ground.W + 60), 2);
    }
}
