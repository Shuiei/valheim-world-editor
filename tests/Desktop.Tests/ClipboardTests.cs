using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Copy and paste: an Area selection's ground and objects, or the Select tool's objects, pasted elsewhere, turned,
/// mirrored and repeated.
/// </summary>
public class ClipboardTests
{
    private static readonly int Beech = StableHash.Of("Beech1");

    /// <summary>
    /// The ground height at grid point (x, z).
    /// </summary>
    private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    /// <summary>
    /// A 2 × 2 zone area with a 3 m mound at grid (40, 40) and a tree on it (world = grid - 32).
    /// </summary>
    private static EditSession Area()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, Beech, new Vector3(8, 33, 8), new Vector3(0, 30, 0), 1, false));
        s.Shape(40, 40, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), 6, 3, "mound");
        return s;
    }

    /// <summary>
    /// An Area box between two grid points, with no soft edge.
    /// </summary>
    private static AreaTool Box(float x0, float z0, float x1, float z1)
    {
        var a = new AreaTool { Soft = 0 };
        a.Points.Add(new Vector2(x0, z0));
        a.Points.Add(new Vector2(x1, z1));
        return a;
    }

    /// <summary>
    /// Area() with a wall on the mound's top, beside the tree (thing 1).
    /// </summary>
    private static EditSession Walled()
    {
        var s = Area();
        s.Scene.Things.Add(new WorldScene.Thing(2, StableHash.Of("woodwall"), new Vector3(8, H(s, 40, 40) + 1, 8), Vector3.Zero, 0, true));
        return s;
    }

    // Copy takes the objects and the ground their buildings stand on: as it is under a piece and a metre
    // around, fading out over three more; the rest of the selection's ground stays behind. A tree alone
    // takes none. The Select tool's copy takes the same.
    [Fact]
    public void CopyTakesTheGroundUnderTheBuildingsOnly()
    {
        var s = Walled();
        var c = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0, 1], n => n == Beech ? "Beech1" : "woodwall")!;
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

        float ox = s.Scene.X0 * 64f - 32f, oz = s.Scene.Z0 * 64f - 32f;
        var sel = CopyData.FromThings(s.Scene, [1], _ => "woodwall", (x, z) => H(s, (int)MathF.Round(x - ox), (int)MathF.Round(z - oz)))!;
        Assert.Equal(1, sel.Wt[sel.W / 2 + sel.H / 2 * sel.W]);
        Assert.True(float.IsNaN(sel.Rel[0]));
        Assert.Equal([1], sel.Taken);
    }

    [Fact]
    public void PastingPutsTheBuildingWithItsGroundElsewhereInOneStep()
    {
        var s = Walled();
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0, 1], n => n == Beech ? "Beech1" : "woodwall") };
        var add = new List<(Editing.NewObject, bool)>();
        var added = s.Commit("Paste", g => { var (t, r, a) = paste.Apply(g, new Vector2(90, 90)); add.AddRange(a); return (t, r); }, [], add);
        // The ground under the wall is kept; further off, the flat ground stays.
        Assert.Equal(H(s, 40, 40) - H(s, 38, 40), H(s, 90, 90) - H(s, 88, 90), 2);
        Assert.Equal(30, H(s, 82, 90), 3);
        var tree = s.Scene.Things[added[0]];
        Assert.Equal(new Vector2(90 - 32, 90 - 32), new Vector2(tree.Position.X, tree.Position.Z));
        // It stood on the mound's top; it still does (the paste is relative to the ground clicked).
        Assert.Equal(H(s, 90, 90), tree.Position.Y, 2);
        Assert.Equal(1, s.Edits.Added[0].SourceId);
        Assert.Equal("Paste", s.UndoLabel);
        s.Undo();
        Assert.Equal(30, H(s, 90, 90), 3);
        Assert.True(s.Scene.Things[added[0]].Gone);
    }

    [Fact]
    public void TurningAndMirroringMoveTheObjectsAroundTheMiddle()
    {
        var p = new PasteTool();
        p.TurnBy(90);
        Assert.Equal(new Vector2(0, 1), Round(p.Xf(new Vector2(1, 0))));
        p.Mirror = true;
        Assert.Equal(new Vector2(0, -1), Round(p.Xf(new Vector2(1, 0))));
        Assert.Equal(new Vector2(1, 0), Round(p.Inv(p.Xf(new Vector2(1, 0)))));
        p.TurnBy(-100);
        Assert.Equal(350, p.Turn, 3);
    }

    /// <summary>
    /// Rounds to 3 decimals (with -0 made 0), to compare points.
    /// </summary>
    private static Vector2 Round(Vector2 v) => new(MathF.Round(v.X, 3) + 0f, MathF.Round(v.Y, 3) + 0f);

    [Fact]
    public void RepeatsGoAlongTheWidthOrUp()
    {
        var s = Area();
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0], _ => "Beech1"), Copies = 3, Gap = 2 };
        var (objs, outlines) = paste.Preview(new Vector2(40, 90), _ => 30);
        Assert.Equal(3, objs.Count);
        Assert.Equal([40f, 58f, 76f], objs.Select(o => MathF.Round(o.X)));
        paste.Direction = PasteTool.Along.Up;
        (objs, _) = paste.Preview(new Vector2(40, 90), _ => 30);
        Assert.Equal(objs[0].X, objs[2].X);
        Assert.True(objs[1].Y > objs[0].Y);
        Assert.Equal(3, outlines.Count);
    }

    [AvaloniaFact]
    public void CtrlCAndCtrlVInTheWindow()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = Area();
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
        w.View.Area.Points.Add(new Vector2(32, 32));
        w.View.Area.Points.Add(new Vector2(48, 48));
        w.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        Assert.NotNull(w.View.Paste.Clip);
        Assert.StartsWith("Copied 19 × 19 m and 1 object(s)", w.MessageText.Text);
        w.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
        Assert.Equal(ToolMode.Paste, w.View.Mode);
        Assert.True(w.PastePanel.Card.IsVisible);
        w.KeyPress(Key.R, RawInputModifiers.None, PhysicalKey.R, "r");
        Assert.Equal(90, w.View.Paste.Turn);
        Assert.Contains("turned 90°", w.PastePanel.Info.Text);
        int zones = s.Pending.Zones;
        w.PasteAt(new Vector2(90, 90));
        // A tree alone: no ground goes with it (the zone pending is the mound's).
        Assert.Equal((zones, 0, 1, 0), s.Pending);
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Equal(ToolMode.Area, w.View.Mode);
    }

    [AvaloniaFact]
    public void TheSelectToolCopiesObjectsThatFollowTheGround()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = Area();
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        w.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        var o = Assert.Single(w.View.Paste.Clip!.Objects);
        Assert.True(o.Follow);
        // On a flat place it lands at the ground plus what it stood above the mound.
        w.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
        w.PasteAt(new Vector2(100, 100));
        Assert.Equal(30 + (33 - H(s, 40, 40)), s.Scene.Things[^1].Position.Y, 2);
        // Objects only: the ground is not touched.
        Assert.Equal(30, H(s, 100, 100), 3);
    }

    // Clear the site: a building pasted into a mound digs the ground in its way down to its lowest
    // piece (a metre around too, never raising it) and takes away the tree there, not the one
    // further off; off, the ground and the tree stay. One undo step.
    [AvaloniaFact]
    public void PastingABuildingClearsItsSite()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, Beech, new Vector3(60 - 32, 33, 60 - 32), Vector3.Zero, 1, false),
            new WorldScene.Thing(2, Beech, new Vector3(80 - 32, 30, 80 - 32), Vector3.Zero, 1, false));
        s.Shape(60, 60, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), 6, 3, "mound");
        w.View.Show(s.Scene, null);
        w.Edit(s);
        float before = H(s, 60, 60), foot = H(s, 66, 60);
        // A wall 6 m west of the point clicked (its middle 1 m up: its bottom on the ground clicked).
        var wall = new CopyData
        {
            W = 1,
            H = 1,
            Rel = [float.NaN],
            Wt = [0f],
            Pnt = [-1f, -1, -1, -1],
            Objects = [new CopyData.Obj(StableHash.Of("woodwall"), "woodwall", -6, 0, 1, new Vector3(0, 90, 0), 0, null)],
            Poly = [new(-8, -2), new(2, -2), new(2, 2), new(-8, 2)],
            Name = "Wall",
        };
        w.View.Paste.Clip = wall;
        w.PasteAt(new Vector2(66, 60));
        Assert.Equal(foot, H(s, 60, 60), 2);
        Assert.True(before > foot + 2);
        Assert.True(s.Scene.Things[0].Gone);
        Assert.False(s.Scene.Things[1].Gone);
        Assert.Contains("cleared from the site", w.MessageText.Text);
        s.Undo();
        Assert.Equal(before, H(s, 60, 60), 3);
        Assert.False(s.Scene.Things[0].Gone);
        // Off: the wall goes in, the mound and the tree stay.
        w.PastePanel.ClearSiteBox.IsChecked = false;
        w.PasteAt(new Vector2(66, 60));
        Assert.Equal(before, H(s, 60, 60), 3);
        Assert.False(s.Scene.Things[0].Gone);
    }

    // The paste's ghost (drawn before clicking) is where the paste puts each object, lowered or raised
    // by Height (Ctrl + wheel in the window).
    [Fact]
    public void TheGhostIsWhereThePastePutsThem()
    {
        var s = Area();
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0], _ => "Beech1"), Turn = 90, Offset = -2 };
        float ox = s.Scene.X0 * 64f - 32f, oz = s.Scene.Z0 * 64f - 32f;
        var (prefab, position, rotation, scale) = Assert.Single(paste.Ghosts(new Vector2(90, 90), g => H(s, (int)MathF.Round(g.X), (int)MathF.Round(g.Y)), ox, oz));
        var (_, _, add) = paste.Apply(s.Ground, new Vector2(90, 90));
        var placed = Assert.Single(add).Item1;
        Assert.Equal(placed.Position, position);
        Assert.Equal(placed.Rotation, rotation);
        Assert.Equal(Beech, prefab);
    }

    // Copied with the Select tool on a slope: building pieces keep the building's shape (each piece's
    // height from the others), trees and the like keep their height above the ground where they land.
    [Fact]
    public void SelectCopiesKeepABuildingsShapeOnASlope()
    {
        int wall = StableHash.Of("woodwall");
        var s = Area();
        // On the mound's side: two walls one above the other, a tree beside them.
        float ox = s.Scene.X0 * 64f - 32f, oz = s.Scene.Z0 * 64f - 32f;
        float gy = H(s, 37, 40);
        s.Scene.Things.Add(new WorldScene.Thing(2, wall, new Vector3(37 + ox, gy + 1, 40 + oz), Vector3.Zero, 0, true));
        s.Scene.Things.Add(new WorldScene.Thing(3, wall, new Vector3(37 + ox, gy + 3, 40 + oz), Vector3.Zero, 0, true));
        float GroundAt(float x, float z) => H(s, (int)MathF.Round(x - ox), (int)MathF.Round(z - oz));
        var clip = CopyData.FromThings(s.Scene, [0, 1, 2], i => i == StableHash.Of("Beech1") ? "Beech1" : "woodwall", GroundAt)!;
        Assert.True(clip.Objects.Single(o => o.Name == "Beech1").Follow);
        Assert.All(clip.Objects.Where(o => o.Name == "woodwall"), o => Assert.False(o.Follow));
        var paste = new PasteTool { Clip = clip, Ground = false };
        var (_, _, add) = paste.Apply(s.Ground, new Vector2(90, 90));
        var walls = add.Select(a => a.Item1).Where(o => o.Prefab == wall).OrderBy(o => o.Position.Y).ToList();
        Assert.Equal(2, walls[1].Position.Y - walls[0].Position.Y, 3);
        var tree = add.Select(a => a.Item1).Single(o => o.Prefab == Beech);
        Assert.Equal(GroundAt(tree.Position.X, tree.Position.Z), tree.Position.Y, 2);
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

    // The copied ground goes in whole, past the game's ±8 m (No limit off): a wall copied on a 12 m
    // mound's top stands on that top, more than 8 m above the flat ground it is pasted on.
    [Fact]
    public void TheCopiedGroundGoesPastTheGamesLimit()
    {
        var s = TestScenes.Flat();
        s.Ground.NoLimit = true;
        s.Shape(40, 40, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), 8, 12, "mound");
        s.Ground.NoLimit = false;
        Assert.Equal(42, H(s, 40, 40), 1);
        s.Scene.Things.Add(new WorldScene.Thing(1, StableHash.Of("woodwall"), new Vector3(8, 43, 8), Vector3.Zero, 0, true));
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(30, 30, 50, 50), s.Ground, s.Scene, [0], _ => "woodwall") };
        s.Commit("Paste", g => { var (t, r, _) = paste.Apply(g, new Vector2(90, 90)); return (t, r); }, [], []);
        Assert.Equal(H(s, 40, 40) - H(s, 38, 40), H(s, 90, 90) - H(s, 88, 90), 1);
        Assert.True(H(s, 90, 90) > 38.5f);
        Assert.False(s.Ground.NoLimit);
    }

    // Clear the site leaves the copied ground alone (a wall set a metre into it stays so, no trench
    // dug round it) and pieces hidden far underground (a mod's stations) dig nothing.
    [Fact]
    public void ClearingTheSiteKeepsTheCopiedGroundAndSkipsHiddenPieces()
    {
        int wall = StableHash.Of("woodwall");
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, wall, new Vector3(40 - 32, 30, 40 - 32), Vector3.Zero, 0, true));
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(34, 34, 46, 46), s.Ground, s.Scene, [0], _ => "woodwall") };
        paste.Apply(s.Ground, new Vector2(90, 90));
        Assert.NotEmpty(paste.Site);
        Assert.Equal(30, s.Ground.HeightOf(90 * s.Ground.W + 90), 3);

        s.Shape(60, 60, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), 6, 3, "mound");
        float before = s.Ground.HeightOf(60 * s.Ground.W + 60);
        paste.Clip = new CopyData
        {
            W = 1,
            H = 1,
            Rel = [float.NaN],
            Wt = [0f],
            Pnt = [-1f, -1, -1, -1],
            Objects = [new CopyData.Obj(wall, "woodwall", 0, 0, -9000, Vector3.Zero, 0, null)],
            Poly = [new(-2, -2), new(2, -2), new(2, 2), new(-2, 2)],
        };
        paste.Apply(s.Ground, new Vector2(60, 60));
        Assert.Empty(paste.Site);
        Assert.Equal(before, s.Ground.HeightOf(60 * s.Ground.W + 60), 3);
    }

    // A paste that goes past the open area is refused: there the ground could not be pasted nor the
    // site cleared.
    [AvaloniaFact]
    public void APastePastTheOpenAreaIsRefused()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var s = Area();
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.View.Paste.Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0], _ => "Beech1");
        Assert.True(w.View.Paste.Fits(new Vector2(90, 90), s.Ground.W, s.Ground.H));
        Assert.False(w.View.Paste.Fits(new Vector2(5, 90), s.Ground.W, s.Ground.H));
        w.PasteAt(new Vector2(5, 90));
        Assert.StartsWith("Not pasted: it goes past the open area", w.MessageText.Text);
        Assert.Equal(0, s.Pending.Added);
    }

    // The ground goes in a whole metre at a time: the point pasted at moves (half a metre at most) so
    // that the copy's ground points land on the ground's, turned too, and its objects stand where they did.
    [Fact]
    public void ThePastePointLinesTheCopysGroundUp()
    {
        var clip = new CopyData { W = 2, H = 1, Rel = [0, 0], Wt = [1, 1], Pnt = [-1, -1, -1, -1, -1, -1, -1, -1], Objects = [], Poly = [new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)] };
        var paste = new PasteTool { Clip = clip };
        Assert.Equal(new Vector2(10.5f, 20), paste.CopyAt(new Vector2(10.3f, 20.2f), 0).At);
        paste.TurnBy(90);
        Assert.Equal(new Vector2(10, 20.5f), Round(paste.CopyAt(new Vector2(10.3f, 20.2f), 0).At));
    }
}
