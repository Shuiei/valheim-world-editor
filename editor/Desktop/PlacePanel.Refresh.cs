using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Bringing the panel's controls in line with the tool's settings and what is chosen.
public sealed partial class PlacePanel
{
    /// <summary>
    /// Shows the rows the mode, shape and chosen kinds use, marks the buttons that are on, and updates the hints.
    /// </summary>
    private void Sync()
    {
        var t = T;
        var m = t.Mode;
        foreach (var (k, b) in ModeButtons)
        {
            b.Classes.Set("on", k == m);
        }
        foreach (var (k, b) in ShapeButtons)
        {
            b.Classes.Set("on", k == t.LineShape);
        }
        BesideButton.Classes.Set("on", !(t.OnTop));
        OnTopButton.Classes.Set("on", t.OnTop);
        bool line = m == PlaceTool.Modes.Line, endToEnd = line && t.EndToEnd;
        _elevRow.IsVisible = t.Elevation != PlaceTool.Elevations.Ground;
        _pieceBox.IsVisible = t.PiecesChosen;
        _attachRow.IsVisible = m == PlaceTool.Modes.Brush;
        GrowBox.IsVisible = t.GrowMatters;
        _brushRows.IsVisible = m == PlaceTool.Modes.Brush;
        _modeHead.Text = (m switch
        {
            PlaceTool.Modes.Line => Lang.T("place.head.line"),
            PlaceTool.Modes.Grid => Lang.T("place.mode.grid"),
            PlaceTool.Modes.Zone => Lang.T("place.head.zone"),
            _ => Lang.T("place.mode.brush"),
        }).ToUpperInvariant();
        // One at a time places only what the click shows: nothing to scatter.
        _scatterRows.IsVisible = m == PlaceTool.Modes.Zone || m == PlaceTool.Modes.Brush && !t.OneAtATime;
        _patchRow.IsVisible = t.Clump > 0;
        _facingRows.IsVisible = !endToEnd;
        _lineShapeBox.IsVisible = line;
        _lineBox.IsVisible = line;
        _layersRow.IsVisible = endToEnd;
        _loopRow.IsVisible = line && t.LineShape == PlaceTool.LineShapes.Points;
        _freeLineRows.IsVisible = !endToEnd;
        AlongBox.IsVisible = t.LineShape == PlaceTool.LineShapes.Points;
        CurveBox.IsVisible = t.LineShape == PlaceTool.LineShapes.Points;
        _gridRow.IsVisible = m == PlaceTool.Modes.Grid;
        _shapeRow.IsVisible = m != PlaceTool.Modes.Brush;
        _undoRow.IsVisible = m is PlaceTool.Modes.Line or PlaceTool.Modes.Zone && t.Points.Count > 0;
        _brushHint.IsVisible = m == PlaceTool.Modes.Brush;
        _brushHint.Text = t.OneAtATime ? Lang.T("place.hint.single") : Lang.T("place.hint.brush");
        _lineHintText.Text = t.LineShape switch
        {
            PlaceTool.LineShapes.Circle => Lang.T("place.hint.circle"),
            PlaceTool.LineShapes.Rect => Lang.T("place.hint.rect"),
            _ => Lang.T("place.hint.points"),
        };
        var cult = t.NeedCultivated;
        Note.Text = t.Chosen.Count == 0 ? Lang.T("place.hint.nothingYet", ChooseKindsText)
            : cult.Count > 1 ? Lang.T("place.hint.cultivated.many", string.Join(", ", cult))
            : cult.Count == 1 ? Lang.T("place.hint.cultivated.one", cult[0]) : "";
        Note.IsVisible = Note.Text != "";
        ShowFit();
    }

    /// <summary>
    /// Fills the controls from the tool's settings; the settings shown follow the mode and what is chosen.
    /// </summary>
    public void Fill()
    {
        _filling = true;
        var t = T;
        ElevationBox.SelectedIndex = (int)t.Elevation;
        ElevBox.Value = (decimal)t.Elev;
        RotationSlider.Value = t.Rotation;
        SingleBox.IsChecked = t.OneAtATime;
        EndToEndBox.IsChecked = t.EndToEnd;
        DensitySlider.Value = t.Density;
        SpacingSlider.Value = t.Spacing;
        ClumpSlider.Value = t.Clump;
        PatchSlider.Value = t.Patch;
        SizeMinBox.Value = (decimal)t.SizeMin;
        SizeMaxBox.Value = (decimal)t.SizeMax;
        TiltSlider.Value = t.Tilt;
        RandomYawBox.IsChecked = t.RandomYaw;
        EverySlider.Value = t.Every;
        CellSlider.Value = t.Cell;
        _filling = false;
        RenderMix();
        Sync();
    }
}
