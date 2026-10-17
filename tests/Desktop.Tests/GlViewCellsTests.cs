using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The area's cells objects, dungeon rooms and location parts are culled by (GlView.PlacedBox, Grow, Runs, ViewPlanes,
/// InView): a cell's box holds the whole of what stands in it, so a part sticking out of the cell keeps it in view.
/// </summary>
public class GlViewCellsTests
{
    /// <summary>An empty cell's box: its low corner past its high one.</summary>
    private static readonly (Vector3 Min, Vector3 Max) Empty = (new Vector3(float.MaxValue), new Vector3(float.MinValue));

    /// <summary>A part standing at 120, 0, 64 whose model reaches 40 m along x: it sticks 32 m out of a 128 m cell.</summary>
    private static (Vector3 Min, Vector3 Max) LongPart() =>
        GlView.PlacedBox((new Vector3(-1, 0, -1), new Vector3(40, 5, 1)), Matrix4x4.CreateTranslation(120, 0, 64));

    /// <summary>A camera at <paramref name="eye"/> looking along x, as the 3D view sets it up.</summary>
    private static Vector4[] ViewAlongX(Vector3 eye)
    {
        var vp = Matrix4x4.CreateLookAt(eye, eye + Vector3.UnitX, Vector3.UnitY) * GlView.Perspective(60 * MathF.PI / 180, 1, 0.5f, 6000);
        var planes = new Vector4[6];
        GlView.ViewPlanes(vp, planes);
        return planes;
    }

    [Fact]
    public void ACellsBoxHoldsTheWholeOfAPartStickingOutOfIt()
    {
        (Vector3 Min, Vector3 Max)[] cells = [Empty, Empty];
        GlView.Grow(cells, 0, LongPart());
        Assert.Equal((new Vector3(119, 0, 63), new Vector3(160, 5, 65)), cells[0]);
        // The other cell is left empty; a second part only grows the box.
        Assert.Equal(Empty, cells[1]);
        GlView.Grow(cells, 0, (new Vector3(10, -2, 70), new Vector3(12, 1, 72)));
        Assert.Equal((new Vector3(10, -2, 63), new Vector3(160, 5, 72)), cells[0]);
    }

    [Fact]
    public void APartWhoseModelIsNotKnownCoversAMetreRoundWhereItStands()
    {
        Assert.Equal((new Vector3(119, -1, 63), new Vector3(121, 1, 65)), GlView.PlacedBox(null, Matrix4x4.CreateTranslation(120, 0, 64)));
    }

    [Fact]
    public void ACellIsInViewWhenOnlyThePartStickingOutOfItShows()
    {
        // The eye at 155 looks along x: only the part's last 5 m are in front of it, where it stands is behind.
        var planes = ViewAlongX(new Vector3(155, 2, 64));
        (Vector3 Min, Vector3 Max)[] cells = [Empty];
        GlView.Grow(cells, 0, GlView.PlacedBox(null, Matrix4x4.CreateTranslation(120, 0, 64)));
        Assert.False(GlView.InView(planes, cells[0].Min, cells[0].Max));
        GlView.Grow(cells, 0, LongPart());
        Assert.True(GlView.InView(planes, cells[0].Min, cells[0].Max));
    }

    [Fact]
    public void ACellWhosePartsAreAllOutOfViewIsNot()
    {
        // The eye at 200 looks along x, away from the whole part.
        var planes = ViewAlongX(new Vector3(200, 2, 64));
        var (min, max) = LongPart();
        Assert.False(GlView.InView(planes, min, max));
        // An empty cell never shows, nor does one beside the view.
        Assert.False(GlView.InView(planes, Empty.Min, Empty.Max));
        Assert.False(GlView.InView(planes, new Vector3(300, 0, 400), new Vector3(310, 5, 410)));
        Assert.True(GlView.InView(planes, new Vector3(300, 0, 60), new Vector3(310, 5, 70)));
    }

    [Fact]
    public void RunsFollowTheInstancesOrder()
    {
        // Unsorted (dungeon rooms, location parts): a run per stretch in one cell, the order kept.
        Assert.Equal([(3, 0, 2), (1, 2, 1), (3, 3, 1)], GlView.Runs([3, 3, 1, 3]));
        // Sorted (objects): one run per cell.
        Assert.Equal([(1, 0, 1), (3, 1, 3)], GlView.Runs([1, 3, 3, 3]));
        Assert.Empty(GlView.Runs([]));
    }
}
