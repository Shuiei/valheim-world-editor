using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The picking steps behind a click on the 3D view: the boxes a ray enters, nearest first (only the known and shown
/// ones); the row a Shift + click makes needs an earlier click; with no mesh met nothing is hit; and with the Workshop's
/// cut, a stand-in box is met where the ray enters it under the cut.
/// </summary>
public class GlViewPickBoxesTests
{
    /// <summary>A thing standing at the origin, the first of its kind.</summary>
    private static WorldScene.Thing Rock(int id) => new(id, StableHash.Of("Rock_Test"), new Vector3(0, 0, 0), Vector3.Zero, 0, false);

    /// <summary>A view on a flat area with three things, every kind shown, and each thing's box set in view space.</summary>
    private static (GlView View, WorldScene Scene) Boxed((int Index, Vector3 Min, Vector3 Max)[] boxes)
    {
        var s = TestScenes.Flat(2, Rock(1), Rock(2), Rock(3));
        var v = new GlView();
        v.Show(s.Scene, null);
        foreach (var k in ObjectKinds.All)
        {
            v.SetShown(k, true);
        }
        v.SetBoxes(boxes);
        return (v, s.Scene);
    }

    [AvaloniaFact]
    public void BoxesTheRayEntersComeNearestFirstAndTheOnesItMissesAreLeftOut()
    {
        var (v, _) = Boxed(
        [
            (0, new Vector3(-1, 0, 20), new Vector3(1, 2, 22)),
            (1, new Vector3(-1, 0, 10), new Vector3(1, 2, 12)),
            (2, new Vector3(49, 0, 0), new Vector3(51, 2, 2)),
        ]);
        var hits = v.BoxesHitBy(new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        Assert.Equal([1, 0], hits.Select(h => h.I));
        Assert.Equal(10f, hits[0].T, 3);
        Assert.Equal(20f, hits[1].T, 3);
    }

    [AvaloniaFact]
    public void ARowNeedsAnEarlierClickAndAThingToStartFrom()
    {
        var (v, s) = Boxed([(0, new Vector3(-1, 0, 20), new Vector3(1, 2, 22))]);
        Assert.Null(v.RangeEnds(s, 0));
        Assert.Null(v.RangeEnds(s, null));
    }

    [AvaloniaFact]
    public void WithNoBoxesToMeetNothingIsHitAndTheNearestIsFar()
    {
        var (v, s) = Boxed([]);
        var (hit, best) = v.NearestMeshHit(s, new Vector3(0, 1, 0), new Vector3(0, 0, 1), []);
        Assert.Null(hit);
        Assert.Equal(float.MaxValue, best);
    }

    [AvaloniaFact]
    public void AStandInBoxIsTheBoxWhenThereIsNoCutAndTheCutsHitWhenThereIs()
    {
        var (min, max) = (new Vector3(-1, 0, -1), new Vector3(1, 2, 1));
        var (v, _) = Boxed([(0, min, max)]);
        var o = new Vector3(0, 10, 0);
        var d = new Vector3(0, -1, 0);
        Assert.Equal(4f, v.BoxUnderCut(0, o, d, 4f, null));
        Assert.Equal(Picking.HitBox(o, d, min, max, 1.5f), v.BoxUnderCut(0, o, d, 4f, 1.5f));
    }
}
