using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool with the mouse, looking straight down on a flat area: a double click in the View tool choosing it,
/// clicking and drawing zones, dragging, double-clicking a building or a ship (the example vehicle mod's), Same kind, Drop and snapping. The view's object boxes come from the pieces' own boxes
/// (headless tests draw nothing, so the view is given them).
/// </summary>
[Collection("World files")]
public class SelectToolMouseTests
{
    private static readonly int Wall = StableHash.Of("wood_wall_half"), Floor = StableHash.Of("wood_floor"), Beech = StableHash.Of("Beech1");

    /// <summary>
    /// The window, its edit session and the view's size, with the Select tool's mouse driven at world points.
    /// </summary>
    internal sealed record View(MainWindow W, EditSession S, Avalonia.Size Size)
    {
        public SelectTool Tool => W.View.SelectTool;

        /// <summary>
        /// Where a world point is on screen.
        /// </summary>
        public Avalonia.Point At(float x, float y, float z)
        {
            var q = Vector4.Transform(new Vector4(x - S.Scene.Cx, y, -(z - S.Scene.Cz), 1), W.View.ViewProj);
            return new Avalonia.Point((q.X / q.W + 1) / 2 * Size.Width, (1 - q.Y / q.W) / 2 * Size.Height);
        }

        /// <summary>
        /// A click at world point (x, z) on the ground, with Shift held and as many clicks as given.
        /// </summary>
        public void Click(float x, float z, bool shift = false, int clicks = 1)
        {
            var p = At(x, 30, z);
            Tool.Down(p, Size, shift, false, clicks);
            Tool.Up(p, Size, shift);
        }

        /// <summary>
        /// A drag along these world points on the ground, with Shift held or not.
        /// </summary>
        public void Drag(bool shift, params (float X, float Z)[] path)
        {
            Tool.Down(At(path[0].X, 30, path[0].Z), Size, shift, false, 1);
            foreach (var (x, z) in path.Skip(1))
            {
                Tool.Moved(At(x, 30, z), Size);
            }
            Tool.Up(At(path[^1].X, 30, path[^1].Z), Size, shift);
        }

        public HashSet<int> Selected => [.. W.View.Selected];

        public WorldScene.Thing Last => S.Scene.Things[^1];
    }

    /// <summary>
    /// A box around a thing in view space: a piece's own box, or 1 × 5 × 1 m for a tree.
    /// </summary>
    internal static (int, Vector3, Vector3) Box(WorldScene s, int i)
    {
        var t = s.Things[i];
        var info = PieceCatalog.Get(t.Prefab);
        var (x0, x1, z0, z1, y0, y1) = info != null ? (info.MinX, info.MaxX, info.MinZ, info.MaxZ, info.MinY, info.MaxY) : (-0.5f, 0.5f, -0.5f, 0.5f, 0f, 5f);
        var p = t.Position;
        return (i, new Vector3(p.X + x0 - s.Cx, p.Y + y0, -(p.Z + z1 - s.Cz)), new Vector3(p.X + x1 - s.Cx, p.Y + y1, -(p.Z + z0 - s.Cz)));
    }

    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects (given their boxes) in the Select tool,
    /// looking straight down on the middle.
    /// </summary>
    internal static View Open(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.View.SetBoxes(Enumerable.Range(0, things.Length).Select(i => Box(s.Scene, i)));
        // Straight down on the middle of the area (a little south, so north is up), 40 m around.
        var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
        w.View.SetCamera(c + new Vector3(0, 40, 0.01f), c, 1);
        return new View(w, s, w.View.Bounds.Size);
    }

    /// <summary>
    /// A thing of this kind at world point (x, y, z): a building piece, or a tree when not.
    /// </summary>
    internal static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool piece = true) => new(id, prefab, new Vector3(x, y, z), Vector3.Zero, piece ? 0 : 1, piece);

    // Shift + click: the row from the last object clicked (w1, Shift + click w3: w1, w2, w3), not what
    // stands off it; Ctrl + click adds one.
    [AvaloniaFact]
    public void ShiftClickTakesTheRowAndCtrlClickAddsOne()
    {
        var v = Open(T(1, Wall, 36, 30, 40), T(2, Wall, 38, 30, 40), T(3, Wall, 40, 30, 40), T(4, Wall, 38, 30, 33), T(5, Wall, 46, 30, 46));
        // From the east end (the move arrows point east and north of the first one, not over the others).
        v.Click(40, 40);
        Assert.Equal([2], v.Selected);
        v.Tool.Range = true;
        v.Click(36, 40, shift: true);
        v.Tool.Range = false;
        Assert.Equal([0, 1, 2], v.Selected);
        // Ctrl (add, no row): one more.
        v.Click(38, 33, shift: true);
        Assert.Equal([0, 1, 2, 3], v.Selected);
        // A plain click: that one alone.
        v.Click(46, 46);
        Assert.Equal([4], v.Selected);
    }

    [AvaloniaFact]
    public void AClickPicksAndAClickOnEmptyGroundDeselects()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Beech, 50, 30, 50, false));
        v.Click(40, 40);
        Assert.Equal([0], v.Selected);
        v.Click(50, 50, shift: true);
        Assert.Equal([0, 1], v.Selected);
        // Shift + click on a selected one takes it out.
        v.Click(50, 50, shift: true);
        Assert.Equal([0], v.Selected);
        v.Click(30, 30);
        Assert.Empty(v.Selected);
    }

    [AvaloniaFact]
    public void AZoneDrawnOnTheGroundSelectsWhatIsInside()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Wall, 55, 30, 55));
        v.Drag(false, (36, 36), (46, 36), (46, 44), (36, 44), (36, 37));
        Assert.Equal([0, 1], v.Selected);
        Assert.Null(v.Tool.Lasso);
        // Shift: added to what is selected.
        v.Drag(true, (52, 52), (58, 52), (58, 58), (52, 58));
        Assert.Equal([0, 1, 2], v.Selected);
        // A zone with nothing in it, with Shift, keeps the selection; a plain click on the ground clears it.
        v.Drag(true, (20, 20), (24, 20), (24, 24), (20, 24));
        Assert.Equal(3, v.Selected.Count);
        v.Click(25, 25, shift: true);
        Assert.Equal(3, v.Selected.Count);
    }

    [AvaloniaFact]
    public void AZoneIsCopiedForTheViewOnlyWhenItGetsAPoint()
    {
        // Every pointer move copied all the zone's points; a move that adds none now keeps the view's copy.
        var v = Open(T(1, Wall, 40, 30, 40));
        v.Tool.Down(v.At(36, 30, 36), v.Size, false, false, 1);
        v.Tool.Moved(v.At(46, 30, 36), v.Size);
        var drawn = v.Tool.Lasso;
        Assert.Equal(2, drawn!.Length);
        v.Tool.Moved(v.At(46.1f, 30, 36), v.Size);
        Assert.Same(drawn, v.Tool.Lasso);
        v.Tool.Moved(v.At(46, 30, 44), v.Size);
        Assert.Equal(3, v.Tool.Lasso!.Length);
    }

    [AvaloniaFact]
    public void EscapeCancelsAZoneBeingDrawn()
    {
        var v = Open(T(1, Wall, 40, 30, 40));
        v.Tool.Down(v.At(36, 30, 36), v.Size, false, false, 1);
        v.Tool.Moved(v.At(46, 30, 36), v.Size);
        v.Tool.Moved(v.At(46, 30, 44), v.Size);
        Assert.NotNull(v.Tool.Lasso);
        Assert.True(v.Tool.Key(Avalonia.Input.Key.Escape, false));
        Assert.Null(v.Tool.Lasso);
    }

    [AvaloniaFact]
    public void DraggingASelectedObjectMovesIt()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        v.Click(40, 40);
        v.Drag(false, (40, 40), (42, 40), (44, 41));
        Assert.True(v.S.Scene.Things[0].Gone);
        Assert.Equal(44, v.Last.Position.X, 1);
        Assert.Equal(41, v.Last.Position.Z, 1);
        // The copy is selected, and one undo puts the tree back.
        Assert.Equal([v.S.Scene.Things.Count - 1], v.Selected);
        v.S.Undo();
        Assert.False(v.S.Scene.Things[0].Gone);
    }

    [AvaloniaFact]
    public void ADraggedWallSnapsToTheWallNextToIt()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 50, 30, 40));
        v.Click(50, 40);
        // Its west end brought 0.4 m from the other wall's east end: it closes the gap.
        v.Drag(false, (50, 40), (46, 40), (42.4f, 40.2f));
        var moved = v.Last;
        Assert.Equal(42, moved.Position.X, 2);
        Assert.Equal(40, moved.Position.Z, 2);
        Assert.Equal(30, moved.Position.Y, 2);
        // With snapping off, it stays where it was dropped.
        v.S.Undo();
        v.Tool.SnapToPieces = false;
        v.Click(50, 40);
        v.Drag(false, (50, 40), (46, 40), (42.4f, 40.2f));
        Assert.Equal(42.4f, v.Last.Position.X, 1);
    }

    [AvaloniaFact]
    public void AWallDraggedFarSnapsToTheWallWhereItEnds()
    {
        // Two walls 80 m apart, seen from high enough for both to be on screen.
        var v = Open(T(1, Wall, 0, 30, 40), T(2, Wall, 80, 30, 40));
        var c = new Vector3(40 - v.S.Scene.Cx, 30, -(40 - v.S.Scene.Cz));
        v.W.View.SetCamera(c + new Vector3(0, 120, 0.01f), c, 1);
        v.Click(0, 40);
        Assert.Equal([0], v.Selected);
        // The first step of the drag gathers the pieces within 60 m (none); its east end is then brought 0.4 m
        // from the other wall's west end, 80 m away: it closes the gap.
        v.Drag(false, (0, 40), (4, 40), (77.6f, 40.2f));
        var moved = v.Last;
        Assert.Equal(78, moved.Position.X, 2);
        Assert.Equal(40, moved.Position.Z, 2);
    }

    [AvaloniaFact]
    public void ADoubleClickOnAnObjectInTheViewToolChoosesTheSelectToolWithItSelected()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40));
        v.W.Tools.ChooseMode(ToolMode.View);
        Assert.False(v.W.SelectPanel.Card.IsVisible);
        var p = v.At(40, 30, 40);
        for (int i = 0; i < 2; i++)
        {
            v.W.MouseDown(p, MouseButton.Left);
            v.W.MouseUp(p, MouseButton.Left);
        }
        Assert.Equal(ToolMode.Select, v.W.Tools.Mode);
        Assert.True(v.W.SelectPanel.Card.IsVisible);
        // The piece alone, not its building.
        Assert.Equal([0], v.Selected);
    }

    [AvaloniaFact]
    public void ADoubleClickSelectsTheWholeBuilding()
    {
        // Two walls end to end and a floor against them: one building; a wall further off is another.
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Floor, 41, 29.5f, 41.2f), T(4, Wall, 55, 30, 55), T(5, Beech, 43.5f, 30, 40, false));
        v.Click(40, 40, clicks: 2);
        Assert.Equal([0, 1, 2], v.Selected);
        // From the button too, adding to what is selected.
        v.Click(55, 55);
        v.Tool.WholeBuilding();
        Assert.Equal([3], v.Selected);
        // A tree is not a building.
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.W.View.Select([4]);
        v.Tool.WholeBuilding();
        Assert.StartsWith("Select a building piece first", said);
    }

    // A ship (the example vehicle mod's) (TestScenes.AddShip): a double click on a part selects the whole vehicle; a part dragged
    // takes it all along, kept afloat (its height as it was), every part stored at the root's new place; deleted, the
    // whole ship goes.
    [AvaloniaFact]
    public void AShipsPartBringsTheWholeShip()
    {
        var v = Open(T(1, Beech, 10, 30, 10, false));
        var ship = TestScenes.AddShip(v.S);
        v.W.View.SetBoxes(ship.Select(i => Box(v.S.Scene, i)));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.Click(40, 38, clicks: 2);
        Assert.Equal(ship.Order(), v.Selected.Order());
        Assert.Equal("Selected the whole vehicle: 5 part(s).", said);
        v.W.View.Select([]);
        v.Click(40, 38);
        Assert.Equal([ship[1]], v.Selected);
        // Exact place shows the whole ship's middle (as a move takes it); End drops nothing (a ship keeps its height).
        var (x, y, z, _, count) = v.Tool.Where()!.Value;
        Assert.Equal((5, 41.6f, 30f, 39.8f), (count, MathF.Round(x, 3), MathF.Round(y, 3), MathF.Round(z, 3)));
        v.Tool.Drop();
        Assert.Equal("Nothing to drop: a vehicle keeps its height.", said);
        v.Tool.Cancel();
        Assert.All(ship, i => Assert.False(v.S.Scene.Things[i].Gone));
        v.Drag(false, (40, 38), (43, 38), (45, 38));
        Assert.All(ship, i => Assert.True(v.S.Scene.Things[i].Gone));
        Assert.StartsWith("Moved 5 object(s) (a whole vehicle", said);
        var copies = Enumerable.Range(ship[^1] + 1, 5).ToList();
        Assert.Equal(copies, v.Selected.Order());
        var root = v.S.Scene.Things[Assert.Single(copies, i => v.S.Scene.Things[i].Prefab == TestScenes.ShipRoot)];
        Assert.Equal(45, root.Position.X, 1);
        Assert.Equal(40, root.Position.Z, 1);
        Assert.Equal(30, root.Position.Y, 3);
        Assert.All(copies, i => Assert.Equal(root.Position, v.S.Edits.FindAdded(v.S.Scene.Things[i].Id)!.Position));
        Assert.Contains(copies, i => Vector3.Distance(v.S.Scene.Things[i].Position, root.Position + new Vector3(4, 1, -1)) < 1e-3f);
        // One part deleted: the whole ship goes.
        v.W.View.Select([copies[0]]);
        v.Tool.Delete();
        Assert.All(copies, i => Assert.True(v.S.Scene.Things[i].Gone));
        Assert.StartsWith("Deleted 5 object(s) (a whole vehicle: a vehicle's parts never go alone)", said);
    }

    [AvaloniaFact]
    public void ADoubleClickOnAChestInABuildingSelectsTheChestAlone()
    {
        // The chest stands on the building's floor; its contents are what a double click is for.
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Floor, 41, 29.5f, 41.2f), T(3, StableHash.Of("piece_chest_wood"), 41, 29.6f, 41.6f));
        v.Click(41, 41.6f, clicks: 2);
        Assert.Equal([2], v.Selected);
        v.Click(40, 40, clicks: 2);
        Assert.Equal([0, 1, 2], v.Selected);
    }

    [AvaloniaFact]
    public void SameKindSelectsEveryShownObjectOfTheSelectedKinds()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Beech, 50, 30, 50, false), T(3, Wall, 45, 30, 45));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.Tool.SameKind();
        Assert.StartsWith("Select an object of each kind", said);
        v.W.View.Select([0]);
        v.Tool.SameKind();
        Assert.Equal([0, 1], v.Selected);
        // Hidden kinds are left out.
        v.W.View.SetShown(ObjectKinds.Of("Beech1", false), false);
        v.W.View.Select([0, 2]);
        v.Tool.SameKind();
        Assert.Equal([2], v.Selected);
    }

    [AvaloniaFact]
    public void DropPutsObjectsOnTheGroundOrOnWhatIsUnderThem()
    {
        // A tree floating 6 m up, and another above a floor.
        var v = Open(T(1, Beech, 40, 36, 40, false), T(2, Floor, 50, 31, 50), T(3, Beech, 50, 40, 50, false));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.W.View.Select([0, 2]);
        Assert.True(v.Tool.Key(Avalonia.Input.Key.End, false));
        Assert.Equal("Dropped 2 object(s): 1 onto other objects, 1 onto the ground.", said);
        v.Tool.Commit();
        var trees = v.S.Scene.Things.Where(t => !t.Gone && t.Prefab == Beech).OrderBy(t => t.Position.X).ToList();
        Assert.Equal(30, trees[0].Position.Y, 2);
        Assert.Equal(31 + PieceCatalog.Get(Floor)!.MaxY, trees[1].Position.Y, 2);
    }

    [AvaloniaFact]
    public void KeysLiftAndTurnTheSelection()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        v.W.View.Select([0]);
        v.Tool.OnGround = false;
        Assert.True(v.Tool.Key(Avalonia.Input.Key.PageUp, false));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.PageUp, true));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.PageDown, false));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.OemPeriod, false));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.OemComma, true));
        Assert.False(v.Tool.Key(Avalonia.Input.Key.Q, false));
        v.Tool.Commit();
        // +0.25 + 1 - 0.25 = +1 m; +1° then -15°.
        Assert.Equal(31, v.Last.Position.Y, 2);
        Assert.Equal(346, (v.Last.Rotation.Y % 360 + 360) % 360, 1);
    }

    [AvaloniaFact]
    public void DeleteWithNothingSelectedSaysSo()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.Tool.Delete();
        Assert.Equal("Select objects to delete first.", said);
        Assert.False(v.S.CanUndo);
    }
}
