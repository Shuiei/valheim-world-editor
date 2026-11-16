using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Build tool in a world's open ground: the Build dock and the building bar instead of the Place panel and the
/// Mask, the Place tool's own settings given back when another tool is chosen; pieces laid along a line and in a grid;
/// and the building bar shared with Select and Paste (the same steps, Shift finer, PgUp, PgDn and Ctrl + wheel alike).
/// </summary>
[Collection("World files")]
public class MainWindowBuildWorldTests
{
    [AvaloniaFact]
    public void TheBuildToolOpensTheDockInAWorldAndGivesThePlaceToolBack()
    {
        var (w, _) = TestWindows.EditingFlat();
        var t = w.PlaceTool;
        w.Tools.ChooseMode(ToolMode.Place);
        t.Chosen.Clear();
        t.Chosen.Add("Beech1");
        t.Mode = PlaceTool.Modes.Zone;
        t.Elev = 3;
        Assert.True(w.PlacePanel.Card.IsVisible);
        Assert.False(w.BuildPanel.Card.IsVisible);
        Assert.Equal(BuildingBar.Uses.None, w.BuildingBar.Use);

        // N: the Build tool, the Place tool set for building.
        Assert.True(w.Tools.Key("U"));
        Assert.Equal(ToolMode.Place, w.Tools.Mode);
        Assert.True(w.Tools.BuildMode);
        Assert.True(w.BuildingInWorld);
        Assert.True(t.Building);
        Assert.True(t.Hammering);
        Assert.True(w.BuildPanel.Card.IsVisible);
        Assert.False(w.PlacePanel.Card.IsVisible);
        Assert.False(w.MaskPanel.Card.IsVisible);
        Assert.Single(t.Chosen);
        Assert.Contains(BuildPanel.Pieces, p => p.Prefab == t.Chosen[0]);
        // No Library in a world (its blueprints go onto the Workshop's plot); lines are offered.
        Assert.False(w.BuildPanel.LibraryTab.IsVisible);
        Assert.True(w.BuildPanel.LinesOffered);
        // The building bar, with Built by.
        Assert.Equal(BuildingBar.Uses.Build, w.BuildingBar.Use);
        Assert.True(w.BuildingBar.Card.IsVisible);
        Assert.True(((Control)w.BuilderBox.Parent!).IsVisible);
        Assert.Equal(new HelpTarget("build.md"), w.HelpTopic());
        // Chosen again: still on, the Place tool's settings kept aside once.
        w.Tools.ChooseBuild();
        Assert.True(w.BuildingInWorld);

        // T: the Place tool as it was.
        Assert.True(w.Tools.Key("T"));
        Assert.False(w.Tools.BuildMode);
        Assert.False(w.BuildingInWorld);
        Assert.False(t.Building);
        Assert.Equal(["Beech1"], t.Chosen);
        Assert.Equal((PlaceTool.Modes.Zone, 3f), (t.Mode, t.Elev));
        Assert.Null(w.PlaceInput.TurnStep);
        Assert.Null(w.PlaceInput.LiftStep);
        Assert.True(w.PlacePanel.Card.IsVisible);
        Assert.False(w.BuildPanel.Card.IsVisible);
        Assert.True(w.MaskPanel.Card.IsVisible);
        Assert.Equal(BuildingBar.Uses.None, w.BuildingBar.Use);
        Assert.Equal(new HelpTarget("place.md"), w.HelpTopic());
        t.Mode = PlaceTool.Modes.Brush;
    }

    [AvaloniaFact]
    public void PiecesAreLaidAlongALineAndInAGrid()
    {
        var (w, s) = TestWindows.EditingFlat();
        var t = w.PlaceTool;
        var p = w.BuildPanel;
        w.Tools.ChooseBuild();
        p.Choose("woodwall");
        Assert.Equal(BuildPanel.Lays.One, p.Lay);

        // A line: the walls end to end between the points, two high.
        p.SetLay(BuildPanel.Lays.Line);
        Assert.Equal(PlaceTool.Modes.Line, t.Mode);
        Assert.Equal(PlaceTool.LineShapes.Points, t.LineShape);
        Assert.True(t.EndToEnd);
        Assert.False(t.Hammering);
        Assert.True(t.Building);
        p.LayersBox.Value = 2;
        Assert.Equal(2, t.Layers);
        // 5 m: two 2 m walls, twice.
        t.Points.Add(new Vector2(20, 32));
        t.Points.Add(new Vector2(25, 32));
        t.Notify();
        Assert.Equal(4, w.PlaceInput.PreviewNow.Count);
        int before = s.Pending.Added;
        p.LayPlaceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(before + 4, s.Pending.Added);
        // Built as a player builds them.
        Assert.All(s.Scene.Things.TakeLast(4), thing => Assert.True(thing.Piece));
        p.LayClearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(t.Points);

        // Closed: the line goes back to its first point.
        p.ClosedBox.IsChecked = true;
        Assert.True(t.Loop);
        p.SetLay(BuildPanel.Lays.Circle);
        Assert.Equal(PlaceTool.LineShapes.Circle, t.LineShape);
        Assert.False(t.Loop);
        p.SetLay(BuildPanel.Lays.Rect);
        Assert.Equal(PlaceTool.LineShapes.Rect, t.LineShape);

        // A grid: a floor of 2 m pieces over a 6 m box.
        p.Choose("wood_floor");
        p.SetLay(BuildPanel.Lays.Grid);
        Assert.Equal(PlaceTool.Modes.Grid, t.Mode);
        p.SpacingBox.Value = 2;
        Assert.Equal(2, t.Cell);
        t.GridA = new Vector2(10, 10);
        t.GridB = new Vector2(16, 16);
        t.Notify();
        Assert.Equal(9, w.PlaceInput.PreviewNow.Count);
        Assert.All(w.PlaceInput.PreviewNow, o => Assert.Equal((1f, Vector3.Zero), (o.Scale, o.Rotation)));

        // PgUp lifts what is laid, a line or a grid too (above the ground).
        float onGround = w.PlaceInput.PreviewNow[0].Position.Y;
        Assert.True(w.PlaceInput.Key(Avalonia.Input.Key.PageUp, false, false));
        Assert.Equal(0.5f, t.HeightNudge);
        Assert.Equal(9, w.PlaceInput.PreviewNow.Count);
        Assert.All(w.PlaceInput.PreviewNow, o => Assert.Equal(onGround + 0.5f, o.Position.Y, 2));
        w.BuildingBar.LiftReset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(0, t.HeightNudge);

        // One piece again: as the hammer.
        p.SetLay(BuildPanel.Lays.One);
        Assert.True(t.Hammering);
        Assert.Null(t.GridA);
        w.Tools.Choose(null);
        Assert.False(t.Building);
    }

    [AvaloniaFact]
    public void BuildHereInADungeonOffersNoLinesAndEndsWithThePlaceTool()
    {
        var (w, _) = TestWindows.EditingFlat();
        w.BuildPanel.SetLay(BuildPanel.Lays.Line);
        // From the Build tool in the world: Build here takes over, the Place tool's own settings kept.
        w.Tools.ChooseBuild();
        Assert.True(w.BuildingInWorld);
        w.BuildInDungeon(true);
        Assert.True(w.BuildingInDungeon);
        Assert.False(w.BuildingInWorld);
        Assert.True(w.Tools.BuildMode);
        // A dungeon's rooms are far above the ground a line is drawn on: one piece at a time only.
        Assert.False(w.BuildPanel.LinesOffered);
        Assert.False(w.BuildPanel.LayRow.IsVisible);
        Assert.True(w.PlaceTool.Hammering);
        // The Place tool ends it.
        w.Tools.ChooseMode(ToolMode.Place);
        Assert.False(w.BuildingInDungeon);
        Assert.False(w.PlaceTool.Building);
        Assert.True(w.PlacePanel.Card.IsVisible);
        w.BuildPanel.SetLay(BuildPanel.Lays.One);
    }

    [AvaloniaFact]
    public void SelectAndPasteShareTheBarsStepsAndKeys()
    {
        var (w, s) = TestWindows.EditingFlat(TestScenes.Beech(1, new Vector3(20, 30, 20)));
        var bar = w.BuildingBar;

        // Select: the bar's snapping, grid and steps.
        w.Tools.ChooseSelect();
        Assert.Equal(BuildingBar.Uses.Select, bar.Use);
        Assert.True(w.View.SelectTool.SnapToPieces);
        bar.SetSnapMode(PlaceTool.SnapModes.Free);
        Assert.False(w.View.SelectTool.SnapToPieces);
        bar.SetSnapMode(PlaceTool.SnapModes.Manual);
        Assert.True(w.View.SelectTool.SnapToPieces);
        bar.GridBox.SelectedIndex = 2;
        bar.TurnBox.SelectedIndex = 3;
        bar.LiftBox.SelectedIndex = 3;
        Assert.Equal((1f, 45f, 1f), (w.View.SelectTool.GridStep, w.View.SelectTool.TurnStep, w.View.SelectTool.LiftStep));
        Assert.Equal(1f, w.View.Paste.GridStep);
        // Ctrl + wheel lifts the selection as PgUp does; with nothing selected it is the view's.
        Assert.False(w.View.CtrlWheel!(1, false));
        w.View.Select([0]);
        w.View.SelectTool.OnGround = false;
        Assert.True(w.View.CtrlWheel!(1, false));
        Assert.True(w.View.SelectTool.Key(Avalonia.Input.Key.PageUp, true));
        Assert.True(w.View.SelectTool.Key(Avalonia.Input.Key.OemPeriod, false));
        Assert.True(w.View.SelectTool.Key(Avalonia.Input.Key.OemComma, true));
        w.View.SelectTool.Commit();
        // +1 m and a fifth of it; 45° and 1° back.
        Assert.Equal(31.2f, s.Scene.Things[^1].Position.Y, 2);
        Assert.Equal(44f, s.Scene.Things[^1].Rotation.Y, 2);
        w.View.SelectTool.OnGround = true;

        // Paste: the same steps for its turn and its height, the lift shown on the bar, Reset takes it back.
        w.View.Paste.Clip = CopyData.FromArea(AreaBoxes.Box(32, 32, 48, 48), s.Ground, s.Scene, [], _ => null);
        w.StartPaste();
        Assert.Equal(BuildingBar.Uses.Paste, bar.Use);
        Assert.Equal("none", bar.LiftText.Text);
        Assert.True(w.View.CtrlWheel!(1, false));
        Assert.Equal(1m, w.PastePanel.OffsetBox.Value);
        Assert.Equal("+1.0 m", bar.LiftText.Text);
        Assert.True(w.View.CtrlWheel!(-1, true));
        Assert.Equal(0.8m, w.PastePanel.OffsetBox.Value);
        bar.LiftReset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(0m, w.PastePanel.OffsetBox.Value);
        Assert.Equal("none", bar.LiftText.Text);
        Assert.True(w.AltWheel(Avalonia.Input.Key.OemComma, false));
        Assert.Equal(45f, w.View.Paste.Turn, 3);
        // The paste goes by the grid.
        Assert.Equal(new Vector2(40, 41), w.View.Paste.OnGrid(new Vector2(40.4f, 40.6f), s.Scene.W, s.Scene.H));
        bar.GridBox.SelectedIndex = 0;
        Assert.Equal(new Vector2(40.4f, 40.6f), w.View.Paste.OnGrid(new Vector2(40.4f, 40.6f), s.Scene.W, s.Scene.H));

        // Another tool: no bar.
        w.Tools.Choose(BrushTool.Raise);
        Assert.Equal(BuildingBar.Uses.None, bar.Use);
        Assert.False(bar.Card.IsVisible);
    }

    [AvaloniaFact]
    public void AMovedPieceGoesByTheGridWhenNothingSnaps()
    {
        // A wall alone (nothing to snap to), dragged across the ground with the 1 m grid on.
        var v = SelectToolRun.Open(SelectToolRun.T(1, SelectToolRun.Wall, 40, 30, 40));
        v.W.BuildingBar.GridBox.SelectedIndex = 2;
        Assert.Equal(1, v.Tool.GridStep);
        v.Tool.OnGround = false;
        v.Click(40, 40);
        v.Drag(Avalonia.Input.KeyModifiers.None, (40, 40), (45, 45), (47.6f, 48.4f));
        var moved = v.Last.Position;
        // On the grid, counted from the area's middle (whole metres here), by where it was dragged.
        Assert.Equal(MathF.Round(moved.X), moved.X, 3);
        Assert.Equal(MathF.Round(moved.Z), moved.Z, 3);
        Assert.InRange(moved.X, 46.5f, 48.5f);
        Assert.InRange(moved.Z, 47.5f, 49.5f);
        // Lifted on the spot: the grid leaves it where it stands.
        v.W.View.Select([v.S.Scene.Things.Count - 1]);
        v.Tool.Lift(0.3f);
        v.Tool.Commit();
        Assert.Equal((moved.X, moved.Z), (v.Last.Position.X, v.Last.Position.Z));
        Assert.Equal(moved.Y + 0.3f, v.Last.Position.Y, 3);
    }
}
