namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The world tools away (the rail keeps Build, Select and View), the View panel and the Mask closed, no zone
    /// borders; the Place tool set for building.
    /// </summary>
    private void EnterBuilding()
    {
        // Building in a dungeon has ended already (ShowEditor's Edit ends it), and building in a world ends here, so the
        // settings kept are the Place tool's own.
        BuildInWorld(false);
        _kept ??= KeepPlaceTool();
        Tools.SetWorkshop(true);
        View.SetOverlay(Overlays.Layer.Borders, false);
        View.ShowNewMarkers = false;
        // The view's cut counts from the plot here, off at first.
        ViewCut.FromPlot(Workshop.Ground);
        // Solid pieces (the View panel and its See-through switch are away).
        View.SeeThroughBuildings = false;
        ViewButton.IsVisible = false;
        MaterialsButton.IsVisible = true;
        ShowRight(ChosenRight());
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
        // The view's cut counts from the ground again, off.
        ViewCut.FromGround(forget: true);
        View.ShowNewMarkers = UnsavedBox.IsChecked == true;
        View.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
        ViewButton.IsVisible = true;
        MaterialsButton.IsVisible = false;
        // The worlds' own right-hand panel again (the Workshop's choice is kept apart).
        ShowRight(ChosenRight());
        BuildPanel.Card.IsVisible = false;
        PlacePanel.Fill();
        PlaceTool.Notify();
    }

    /// <summary>
    /// The Place tool's settings from before the Build tool was chosen in a world (restored when another tool is); null
    /// when not building in a world's open ground.
    /// </summary>
    private Kept? _worldKept;

    /// <summary>True while the Build tool is on in a world's open ground (not the Workshop's plot, not Build here in a dungeon).</summary>
    internal bool BuildingInWorld => _worldKept != null;

    /// <summary>
    /// The Build tool in a world, on (the Place tool's own settings are kept aside and the Build dock starts: its
    /// pieces, one at a time or along lines; no Library, whose blueprints go onto the Workshop's plot) or off (the
    /// Place tool gets its settings back).
    /// </summary>
    /// <param name="on">Whether the Build tool is on.</param>
    internal void BuildInWorld(bool on)
    {
        if (on && _worldKept == null)
        {
            _worldKept = KeepPlaceTool();
            BuildPanel.Start(library: false);
        }
        else if (!on && _worldKept is { } k)
        {
            _worldKept = null;
            RestorePlaceTool(k);
            PlacePanel.Fill();
            PlaceTool.Notify();
        }
    }
}
