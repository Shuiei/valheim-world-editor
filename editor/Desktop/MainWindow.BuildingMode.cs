using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The world tools away (the rail keeps Build, Select and View), the View panel and the Mask closed, no zone
    /// borders; the Place tool set for building.
    /// </summary>
    private void EnterBuilding()
    {
        // Building in a dungeon has ended already (ShowEditor's Edit ends it), so the settings kept are the Place tool's own.
        _kept ??= KeepPlaceTool();
        Tools.SetWorkshop(true);
        View.SetOverlay(Overlays.Layer.Borders, false);
        View.ShowNewMarkers = false;
        CutBox.IsVisible = true;
        CutSlider.Value = CutOff;
        // Solid pieces (the View panel and its See-through switch are away).
        View.SeeThroughBuildings = false;
        ShowRight(null);
        ViewButton.IsVisible = false;
        MaskPanel.Card.IsVisible = false;
        BuildPanel.Start();
    }

    /// <summary>
    /// Out of building: the Place tool, the zone borders and the View panel as they were.
    /// </summary>
    private void LeaveBuilding()
    {
        if (_kept is not { } k)
        {
            return;
        }
        _kept = null;
        RestorePlaceTool(k);
        Tools.SetWorkshop(false);
        View.SetOverlay(Overlays.Layer.Borders, k.Borders);
        CutBox.IsVisible = false;
        CutSlider.Value = CutOff;
        View.ShowNewMarkers = UnsavedBox.IsChecked == true;
        View.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
        ViewButton.IsVisible = true;
        BuildPanel.Card.IsVisible = false;
        PlacePanel.Fill();
        PlaceTool.Notify();
    }

    /// <summary>
    /// The cut at so many metres above the plot (0: the ground; <see cref="CutOff"/>: none): nothing drawn above it,
    /// and the cursor goes through what it hides.
    /// </summary>
    private void SetCut(float metres)
    {
        float? y = metres < CutOff ? Workshop.Ground + metres : null;
        View.CutY = y;
        PlaceTool.CutY = y;
        CutText.Text = metres < CutOff ? Lang.T("workshop.cutAt", metres) : Lang.T("workshop.cutOff");
        PlaceInput.Refresh();
    }
}
