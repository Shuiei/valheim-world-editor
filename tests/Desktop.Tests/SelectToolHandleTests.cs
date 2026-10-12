using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's handles and clicks driven on the tool itself: the north arrow in half-metre steps, the up arrow,
/// drags the camera no longer lets follow, a handle on a runestone or after the move was committed, a plain click on a
/// selected object, double clicks on what is not a building, and lowering by a metre.
/// </summary>
public class SelectToolHandleTests
{
    private static readonly int Wall = StableHash.Of("wood_wall_half"), Beech = StableHash.Of("Beech1");

    /// <summary>
    /// The window, its edit session and the view's size, with points in view space put on screen through the camera
    /// set last.
    /// </summary>
    private sealed record View(MainWindow W, EditSession S, Avalonia.Size Size)
    {
        public SelectTool Tool => W.View.SelectTool;

        /// <summary>
        /// The middle of the first object, in view space.
        /// </summary>
        public Vector3 Centre => new(40 - S.Scene.Cx, 30, -(40 - S.Scene.Cz));

        /// <summary>
        /// Where a point in view space is on screen.
        /// </summary>
        public Avalonia.Point Screen(Vector3 p)
        {
            var q = Vector4.Transform(new Vector4(p, 1), W.View.ViewProj);
            return new Avalonia.Point((q.X / q.W + 1) / 2 * Size.Width, (1 - q.Y / q.W) / 2 * Size.Height);
        }

        /// <summary>
        /// The handles' scale for the camera set last.
        /// </summary>
        public float Scale => W.View.GizmoAt()!.Value.Scale;

        /// <summary>
        /// The camera looking at the first object from this offset.
        /// </summary>
        public void Look(Vector3 from) => W.View.SetCamera(Centre + from, Centre, 1);
    }

    /// <summary>
    /// A box around a thing in view space: a piece's own box, or 1 × 5 × 1 m otherwise.
    /// </summary>
    private static (int, Vector3, Vector3) Box(WorldScene s, int i)
    {
        var t = s.Things[i];
        var info = PieceCatalog.Get(t.Prefab);
        var (x0, x1, z0, z1, y0, y1) = info != null ? (info.MinX, info.MaxX, info.MinZ, info.MaxZ, info.MinY, info.MaxY) : (-0.5f, 0.5f, -0.5f, 0.5f, 0f, 5f);
        var p = t.Position;
        return (i, new Vector3(p.X + x0 - s.Cx, p.Y + y0, -(p.Z + z1 - s.Cz)), new Vector3(p.X + x1 - s.Cx, p.Y + y1, -(p.Z + z0 - s.Cz)));
    }

    /// <summary>
    /// A shown main window in the Select tool on a flat 2 × 2 zone area with these objects (given their boxes), the
    /// first one at (40, 30, 40) selected, looked at from above.
    /// </summary>
    private static View Open(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.View.SetBoxes(Enumerable.Range(0, things.Length).Select(i => Box(s.Scene, i)));
        w.View.Select([0]);
        var v = new View(w, s, w.View.Bounds.Size);
        v.Look(new Vector3(0, 30, 3));
        return v;
    }

    /// <summary>
    /// A thing of this kind at world point (x, y, z): a building piece, or not.
    /// </summary>
    private static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool piece = true) => new(id, prefab, new Vector3(x, y, z), Vector3.Zero, piece ? 0 : 1, piece);

    [AvaloniaFact]
    public void TheNorthArrowWithCtrlMovesInHalfMetres()
    {
        var v = Open(T(1, Wall, 40, 30, 40));
        float scale = v.Scale;
        var from = v.Screen(v.Centre + new Vector3(0, 0, -0.7f * scale));
        var to = v.Screen(v.Centre + new Vector3(0, 0, -0.7f * scale - 3.2f));
        v.Tool.Down(from, v.Size, false, false, 1);
        Assert.Equal(Gizmo.Handle.Z, v.Tool.HotHandle);
        v.Tool.Moved(to, v.Size, ctrl: true);
        v.Tool.Up(to, v.Size, false);
        var moved = v.S.Scene.Things[^1];
        Assert.True(v.S.Scene.Things[0].Gone);
        Assert.Equal(40, moved.Position.X, 1);
        Assert.Equal(43, moved.Position.Z, 1);
    }

    [AvaloniaFact]
    public void TheUpArrowLiftsAndIsLeftAloneLookingDownIt()
    {
        var v = Open(T(1, Wall, 40, 30, 40));
        // From the side, level with it.
        v.Look(new Vector3(0, 1, 30));
        float scale = v.Scale;
        v.Tool.Down(v.Screen(v.Centre + new Vector3(0, 0.7f * scale, 0)), v.Size, false, false, 1);
        Assert.Equal(Gizmo.Handle.Y, v.Tool.HotHandle);
        v.Tool.Moved(v.Screen(v.Centre + new Vector3(0, 0.7f * scale + 2, 0)), v.Size);
        // Looking straight down the arrow: no point along it, the lift kept.
        v.Look(new Vector3(0, 30, 0.01f));
        var down = v.Screen(v.Centre);
        v.Tool.Moved(down, v.Size);
        v.Tool.Up(down, v.Size, false);
        float[][]? snaps = PieceCatalog.Get(Wall)?.Snaps;
        float bottom = snaps is { Length: > 0 } ? snaps.Min(p => p[1]) : 0;
        Assert.Equal(32 - bottom, v.S.Scene.Things[^1].Position.Y, 1);
    }

    [AvaloniaFact]
    public void TheRingIsLeftAloneWhenTheMouseLooksAboveTheGround()
    {
        var v = Open(T(1, Wall, 40, 30, 40));
        float r = Gizmo.RingRadius * v.Scale;
        var from = v.Screen(v.Centre + new Vector3(0, 0, -r));
        v.Tool.Down(from, v.Size, false, false, 1);
        Assert.Equal(Gizmo.Handle.Ring, v.Tool.HotHandle);
        // Level with the ground, the pointer above it: the ray never meets the ground's plane.
        v.Look(new Vector3(0, 0, 30));
        var up = v.Screen(v.Centre + new Vector3(0, 5, 0));
        v.Tool.Moved(up, v.Size, ctrl: true);
        v.Tool.Up(up, v.Size, false);
        Assert.Single(v.S.Scene.Things);
        Assert.False(v.S.CanUndo);
    }

    [AvaloniaFact]
    public void AHandleOnARunestoneOrAfterTheMoveWasCommittedDoesNothing()
    {
        var stone = Open(new WorldScene.Thing(4, StableHash.Of("Runestone_Meadows"), new Vector3(40, 30, 40), Vector3.Zero, 1, false));
        float scale = stone.Scale;
        var east = stone.Screen(stone.Centre + new Vector3(0.7f * scale, 0, 0));
        stone.Tool.Down(east, stone.Size, false, false, 1);
        Assert.False(stone.Tool.Moving);
        Assert.Null(stone.Tool.HotHandle);

        var v = Open(T(1, Wall, 40, 30, 40));
        scale = v.Scale;
        east = v.Screen(v.Centre + new Vector3(0.7f * scale, 0, 0));
        v.Tool.Down(east, v.Size, false, false, 1);
        Assert.True(v.Tool.Moving);
        // Committed (as the keys' timer does) while the handle is held: the drag has no move left.
        v.Tool.Commit();
        var further = v.Screen(v.Centre + new Vector3(0.7f * scale + 3, 0, 0));
        v.Tool.Moved(further, v.Size);
        Assert.False(v.Tool.Moving);
        v.Tool.Up(further, v.Size, false);
        Assert.Single(v.S.Scene.Things);
    }

    [AvaloniaFact]
    public void APlainClickOnASelectedObjectSelectsItAlone()
    {
        // The second tree west of the first: the handles point east and north, away from it.
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Beech, 36, 30, 40, false));
        v.Look(new Vector3(0, 40, 0.01f));
        v.W.View.Select([0, 1]);
        var tree = v.Screen(v.Centre + new Vector3(-4, 0, 0));
        v.Tool.Down(tree, v.Size, false, false, 1);
        v.Tool.Up(tree, v.Size, false);
        Assert.Equal([1], v.W.View.Selected);
        Assert.False(v.Tool.Moving);
    }

    [AvaloniaFact]
    public void ADoubleClickOnATreeOrAnUnknownPiece()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, StableHash.Of("piece_nothing_known"), 46, 30, 40));
        v.Look(new Vector3(0, 40, 0.01f));
        v.W.View.Select([]);
        // A tree is not a building: it is just picked.
        var tree = v.Screen(v.Centre);
        v.Tool.Down(tree, v.Size, false, false, 2);
        v.Tool.Up(tree, v.Size, false);
        Assert.Equal([0], v.W.View.Selected);
        // A piece the catalogues do not know (no container): its building, which is itself.
        var piece = v.Screen(v.Centre + new Vector3(6, 0.5f, 0));
        v.W.View.Select([]);
        v.Tool.Down(piece, v.Size, false, false, 2);
        v.Tool.Up(piece, v.Size, false);
        Assert.Equal([1], v.W.View.Selected);
    }

    [AvaloniaFact]
    public void ShiftPageDownLowersByAMetre()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        string? said = null;
        v.Tool.Message += (t, _) => said = t;
        v.Tool.OnGround = false;
        Assert.True(v.Tool.Key(Key.PageDown, true));
        Assert.Equal("Moving 1 object(s): 0.0 m, -1.00 m", said);
        v.Tool.Commit();
        Assert.Equal(29, v.S.Scene.Things[^1].Position.Y, 2);
    }
}
