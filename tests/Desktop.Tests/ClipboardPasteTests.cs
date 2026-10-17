using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaBoxes;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Paste: where a paste puts the copy and its ground, the ghost, repeats, the site cleared and the limit.
/// </summary>
public class ClipboardPasteTests
{
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

    // The paste's ghost (drawn before clicking) is where the paste puts each object, lowered or raised
    // by Height (Ctrl + wheel in the window).
    [Fact]
    public void TheGhostIsWhereThePastePutsThem()
    {
        var s = Area();
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0], _ => "Beech1"), Turn = 90, Offset = -2 };
        var (ox, oz) = GridOrigin(s);
        var (prefab, position, rotation, scale) = Assert.Single(paste.Ghosts(new Vector2(90, 90), g => H(s, (int)MathF.Round(g.X), (int)MathF.Round(g.Y)), ox, oz));
        var (_, _, add) = paste.Apply(s.Ground, new Vector2(90, 90));
        var placed = Assert.Single(add).Item1;
        Assert.Equal(placed.Position, position);
        Assert.Equal(placed.Rotation, rotation);
        Assert.Equal(Beech, prefab);
    }

    // The copied ground goes in whole, past the game's ±8 m (No limit off): a wall copied on a 12 m
    // mound's top stands on that top, more than 8 m above the flat ground it is pasted on.
    [Fact]
    public void TheCopiedGroundGoesPastTheGamesLimit()
    {
        var s = TestScenes.Flat();
        s.Ground.NoLimit = true;
        Mound(s, 40, 40, 8, 12);
        s.Ground.NoLimit = false;
        Assert.Equal(42, H(s, 40, 40), 1);
        s.Scene.Things.Add(new WorldScene.Thing(1, StableHash.Of("woodwall"), new Vector3(8, 43, 8), Vector3.Zero, 0, true));
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(30, 30, 50, 50), s.Ground, s.Scene, [0], _ => "woodwall") };
        s.Commit("Paste", g => { var (t, r, _) = paste.Apply(g, new Vector2(90, 90)); return (t, r); }, [], []);
        Assert.Equal(H(s, 40, 40) - H(s, 38, 40), H(s, 90, 90) - H(s, 88, 90), 1);
        Assert.True(H(s, 90, 90) > 38.5f);
        Assert.False(s.Ground.NoLimit);
    }

    // A paste that fails part-way (a copy whose arrays are shorter than its size) leaves No limit as it was: it
    // stayed on, and every later brush went past the game's ±8 m.
    [Fact]
    public void AFailedPasteLeavesNoLimitAsItWas()
    {
        var s = TestScenes.Flat();
        var paste = new PasteTool { Clip = GroundOnlyClip(3, 3, [new(-1, -1), new(1, 1)]) };
        Assert.Throws<IndexOutOfRangeException>(() => paste.Apply(s.Ground, new Vector2(40, 40)));
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

        Mound(s, 60, 60, 6, 3);
        float before = s.Ground.HeightOf(60 * s.Ground.W + 60);
        paste.Clip = SingleObjectClip(new CopyData.Obj(wall, "woodwall", 0, 0, -9000, Vector3.Zero, 0, null), [new(-2, -2), new(2, -2), new(2, 2), new(-2, 2)]);
        paste.Apply(s.Ground, new Vector2(60, 60));
        Assert.Empty(paste.Site);
        Assert.Equal(before, s.Ground.HeightOf(60 * s.Ground.W + 60), 3);
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
