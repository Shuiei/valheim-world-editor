using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaBoxes;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Copy: what a copy takes from the ground and the objects, what a copy keeps and how a copy of no outline repeats.
/// </summary>
public class ClipboardCopyTests
{
    // Copy takes the objects and the ground their buildings stand on: as it is under a piece and a metre
    // around, fading out over three more; the rest of the selection's ground stays behind. A tree alone
    // takes none. The Select tool's copy takes the same.
    [Fact]
    public void CopyTakesTheGroundUnderTheBuildingsOnly()
    {
        var s = Walled();
        var c = WalledCopy(s);
        Assert.Equal(19, c.W);
        Assert.Equal(2, c.Objects.Count);
        Assert.Equal([0, 1], c.Taken);
        var tree = c.Objects[0];
        Assert.Equal(new Vector2(0, 0), new Vector2(tree.Dx, tree.Dz));
        Assert.Equal(1, tree.SourceId);
        int mid = 9 * c.W + 9;
        Assert.Equal(1, c.Wt[mid]);
        Assert.Equal(H(s, 40, 40) - H(s, 39, 40), c.Rel[mid] - c.Rel[mid - 1], 2);
        Assert.InRange(c.Wt[9 * c.W + 13], 0.01f, 0.99f);
        Assert.True(float.IsNaN(c.Rel[9 * c.W + 1]));
        Assert.All(CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0], _ => "Beech1")!.Rel, r => Assert.True(float.IsNaN(r)));

        var (ox, oz) = GridOrigin(s);
        var sel = CopyData.FromThings(s.Scene, [1], _ => "woodwall", (x, z) => H(s, (int)MathF.Round(x - ox), (int)MathF.Round(z - oz)))!;
        Assert.Equal(1, sel.Wt[sel.W / 2 + sel.H / 2 * sel.W]);
        Assert.True(float.IsNaN(sel.Rel[0]));
        Assert.Equal([1], sel.Taken);
    }

    // Copies overlapping (a negative gap) each stand where the ghost shows them: every copy's height is read from the
    // ground before any copy shapes it, not from the ground the copy before it pasted.
    [Fact]
    public void OverlappingCopiesStandWhereTheGhostShowsThem()
    {
        var s = Walled();
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0, 1], _ => "Beech1"), Copies = 2 };
        var at = new Vector2(90, 90);
        // A step of one metre: the second copy's middle on the first copy's ground.
        paste.Gap = 1 - (paste.CopyAt(at, 1).At.X - paste.CopyAt(at, 0).At.X);
        Assert.Equal(1f, paste.CopyAt(at, 1).At.X - paste.CopyAt(at, 0).At.X, 3);
        var ghosts = GhostsAt(paste, s, at);
        var (_, _, add) = paste.Apply(s.Ground, at);
        Assert.Equal(ghosts.Select(g => g.Position), add.Select(a => a.Item1.Position));
    }

    // A pasted object keeps what its original holds (a sign's text, its builder), however long after
    // the copy it is pasted; saved as a blueprint, the builder is left out.
    [Fact]
    public void PastedObjectsKeepWhatTheOriginalsHold()
    {
        var s = TestScenes.Flat();
        var text = new Editing.ObjectField("strings", StableHash.Of("text"), "Hello");
        var builder = new Editing.ObjectField("longs", Editing.ObjectField.CreatorKey, "42");
        int sign = s.Commit("Sign", null, [], [(new Editing.NewObject(0, StableHash.Of("sign"), new Vector3(8, 31, 8), Vector3.Zero, 0, Data: [text, builder]), true)])[0];
        var clip = CopyData.FromThings(s.Scene, [sign], _ => "sign", (_, _) => 30)!;
        var (_, _, add) = new PasteTool { Clip = clip }.Apply(s.Ground, new Vector2(90, 90));
        var data = Assert.Single(add).Item1.Data!;
        Assert.Contains(text, data);
        Assert.Contains(builder, data);
        var kept = Assert.Single(clip.WithoutBuilders().Objects).Data!;
        Assert.Contains(text, kept);
        Assert.DoesNotContain(builder, kept);
    }

    // A copy without an outline (a blueprint that has none) is pasted and repeated by its grid's size: the step
    // between copies took the outline's and threw on an empty one.
    [Fact]
    public void ACopyWithoutAnOutlineRepeatsByItsSize()
    {
        var paste = new PasteTool { Clip = new CopyData { W = 3, H = 2, Rel = [.. Enumerable.Repeat(0f, 6)], Wt = [.. Enumerable.Repeat(1f, 6)], Pnt = [.. Enumerable.Repeat(-1f, 24)], Objects = [], Poly = [] } };
        Assert.Equal(paste.CopyAt(new Vector2(40, 40), 0).At + new Vector2(2 + paste.Gap, 0), paste.CopyAt(new Vector2(40, 40), 1).At);
    }
}
