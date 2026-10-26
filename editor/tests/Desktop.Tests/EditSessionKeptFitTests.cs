using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Fitting a kept history to an open area, one part at a time: which of the area's things a change refers to, its
/// points and zones read in the area, whether a change fits, straddles or is kept aside, and the things found
/// elsewhere that join the scene.
/// </summary>
public class EditSessionKeptFitTests
{
    /// <summary>
    /// A kept history over the session's own area and grid, with the changes given and no objects of their own.
    /// </summary>
    private static EditSession.Kept KeptOf(EditSession s, List<EditSession.Change> undo)
    {
        var ids = new Dictionary<EditSession.Change, int[]>();
        foreach (var c in undo)
        {
            ids[c] = [];
        }
        return new(undo, [], s.Scene.X0 * 64 - 32, s.Scene.Z0 * 64 - 32, s.Ground.W, s.Scene.X0, s.Scene.Z0, ids);
    }

    private static EditSession.Change ChangeOn(EditSession s, params int[] points) =>
        new("step", points, s.Ground.Snapshot(), s.Ground.Snapshot(), []);

    /// <summary>
    /// A beech (id 8) found <paramref name="dx"/> and <paramref name="dz"/> metres from the origin of a flat one-zone area.
    /// </summary>
    private static Dictionary<int, WorldScene.Thing> FoundBeech(float dx, float dz) => new() { [8] = TestScenes.BeechInFlat(8, dx, dz) };

    private static EditSession.KeptFit FitOf(EditSession s, EditSession.Kept k, Dictionary<int, int>? index = null,
        Dictionary<int, WorldScene.Thing>? found = null) => new(s, k, index ?? [], found ?? []);

    [Fact]
    public void AChangeInsideTheAreaFits()
    {
        var s = TestScenes.Flat(1);
        int p = 32 * s.Ground.W + 32;
        var c = ChangeOn(s, p);
        var fit = FitOf(s, KeptOf(s, [c]));
        var (standing, fitted) = fit.Fit(c);
        Assert.Equal(EditSession.Standing.Fits, standing);
        Assert.Equal(new[] { p }, fitted!.Points);
    }

    [Fact]
    public void AChangeOutsideTheAreaIsKeptAside()
    {
        var s = TestScenes.Flat(1);
        var c = ChangeOn(s, 0); // a locked edge point
        var (standing, fitted) = FitOf(s, KeptOf(s, [c])).Fit(c);
        Assert.Equal(EditSession.Standing.Outside, standing);
        Assert.Null(fitted);
    }

    [Fact]
    public void AChangePartlyInsideTheAreaStraddles()
    {
        var s = TestScenes.Flat(1);
        var c = ChangeOn(s, 0, 32 * s.Ground.W + 32);
        var (standing, fitted) = FitOf(s, KeptOf(s, [c])).Fit(c);
        Assert.Equal(EditSession.Standing.Straddles, standing);
        Assert.Null(fitted);
    }

    [Fact]
    public void ThingReturnsTheIndexOfAnObjectTheAreaHas()
    {
        var s = TestScenes.Flat(1);
        var fit = FitOf(s, KeptOf(s, []), index: new() { [7] = 3 });
        Assert.Equal(3, fit.Thing(7));
        Assert.Null(fit.Thing(8));
    }

    [Fact]
    public void ThingFindsAnObjectElsewhereInsideTheAreaAsPending()
    {
        var s = TestScenes.Flat(1);
        var found = FoundBeech(32, 32);
        var fit = FitOf(s, KeptOf(s, []), found: found);
        Assert.Equal(0, fit.Thing(8));
        Assert.Single(fit.Pending);
    }

    [Fact]
    public void ThingIgnoresAnObjectElsewhereOutsideTheArea()
    {
        var s = TestScenes.Flat(1);
        var found = FoundBeech(500, 32);
        var fit = FitOf(s, KeptOf(s, []), found: found);
        Assert.Null(fit.Thing(8));
        Assert.Empty(fit.Pending);
    }

    [Fact]
    public void PointsReadInTheAreasGridAndCountTheInside()
    {
        var s = TestScenes.Flat(1);
        int p = 32 * s.Ground.W + 32;
        var fit = FitOf(s, KeptOf(s, []));
        var frame = new EditSession.Frame(s.Scene.X0 * 64 - 32, s.Scene.Z0 * 64 - 32, s.Ground.W, s.Scene.X0, s.Scene.Z0);
        var (points, inside) = fit.Points([p, 0], frame);
        Assert.Equal(new[] { p, -1 }, points);
        Assert.Equal(1, inside);
    }

    [Fact]
    public void ZonesReadRelativeToTheAreaAndCountTheInside()
    {
        var s = TestScenes.Flat(2);
        var fit = FitOf(s, KeptOf(s, []));
        var frame = new EditSession.Frame(s.Scene.X0 * 64 - 32, s.Scene.Z0 * 64 - 32, s.Ground.W, s.Scene.X0, s.Scene.Z0);
        var (zones, inside) = fit.Zones([(0, 0), (5, 5)], frame);
        Assert.Equal(new[] { (0, 0), (5, 5) }, zones.ToArray());
        Assert.Equal(1, inside);
    }

    [Fact]
    public void AsideKeepsTheChangeWithItsFrameAndIds()
    {
        var s = TestScenes.Flat(1);
        var c = ChangeOn(s, 32 * s.Ground.W + 32);
        var k = KeptOf(s, [c]);
        var (change, frame, ids) = FitOf(s, k).Aside(c);
        Assert.Same(c, change);
        Assert.Equal(k.FrameOf(c), frame);
        Assert.Equal(k.Ids[c], ids);
    }

    [Fact]
    public void JoinFoundAddsThePendingThingsAndPointsTheStepAtThem()
    {
        var s = TestScenes.Flat(1);
        var found = FoundBeech(32, 32);
        var fit = FitOf(s, KeptOf(s, []), found: found);
        fit.Thing(8);
        var step = new EditSession.Change("join", [], s.Ground.Snapshot(), s.Ground.Snapshot(), [])
        {
            Things = [(fit.First, false, true)],
        };
        s.JoinFound([step], fit);
        Assert.Single(s.Scene.Things);
        Assert.Equal(0, step.Things[0].Index);
    }

    [Fact]
    public void RestoreOrderNumbersTheStepsAsTheyWereMade()
    {
        var s = TestScenes.Flat(1);
        var c1 = ChangeOn(s, 32 * s.Ground.W + 32);
        var c2 = ChangeOn(s, 33 * s.Ground.W + 33);
        var k = KeptOf(s, [c1, c2]);
        var undo = new List<(EditSession.Standing Standing, EditSession.Change? Fitted)>
        {
            (EditSession.Standing.Fits, c1),
            (EditSession.Standing.Fits, c2),
        };
        s.RestoreOrder(k, 2, 0, 0, -1, undo, []);
        Assert.Equal(0, s.OrderOf(c1));
        Assert.Equal(1, s.OrderOf(c2));
    }
}
