using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The building bar's snapping: Auto, Manual and Free, kept between runs and given to the Place tool only while
/// building (it is the worlds' tool too); by hand, Q and E choose the piece's snap point (E is Select otherwise), the
/// bar says which and the preview marks it; Alt places by a quarter metre without snapping.
/// </summary>
public sealed class BuildPanelSnapTests
{
    /// <summary>A mode's button.</summary>
    private static Button ModeButton(BuildPanel p, PlaceTool.SnapModes mode) => p.SnapModes.Children.OfType<Button>().Single(b => (PlaceTool.SnapModes)b.Tag! == mode);

    [AvaloniaFact]
    public void TheModeIsKeptAndLeavesTheWorldsPlaceToolAlone()
    {
        var prefs = Prefs.InMemory();
        var w = new MainWindow(load: false);
        var p = w.BuildPanel;
        p.Remember(prefs);
        Assert.Equal(3, p.SnapModes.Children.Count);
        Assert.Contains("on", ModeButton(p, PlaceTool.SnapModes.Auto).Classes);
        Assert.True(w.PlaceTool.SnapTo);

        ModeButton(p, PlaceTool.SnapModes.Free).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(PlaceTool.SnapModes.Free, p.SnapMode);
        Assert.Contains("on", ModeButton(p, PlaceTool.SnapModes.Free).Classes);
        Assert.DoesNotContain("on", ModeButton(p, PlaceTool.SnapModes.Auto).Classes);
        // Not building: the worlds' Place tool still snaps.
        Assert.True(w.PlaceTool.SnapTo);
        Assert.Equal("Free", prefs.Get("build.snapMode", ""));
        var again = new MainWindow(load: false).BuildPanel;
        again.Remember(prefs);
        Assert.Equal(PlaceTool.SnapModes.Free, again.SnapMode);
        // Something else kept: Auto.
        prefs.Set("build.snapMode", "Sideways");
        again.Remember(prefs);
        Assert.Equal(PlaceTool.SnapModes.Auto, again.SnapMode);
    }

    [AvaloniaFact]
    public async Task ByHandQAndEChooseTheSnapPoint()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        await w.OpenWorkshop(null);
        var p = w.BuildPanel;
        var t = w.PlaceTool;
        p.Choose("woodwall");
        int n = Hammer.SnapCount("woodwall");
        // Auto: E is not the tool's (Select takes it), the bar says nothing of snap points.
        Assert.False(w.PlaceInput.Key(Key.E, false, false));
        Assert.False(p.SnapPointText.IsVisible);

        p.SetSnapMode(PlaceTool.SnapModes.Manual);

        Assert.Equal(PlaceTool.SnapModes.Manual, t.SnapMode);
        Assert.True(t.SnapTo);
        Assert.True(p.SnapPointText.IsVisible);
        Assert.Equal($"Point 1 of {n} · Q / E", p.SnapPointText.Text);
        Assert.True(w.PlaceInput.Key(Key.E, false, false));
        Assert.Equal(1, t.SnapPoint);
        Assert.Equal($"Point 2 of {n} · Q / E", p.SnapPointText.Text);
        Assert.True(w.PlaceInput.Key(Key.Q, false, false));
        Assert.True(w.PlaceInput.Key(Key.Q, false, false));
        Assert.Equal($"Point {n} of {n} · Q / E", p.SnapPointText.Text);
        // Another piece: from its first snap point.
        p.Choose("wood_floor");
        Assert.Equal(0, t.SnapPoint);
        // Free: no snapping at all.
        p.SetSnapMode(PlaceTool.SnapModes.Free);
        Assert.False(t.SnapTo);
        Assert.False(p.SnapPointText.IsVisible);
        Assert.False(w.PlaceInput.Key(Key.Q, false, false));
        await w.LeaveWorkshop();
        // Out of the Workshop the worlds' Place tool snaps as it did.
        Assert.True(t.SnapTo);
        Assert.False(t.Building);
    }

    // The preview marks the chosen snap point by hand; Alt rounds where the piece goes to a quarter metre.
    [AvaloniaFact]
    public async Task ThePreviewMarksTheSnapPointAndAltPlacesFinely()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        await w.OpenWorkshop(null);
        w.BuildPanel.Choose("woodwall");
        var s = w.Session!;
        var t = w.PlaceTool;
        int c = (s.Scene.W - 1) / 2;
        var mid = new Vector3(s.Scene.X0 * 64f - 32f + c, 0, s.Scene.Z0 * 64f - 32f + c);
        void Aim(float dx, float dz) => t.AimRay = (mid + new Vector3(dx, Workshop.Ground + 50, dz), -Vector3.UnitY, 50);

        Aim(0.37f, 0.1f);
        var free = t.Preview(new Vector2(c, c)).Single();
        Assert.Equal(mid.X + 0.37f, free.Position.X, 3);
        Assert.Null(t.ActiveSnap);

        t.Fine = true;
        var fine = t.Preview(new Vector2(c, c)).Single();
        Assert.Equal(mid.X + 0.25f, fine.Position.X, 3);
        Assert.Equal(mid.Z, fine.Position.Z, 3);
        t.Fine = false;

        w.BuildPanel.SetSnapMode(PlaceTool.SnapModes.Manual);
        var one = t.Preview(new Vector2(c, c)).Single();
        Assert.True(t.ActiveSnap is { Number: 1 });
        Assert.Equal(Hammer.SnapPointAt("woodwall", BlueprintFormats.FromEuler(one.Rotation), one.Position, 0), t.ActiveSnap!.Value.At);
        // Held Alt: no snap point by hand.
        t.Fine = true;
        t.Preview(new Vector2(c, c));
        Assert.Null(t.ActiveSnap);
    }
}
