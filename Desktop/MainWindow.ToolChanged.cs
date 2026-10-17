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
        ShapePanel.Card.IsVisible = Tools.Mode == ToolMode.Shape;
        MountainPanel.Card.IsVisible = Tools.Mode == ToolMode.Mountain;
        ScriptPanel.Card.IsVisible = Tools.Mode == ToolMode.Script;
        if (Blueprints.Card.IsVisible != (Tools.Mode == ToolMode.Blueprints))
        {
            Blueprints.Toggle(Tools.Mode == ToolMode.Blueprints);
        }
        View.ShapeRadius = Tools.Mode == ToolMode.Mountain ? MountainPanel.Spec.Reach : ShapePanel.Radius;
        View.MountainPreview = Tools.Mode == ToolMode.Mountain ? Mountain.Shape(MountainPanel.Spec) : null;
        PathPanel.Card.IsVisible = Tools.Mode == ToolMode.Path;
        if (Tools.Mode == ToolMode.Path)
        {
            Tools.MarkCave(View.Path.Act == PathTool.Action.Cave);
        }
        AreaPanel.Card.IsVisible = Tools.Mode == ToolMode.Area;
        PastePanel.Card.IsVisible = Tools.Mode == ToolMode.Paste;
        if (BuildingInDungeon && Tools.Mode != ToolMode.Place)
        {
            if (Tools.Mode != ToolMode.Dungeon)
            {
                DungeonPanel.Leave();
            }
            BuildInDungeon(false);
        }
        PlacePanel.Card.IsVisible = Tools.Mode == ToolMode.Place && !InWorkshop && !BuildingInDungeon;
        BuildPanel.Card.IsVisible = Tools.Mode == ToolMode.Place && (InWorkshop || BuildingInDungeon);
        if (Tools.Mode != ToolMode.Place)
        {
            PlacePanel.Chooser.IsVisible = false;
            PlacePanel.KindsButton.Content = PlacePanel.ChooseKindsText;
        }
        PlaceInput.Refresh();
        MaskPanel.Card.IsVisible = !InWorkshop && Tools.Mode is ToolMode.Brush or ToolMode.Path or ToolMode.Area or ToolMode.Shape or ToolMode.Mountain or ToolMode.Place;
        if (Tools.Mode == ToolMode.Area)
        {
            AreaPanel.Refresh();
        }
    }
}
