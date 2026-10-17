using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaBoxes;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Copy and paste in the window: the keys, the Select tool's copy, clearing a site and a paste past the open area.
/// </summary>
public class ClipboardWindowTests
{
    [AvaloniaFact]
    public void CtrlCAndCtrlVInTheWindow()
    {
        var s = Area();
        var w = TestWindows.Editing(s);
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
        var s = Area();
        var w = TestWindows.Editing(s);
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
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, Beech, new Vector3(60 - 32, 33, 60 - 32), Vector3.Zero, 1, false),
            new WorldScene.Thing(2, Beech, new Vector3(80 - 32, 30, 80 - 32), Vector3.Zero, 1, false));
        Mound(s, 60, 60, 6, 3);
        var w = WindowOn(s, 1200, 900);
        float before = H(s, 60, 60), foot = H(s, 66, 60);
        // A wall 6 m west of the point clicked (its middle 1 m up: its bottom on the ground clicked).
        var wall = SingleObjectClip(new CopyData.Obj(StableHash.Of("woodwall"), "woodwall", -6, 0, 1, new Vector3(0, 90, 0), 0, null), [new(-8, -2), new(2, -2), new(2, 2), new(-8, 2)], "Wall");
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

    // A paste that goes past the open area is refused: there the ground could not be pasted nor the
    // site cleared.
    [AvaloniaFact]
    public void APastePastTheOpenAreaIsRefused()
    {
        var s = Area();
        var w = WindowOn(s, 1200, 900);
        w.View.Paste.Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0], _ => "Beech1");
        Assert.True(w.View.Paste.Fits(new Vector2(90, 90), s.Ground.W, s.Ground.H));
        Assert.False(w.View.Paste.Fits(new Vector2(5, 90), s.Ground.W, s.Ground.H));
        w.PasteAt(new Vector2(5, 90));
        Assert.StartsWith("Not pasted: it goes past the open area", w.MessageText.Text);
        Assert.Equal(0, s.Pending.Added);
    }
}
