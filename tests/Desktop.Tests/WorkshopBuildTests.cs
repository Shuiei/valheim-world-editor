using Avalonia.Headless.XUnit;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Building in the Workshop as the game's hammer: the build pieces by station then name, the piece going where the build
/// ray points and keeping its turn, and the lift and the turn steps.
/// </summary>
public class WorkshopBuildTests
{
    [Fact]
    public void BuildPiecesAreTheHammersByStationThenName()
    {
        var pieces = BuildPanel.Pieces;
        Assert.Contains(pieces, p => p.Prefab == "woodwall" && p.Name == "Wood Wall" && p.Category == 2);
        Assert.DoesNotContain(pieces, p => p.Prefab == "Beech1");
        for (int i = 1; i < pieces.Count; i++)
        {
            string? a = PieceCost.Get(pieces[i - 1].Prefab)?.Station, b = PieceCost.Get(pieces[i].Prefab)?.Station;
            Assert.True(BuildPanel.StationRank(a) <= BuildPanel.StationRank(b));
            if (a == b)
            {
                Assert.True(string.Compare(pieces[i - 1].Name, pieces[i].Name, StringComparison.OrdinalIgnoreCase) <= 0);
            }
        }
    }

    // Building as the game's hammer (Hammer): the piece goes where the build ray points (a wall's top:
    // on it), turned by the game's step; the grid rounds a ground point; Ctrl + wheel lifts it (a notch
    // at a time, parts of a notch added up).
    [AvaloniaFact]
    public async Task PiecesGoUpWhereTheCursorPointsAndKeepTheirTurn()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        // As an earlier test may leave it (the Place tool's choice is remembered): another piece chosen.
        w.PlaceTool.Chosen.Clear();
        w.PlaceTool.Chosen.Add("wood_floor");
        await w.OpenWorkshop(null);
        // The wall chosen in Build, as the user would (the Workshop keeps a piece already chosen).
        w.BuildPanel.Choose("woodwall");
        var s = w.Session!;
        var t = w.PlaceTool;
        int c = (s.Scene.W - 1) / 2;
        // The build ray, as the view gives it (world space): straight down onto the plot's middle.
        var mid = new Vector3(s.Scene.X0 * 64f - 32f + c, 0, s.Scene.Z0 * 64f - 32f + c);
        void Aim(float dx, float dz) => t.AimRay = (mid + new Vector3(dx, Workshop.Ground + 50, dz), -Vector3.UnitY, 50);
        Aim(0, 0);
        var first = t.Preview(new Vector2(c, c)).Single();
        Assert.Equal(Workshop.Ground + 1, first.Position.Y, 2);
        s.Commit("wall", null, [], [(new NewObject(0, StableHash.Of("woodwall"), first.Position, first.Rotation, 0), true)]);
        // Pointed at its top: on it.
        Aim(0.3f, 0);
        var up = t.Preview(new Vector2(c, c)).Single();
        Assert.Equal(Workshop.Ground + 3, up.Position.Y, 2);
        Assert.Equal((PlaceTool.SnapWays.To, "Wood Wall"), t.Snapped);
        // Turned as asked, by the game's 22.5° step.
        Assert.True(w.PlaceInput.Key(Avalonia.Input.Key.OemPeriod, false, false));
        Assert.Equal(22.5f, t.Rotation);
        Assert.Equal(22.5f, t.Preview(new Vector2(c, c)).Single().Rotation.Y, 2);
        // The grid rounds the ground's point (Off by default, as in the game).
        t.Rotation = 0;
        w.BuildPanel.SnapBox.IsChecked = false;
        w.BuildPanel.GridBox.SelectedIndex = 3;
        Aim(5.4f, 0);
        Assert.Equal(mid.X + 6, t.Preview(new Vector2(c, c)).Single().Position.X, 2);
        w.BuildPanel.GridBox.SelectedIndex = 0;
        w.BuildPanel.SnapBox.IsChecked = true;
        // Ctrl + wheel: half a metre a notch; two half notches make one; Shift: 0.1 m.
        t.Rotation = 0;
        void Wheel(double dy, Avalonia.Input.KeyModifiers mods) => w.Surface.RaiseEvent(new Avalonia.Input.PointerWheelEventArgs(w.Surface,
            new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true), w.Surface, new Avalonia.Point(10, 10), 0,
            new Avalonia.Input.PointerPointProperties(), mods, new Avalonia.Vector(0, dy)));
        Wheel(1, Avalonia.Input.KeyModifiers.Control);
        Assert.Equal(0.5f, t.HeightNudge);
        Wheel(0.5, Avalonia.Input.KeyModifiers.Control);
        Assert.Equal(0.5f, t.HeightNudge);
        Wheel(0.5, Avalonia.Input.KeyModifiers.Control);
        Assert.Equal(1f, t.HeightNudge);
        Wheel(-1, Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift);
        Assert.Equal(0.9f, t.HeightNudge, 3);
        Assert.Equal("+0.9 m", w.BuildPanel.LiftText.Text);
        // Alt + wheel turns by the step, a notch at a time.
        t.Rotation = 0;
        w.BuildPanel.TurnBox.SelectedIndex = 4;
        Wheel(0.5, Avalonia.Input.KeyModifiers.Alt);
        Assert.Equal(0, t.Rotation);
        Wheel(0.5, Avalonia.Input.KeyModifiers.Alt);
        Assert.Equal(90, MathF.Abs(t.Rotation));
        Wheel(1, Avalonia.Input.KeyModifiers.Alt);
        Assert.Equal(180, MathF.Abs(t.Rotation));
        // Reset: no lift.
        w.BuildPanel.LiftReset.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Equal(0, t.HeightNudge);
    }
}
