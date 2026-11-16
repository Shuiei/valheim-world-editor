namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Another tool chosen: the 3D view takes it, its panel shows and the others hide, and what the last tool left
    /// (the dungeon's cut, building in the dungeon, the kinds chooser) is put away.
    /// </summary>
    /// <param name="t">The brush chosen, if the tool is a brush.</param>
    private void OnToolChanged(BrushTool? t)
    {
        View.Tool = t;
        View.Mode = Tools.Mode;
        ModToolChanged();
        SelectPanel.Card.IsVisible = Tools.SelectMode;
        MeasurePanel.Card.IsVisible = Tools.Mode == ToolMode.Measure;
        if (Tools.Mode == ToolMode.Measure)
        {
            // The ground may have changed since the tape was drawn.
            MeasurePanel.Refresh();
        }
        if (Tools.Mode == ToolMode.Dungeon)
        {
            DungeonPanel.Reload();
        }
        else if (DungeonPanel.Card.IsVisible && !BuildingInDungeon)
        {
            // Not into Build here (it builds under the dungeon's cut): the cut off.
            DungeonPanel.Leave();
        }
        DungeonPanel.Card.IsVisible = Tools.Mode == ToolMode.Dungeon;
        LandformPanel.Card.IsVisible = Tools.Mode == ToolMode.Landform;
        ScriptPanel.Card.IsVisible = Tools.Mode == ToolMode.Script;
        if (Blueprints.Card.IsVisible != (Tools.Mode == ToolMode.Blueprints))
        {
            Blueprints.Toggle(Tools.Mode == ToolMode.Blueprints);
        }
        ShowLandform();
        PathPanel.Card.IsVisible = Tools.Mode == ToolMode.Path;
        AreaPanel.Card.IsVisible = Tools.Mode == ToolMode.Area;
        PastePanel.Card.IsVisible = Tools.Mode == ToolMode.Paste;
        // Build here ends with any other tool, the Place tool too (the Build tool is the one it runs under).
        if (BuildingInDungeon && !Tools.BuildMode)
        {
            if (Tools.Mode != ToolMode.Dungeon)
            {
                DungeonPanel.Leave();
            }
            BuildInDungeon(false);
        }
        // The Build tool in a world's open ground: the Place tool set for building while it is on, its own settings back after.
        BuildInWorld(Tools.BuildMode && !InWorkshop && !BuildingInDungeon);
        PlacePanel.Card.IsVisible = Tools.Mode == ToolMode.Place && !Tools.BuildMode;
        BuildPanel.Card.IsVisible = Tools.BuildMode;
        if (!PlacePanel.Card.IsVisible)
        {
            PlacePanel.Chooser.IsVisible = false;
            PlacePanel.KindsButton.Content = PlacePanel.ChooseKindsText;
        }
        PlaceInput.Refresh();
        ShowBuildingBar();
        // The Mask: with the tools it holds back (Build puts each piece where it is pointed at).
        MaskPanel.Card.IsVisible = !InWorkshop && !Tools.BuildMode && Tools.Mode is ToolMode.Brush or ToolMode.Path or ToolMode.Area or ToolMode.Landform or ToolMode.Place;
        MaskPanel.ShowNoLimit(GovernedByNoLimit(Tools.Mode, t));
        if (Tools.Mode == ToolMode.Area)
        {
            AreaPanel.Refresh();
        }
        else if (Tools.Mode == ToolMode.Select)
        {
            // Replace with offers what the world can make now (a live merge or a mod's catalogue brought kinds).
            FillReplaceKinds();
        }
    }

    /// <summary>
    /// Whether a tool keeps to the game's ±8 m unless No limit is on, so the Mask card shows the switch with it: the
    /// sculpt brushes (and their stamps), Landform (its formulas and pictures; its panel says that a mountain always
    /// goes past the limit), Path and Region (its heightmap imports too). Not the Paint brush and Place, which move no
    /// ground.
    /// </summary>
    /// <param name="mode">The tool.</param>
    /// <param name="brush">The brush, when the tool is one.</param>
    internal static bool GovernedByNoLimit(ToolMode mode, BrushTool? brush) =>
        mode is ToolMode.Path or ToolMode.Area or ToolMode.Landform || mode == ToolMode.Brush && brush is { } b && !ToolPanel.IsPaint(b);
}
