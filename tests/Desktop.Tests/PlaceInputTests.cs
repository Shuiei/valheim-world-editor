using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's input in the 3D view: the eyedropper and the height picker, the strokes of the brush, erasing, and
/// building a piece where the ray points. The rig they drive is here too, for the Place input tests of the other panels.
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
    internal sealed class Rig
    {
        public MainWindow W { get; }
        public EditSession S { get; }
        public PlaceTool T { get; }
        public PlaceInput In { get; }
        public Avalonia.Size Size { get; } = SharedPlaceWindow.Size;
        public List<string> Said { get; } = [];
        /// <summary>What was said, in both languages.</summary>
        public List<TerrainEditor.App.Said> Told { get; } = [];
        public List<IReadOnlyList<string>> Placed { get; } = [];
        public int Changes { get; private set; }

        public Rig(PlaceTool.Modes mode, bool listen = true, params WorldScene.Thing[] things)
        {
            (W, S) = SharedPlaceWindow.Open(things);
            var scene = S.Scene;
            T = new PlaceTool { Scene = () => scene, Brush = S.Brush, Mode = mode, Tilt = 0, NameOf = h => h == StableHash.Of(Wall) ? Wall : null };
            T.Chosen.Clear();
            T.Chosen.Add("Beech1");
            var session = S;
            In = new PlaceInput(W.View, T) { Session = () => session };
            if (listen)
            {
                In.Message += t =>
                {
                    Said.Add(t.Shown);
                    Told.Add(t);
                };
                In.Placed += Placed.Add;
                In.Changed += () => Changes++;
            }
        }

        /// <summary>The last thing said, or null.</summary>
        public string? Last => Said.Count > 0 ? Said[^1] : null;

        /// <summary>Grid point (gx, gz) on the view (the ground is at 30 m).</summary>
        public Avalonia.Point At(float gx, float gz) => SharedPlaceWindow.At(W, gx, gz);

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
        public void Drag(params (float X, float Z)[] path) => SharedPlaceWindow.Drag(In, W, Size, path);

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
}
