using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's mouse and keys through an input of its own on a shown window, looking straight down on a flat
/// 2 × 2 zone area: picking a kind or a height, brush strokes (painting, erasing, nothing under the brush, no area
/// open), building one piece where the ray points, lines clicked off the area or double-clicked away from their end,
/// freehand lines, turned zones edited, grid boxes, circles and rectangles let go off the area, the keys, and
/// everything again with nobody listening.
/// </summary>
public class PlaceInputTests
{
    private const string Wall = "woodwall";

    /// <summary>A point of the view off the area (its top left corner): no ground under it.</summary>
    private static readonly Avalonia.Point Sky = new(2, 2);

    /// <summary>
    /// A shown window editing a flat 2 × 2 zone area in the Place tool, looking straight down, with a Place tool and
    /// input of the test's own (Beech1 chosen, the given mode), its messages and placed kinds kept when listened to.
    /// </summary>
    private sealed class Rig
    {
        public MainWindow W { get; }
        public EditSession S { get; }
        public PlaceTool T { get; }
        public PlaceInput In { get; }
        public Avalonia.Size Size { get; } = new(1600, 1000);
        public List<string> Said { get; } = [];
        public List<IReadOnlyList<string>> Placed { get; } = [];
        public int Changes { get; private set; }

        public Rig(PlaceTool.Modes mode, bool listen = true, params WorldScene.Thing[] things)
        {
            W = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            W.Show();
            S = TestScenes.Flat(2, things);
            W.View.Show(S.Scene, null);
            W.Edit(S);
            W.Tools.ChooseMode(ToolMode.Place);
            W.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
            var scene = S.Scene;
            T = new PlaceTool { Scene = () => scene, Brush = S.Brush, Mode = mode, Tilt = 0, NameOf = h => h == StableHash.Of(Wall) ? Wall : null };
            T.Chosen.Clear();
            T.Chosen.Add("Beech1");
            var session = S;
            In = new PlaceInput(W.View, T) { Session = () => session };
            if (listen)
            {
                In.Message += Said.Add;
                In.Placed += Placed.Add;
                In.Changed += () => Changes++;
            }
        }

        /// <summary>The last thing said, or null.</summary>
        public string? Last => Said.Count > 0 ? Said[^1] : null;

        /// <summary>Grid point (gx, gz) on the view (the ground is at 30 m).</summary>
        public Avalonia.Point At(float gx, float gz)
        {
            var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), W.View.ViewProj);
            return new Avalonia.Point((q.X / q.W + 1) / 2 * Size.Width, (1 - q.Y / q.W) / 2 * Size.Height);
        }

        /// <summary>A grid point on the view.</summary>
        public Avalonia.Point At(Vector2 g) => At(g.X, g.Y);

        /// <summary>A click at grid point (gx, gz), with these keys held and as many clicks as given.</summary>
        public void Click(float gx, float gz, bool shift = false, bool ctrl = false, bool alt = false, int clicks = 1)
        {
            In.Down(At(gx, gz), Size, shift, ctrl, alt, clicks);
            In.Up(At(gx, gz), Size);
        }

        /// <summary>A click on a point of the view.</summary>
        public void Click(Avalonia.Point p, bool shift = false, bool alt = false)
        {
            In.Down(p, Size, shift, false, alt, 1);
            In.Up(p, Size);
        }

        /// <summary>A drag along these grid points.</summary>
        public void Drag(params (float X, float Z)[] path)
        {
            In.Down(At(path[0].X, path[0].Z), Size, false, false, false, 1);
            foreach (var (x, z) in path.Skip(1))
            {
                In.Moved(At(x, z), Size);
            }
            In.Up(At(path[^1].X, path[^1].Z), Size);
        }

        /// <summary>A key with Shift held or not.</summary>
        public bool Key(Key key, bool shift = false) => In.Key(key, shift, false);

        /// <summary>One paint step of the brush's timer, by hand.</summary>
        public void Paint() =>
            typeof(PlaceInput).GetMethod("Paint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(In, null);
    }

    [AvaloniaFact]
    public void TheEyedropperAndTheHeightPickerSayWhatTheyDid()
    {
        var r = new Rig(PlaceTool.Modes.Brush, true, new WorldScene.Thing(1, StableHash.Of("Oak1"), new Vector3(64 - 32, 30, 64 - 32), Vector3.Zero, 1, false));
        var s = r.S.Scene;
        r.W.View.SetBoxes([(0, new Vector3(64 - 32 - 1 - s.Cx, 30, -(64 - 32 + 1 - s.Cz)), new Vector3(64 - 32 + 1 - s.Cx, 36, -(64 - 32 - 1 - s.Cz)))]);
        string? picked = null;
        bool added = false;
        // Nothing under the cursor, then an object whose kind has no name: nothing picked.
        r.In.PickOnce = (n, _) => picked = n;
        r.Click(10, 10);
        Assert.StartsWith("Nothing picked", r.Last);
        r.In.PickOnce = (n, _) => picked = n;
        r.Click(64, 64);
        Assert.Null(picked);
        Assert.Null(r.In.PickOnce);
        // Named: picked, with Shift passed on.
        r.T.NameOf = _ => "Oak1";
        r.In.PickOnce = (n, shift) => (picked, added) = (n, shift);
        r.Click(64, 64, shift: true);
        Assert.Equal(("Oak1", true), (picked, added));
        // Esc cancels picking.
        r.In.PickOnce = (n, _) => picked = n;
        Assert.True(r.Key(Key.Escape));
        Assert.Equal("Picking cancelled.", r.Last);
        Assert.Null(r.In.PickOnce);
        // Alt + click on the ground picks its height; off the area, nothing.
        r.Click(40, 40, alt: true);
        Assert.StartsWith("Elevation: everything at", r.Last);
        int said = r.Said.Count;
        r.Click(Sky, alt: true);
        Assert.Equal(said, r.Said.Count);
        // Alt with Shift is no height pick: a Shift click erases (nothing here to erase).
        r.Click(40, 40, shift: true, alt: true);
        Assert.Equal("Removed 0 object(s).", r.Last);
    }

    [AvaloniaFact]
    public void RefreshLooksUnderTheCursorOnlyWhenItNeedsTo()
    {
        var r = new Rig(PlaceTool.Modes.Brush);
        r.T.OnTop = true;
        // No mouse yet: no preview off the ground.
        r.T.Notify();
        Assert.Empty(r.In.Shown);
        r.In.Moved(r.At(64, 64), r.Size);
        Assert.NotEmpty(r.In.Shown);
        Assert.True(r.Changes > 0);
        // Shift held hides the brush's preview; held again or let go again changes nothing.
        r.In.ShiftHeld(false);
        Assert.NotEmpty(r.In.Shown);
        r.In.ShiftHeld(true);
        r.In.ShiftHeld(true);
        Assert.Empty(r.In.Shown);
        r.In.ShiftHeld(false);
        Assert.NotEmpty(r.In.Shown);
        // Off the ground: no preview.
        r.In.Moved(Sky, r.Size);
        Assert.Empty(r.In.Shown);
    }

    [AvaloniaFact]
    public void AStrokePaintsOnlyWhileDraggedOnTheGround()
    {
        var r = new Rig(PlaceTool.Modes.Brush);
        r.T.Density = 10;
        // No stroke: a paint step does nothing.
        r.Paint();
        r.In.Moved(r.At(64, 64), r.Size);
        int stamp = r.In.Shown.Length;
        Assert.True(stamp > 0);
        r.In.Down(r.At(64, 64), r.Size, false, false, false, 1);
        // A few pixels: not yet a drag, nothing painted.
        r.In.Moved(r.At(64.2f, 64), r.Size);
        r.Paint();
        Assert.Equal(stamp, r.In.Shown.Length);
        // A drag: the click's layout and more.
        r.In.Moved(r.At(74, 64), r.Size);
        r.Paint();
        Assert.True(r.In.Shown.Length >= stamp);
        // Off the ground: nothing painted there.
        r.In.Moved(Sky, r.Size);
        int shown = r.In.Shown.Length;
        r.Paint();
        Assert.Equal(shown, r.In.Shown.Length);
        r.In.Up(Sky, r.Size);
        Assert.Equal(shown, r.S.Pending.Added);
        Assert.StartsWith("Placed", r.S.UndoLabel);
        Assert.StartsWith("Placed ", r.Last);
        Assert.Equal("Beech1", Assert.Single(r.Placed[^1]));
        // No area open: a click places nothing.
        r.In.Session = () => null;
        r.Click(40, 40);
        Assert.Equal(shown, r.S.Pending.Added);
    }

    [AvaloniaFact]
    public void ErasingWithNothingUnderTheBrushRemovesNothing()
    {
        var r = new Rig(PlaceTool.Modes.Brush);
        int steps = r.S.UndoList.Count;
        r.In.Down(r.At(64, 64), r.Size, true, false, false, 1);
        r.In.Moved(r.At(74, 64), r.Size);
        r.Paint();
        r.In.Up(r.At(74, 64), r.Size);
        Assert.Equal("Removed 0 object(s).", r.Last);
        Assert.Equal(steps, r.S.UndoList.Count);
    }

    [AvaloniaFact]
    public void BuildingPutsOnePieceWhereTheRayPoints()
    {
        var gone = new WorldScene.Thing(2, StableHash.Of(Wall), new Vector3(0, 30, 0), Vector3.Zero, 0, true) { Gone = true };
        var r = new Rig(PlaceTool.Modes.Brush, true, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(-20, 30, -20), Vector3.Zero, 1, false), gone);
        r.T.Chosen.Clear();
        r.T.Chosen.Add(Wall);
        r.T.Building = true;
        r.In.Moved(r.At(64, 64), r.Size);
        Assert.Single(r.In.Shown);
        // A click (even dragged a little) puts the one the preview shows; dragging paints no more.
        r.In.Down(r.At(64, 64), r.Size, false, false, false, 1);
        r.In.Moved(r.At(74, 64), r.Size);
        r.Paint();
        r.In.Up(r.At(74, 64), r.Size);
        Assert.Equal(1, r.S.Pending.Added);
        Assert.StartsWith("Placed ", r.Last);
        Assert.EndsWith(". Ctrl+Z takes it back.", r.Last);
        // Beside the one just built, and on the grid.
        r.Click(65, 64);
        r.T.GridStep = 2;
        r.In.Moved(r.At(45.3f, 50), r.Size);
        Assert.Single(r.In.Shown);
        r.T.GridStep = 0;
        // Past the block's edge: nothing.
        r.In.Moved(r.At(0.5f, 64), r.Size);
        Assert.Empty(r.In.Shown);
        // Off the area: the ray meets nothing, and a click says so.
        r.Click(Sky);
        Assert.Equal("Nothing placed here.", r.Last);
        // No view size: no ray at all.
        r.In.Moved(r.At(64, 64), default);
        Assert.Empty(r.In.Shown);
        // Ctrl + wheel lifts it, from where the cursor is.
        r.In.Lift(0.5f);
        Assert.Equal(0.5f, r.T.HeightNudge);
        Assert.StartsWith("Lift: +", r.Last);
        r.In.Lift(-0.5f);
        Assert.Equal("Lift: none (where the cursor points).", r.Last);
    }

    [AvaloniaFact]
    public void LinePointsOffTheAreaAndDoubleClicksAwayFromTheEnd()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        // Off the area: nothing.
        r.Click(Sky);
        Assert.Empty(r.T.Points);
        // A double-click on the first point: no line yet to place.
        r.Click(40, 64, clicks: 2);
        r.Click(64, 64);
        // A double-click away from the last point adds one.
        r.Click(30, 30, clicks: 2);
        Assert.Equal(3, r.T.Points.Count);
        Assert.Equal(0, r.S.Pending.Added);
        // Esc takes the line; with none left Backspace and Esc are not the tool's.
        Assert.True(r.Key(Key.Escape));
        Assert.Empty(r.T.Points);
        Assert.False(r.Key(Key.Back));
        Assert.False(r.Key(Key.Escape));
        r.In.RemoveLastPoint();
        Assert.Empty(r.T.Points);
    }

    [AvaloniaFact]
    public void AFreehandDragAddsAPointEveryTwoMetres()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        r.Drag((30, 40), (33, 40), (36, 40), (39, 40));
        // Let go where the last point went: none added there again.
        Assert.Equal(4, r.T.Points.Count);
        // Pressed and barely moved: no drag yet; then on from the line's end.
        r.In.Down(r.At(50, 40), r.Size, false, false, false, 1);
        r.In.Moved(r.At(50.5f, 40), r.Size);
        Assert.Equal(4, r.T.Points.Count);
        r.In.Moved(r.At(54, 40), r.Size);
        r.In.Moved(r.At(55, 40), r.Size);
        Assert.Equal(6, r.T.Points.Count);
        r.In.Up(r.At(56, 40), r.Size);
        Assert.Equal(7, r.T.Points.Count);
    }

    [AvaloniaFact]
    public void AZoneIsClickedEditedTurnedAndPlaced()
    {
        var r = new Rig(PlaceTool.Modes.Zone);
        Assert.True(r.Key(Key.OemPeriod));
        Assert.Equal("Draw a zone first, then turn it.", r.Last);
        foreach (var (x, z) in new[] { (40f, 40f), (90f, 40f), (90f, 90f) })
        {
            r.Click(x, z);
        }
        Assert.Equal(3, r.T.Points.Count);
        // Turned by 15° (Shift); its points are kept unturned and dragged where they are drawn.
        Assert.True(r.Key(Key.OemPeriod, shift: true));
        Assert.Equal(15, r.T.ShapeTurn, 3);
        var first = r.T.Xf(r.T.Points[0]);
        var to = first + new Vector2(0, -6);
        r.In.Down(r.At(first), r.Size, false, false, false, 1);
        r.In.Moved(r.At(to), r.Size);
        r.In.Up(r.At(to), r.Size);
        Assert.Equal(3, r.T.Points.Count);
        Assert.True(Vector2.Distance(r.T.Xf(r.T.Points[0]), to) < 0.5f, $"{r.T.Xf(r.T.Points[0])}");
        // A new point, kept unturned.
        r.Click(60, 110);
        Assert.Equal(4, r.T.Points.Count);
        Assert.True(Vector2.Distance(r.T.Xf(r.T.Points[^1]), new Vector2(60, 110)) < 0.5f);
        int shown = r.In.Shown.Length;
        Assert.True(shown > 0);
        Assert.True(r.Key(Key.Enter));
        Assert.Equal($"Placed {shown} in a zone", r.S.UndoLabel);
        Assert.True(r.Key(Key.Escape));
        Assert.Empty(r.T.Points);
    }

    [AvaloniaFact]
    public void AGridBoxIsPlacedAndClearedWithTheKeys()
    {
        var r = new Rig(PlaceTool.Modes.Grid);
        r.Drag((50, 50), (60, 60), (70, 66));
        Assert.NotNull(r.T.GridB);
        int shown = r.In.Shown.Length;
        Assert.True(shown > 0);
        Assert.True(r.Key(Key.Enter));
        Assert.Equal($"Placed {shown} in a grid", r.S.UndoLabel);
        // Backspace is no grid key; Esc clears the box.
        Assert.False(r.Key(Key.Back));
        r.In.RemoveLastPoint();
        Assert.True(r.Key(Key.Escape));
        Assert.Null(r.T.GridA);
    }

    [AvaloniaFact]
    public void CirclesAndRectanglesAreDraggedOutAndCleared()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        r.T.LineShape = PlaceTool.LineShapes.Circle;
        r.Click(64, 64);
        Assert.Equal("Press at the centre and drag out to the size.", r.Last);
        Assert.Null(r.T.FigA);
        r.Drag((64, 64), (70, 68));
        Assert.NotNull(r.T.FigA);
        Assert.NotNull(r.T.FigB);
        Assert.True(r.In.Shown.Length > 0);
        // Dragged off the area and let go there: as a click.
        r.In.Down(r.At(64, 64), r.Size, false, false, false, 1);
        r.In.Moved(Sky, r.Size);
        r.In.Up(Sky, r.Size);
        Assert.Null(r.T.FigA);
        r.T.LineShape = PlaceTool.LineShapes.Rect;
        r.Click(64, 64);
        Assert.Equal("Press at a corner and drag to the opposite one.", r.Last);
        r.Drag((64, 64), (70, 68));
        Assert.True(r.Key(Key.Escape));
        Assert.Null(r.T.FigA);
    }

    [AvaloniaFact]
    public void KeysTurnByTheirStepAndLeaveTheRest()
    {
        var r = new Rig(PlaceTool.Modes.Brush);
        Assert.False(r.In.Key(Key.R, false, true));
        // The Workshop's own step; Shift: 1°.
        r.In.TurnStep = 22.5f;
        Assert.True(r.Key(Key.OemPeriod));
        Assert.True(r.Key(Key.OemPeriod, shift: true));
        Assert.Equal(23.5f, r.T.Rotation, 3);
        r.In.TurnStep = null;
        Assert.True(r.Key(Key.OemComma, shift: true));
        Assert.Equal(8.5f, r.T.Rotation, 3);
        // Not the brush's keys.
        Assert.False(r.Key(Key.Enter));
        Assert.False(r.Key(Key.Escape));
        Assert.False(r.Key(Key.Back));
        Assert.True(r.Key(Key.R));
        Assert.True(r.Key(Key.PageDown, shift: true));
        Assert.StartsWith("Elevation: above the ground by -0.1", r.Last);
    }

    [AvaloniaFact]
    public void PlacingAShapeNeedsAnAreaOpen()
    {
        var r = new Rig(PlaceTool.Modes.Line);
        r.Click(40, 64);
        r.Click(60, 64);
        Assert.True(r.In.Shown.Length > 0);
        r.In.Session = () => null;
        r.In.PlaceShape();
        Assert.Equal("Draw a line first (click points on the ground).", r.Last);
        Assert.Equal(0, r.S.Pending.Added);
    }

    [AvaloniaFact]
    public void WithNobodyListeningNothingIsSaid()
    {
        var r = new Rig(PlaceTool.Modes.Brush, listen: false);
        r.In.Moved(r.At(64, 64), r.Size);
        r.In.PickOnce = (_, _) => { };
        r.Click(10, 10);
        r.Click(40, 40, alt: true);
        r.In.Lift(0.5f);
        Assert.True(r.Key(Key.PageUp));
        r.In.PickOnce = (_, _) => { };
        Assert.True(r.Key(Key.Escape));
        // A click places, a Shift click erases.
        r.Click(64, 64);
        int placed = r.S.Pending.Added;
        Assert.True(placed > 0);
        r.Click(20, 20, shift: true);
        // Building, with nothing placed.
        r.T.Building = true;
        r.Click(Sky);
        r.T.Building = false;
        r.T.Mode = PlaceTool.Modes.Grid;
        Assert.True(r.Key(Key.OemPeriod));
        r.In.PlaceShape();
        r.T.Mode = PlaceTool.Modes.Line;
        r.T.LineShape = PlaceTool.LineShapes.Circle;
        r.Click(64, 64);
        r.T.LineShape = PlaceTool.LineShapes.Points;
        r.Click(40, 64);
        r.Click(60, 64);
        Assert.True(r.Key(Key.Enter));
        Assert.True(r.S.Pending.Added > placed);
    }
}
