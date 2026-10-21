using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>Switches the rail to the Workshop's (Place shown as Build) or back; switching on, another tool in use goes back to View.</summary>
    public void SetWorkshop(bool on)
    {
        Workshop = on;
        foreach (var c in _rail.Children)
        {
            c.IsVisible = !on || c == _placeButton || c == SelectButton || c == _viewButton;
        }
        var label = ((_placeButton.Content as Grid)!.Children[0] as StackPanel)!.Children[1] as TextBlock;
        label!.Text = on ? Lang.T("rail.label.build") : Lang.T("rail.label.place");
        ToolTip.SetTip(_placeButton, Lang.T(on ? "rail.tip.build" : "rail.tip.place"));
        if (on && (Tool != null || Mode is not (ToolMode.View or ToolMode.Place or ToolMode.Select)))
        {
            Choose(null);
        }
    }

    /// <summary>A brush, or (null) the mode's tool. In the Workshop only View, Place and Select can be picked.</summary>
    public void Choose(BrushTool? t, ToolMode mode = ToolMode.View)
    {
        if (Workshop && (t != null || mode is not (ToolMode.View or ToolMode.Place or ToolMode.Select)))
        {
            return;
        }
        Tool = t;
        Mode = t != null ? ToolMode.Brush : mode;
        _viewButton.Classes.Set("on", Mode == ToolMode.View);
        SelectButton.Classes.Set("on", Mode == ToolMode.Select);
        _measureButton.Classes.Set("on", Mode == ToolMode.Measure);
        _shapeButton.Classes.Set("on", Mode == ToolMode.Shape);
        _pathButton.Classes.Set("on", Mode == ToolMode.Path);
        _areaButton.Classes.Set("on", Mode is ToolMode.Area or ToolMode.Paste);
        _placeButton.Classes.Set("on", Mode == ToolMode.Place);
        _mountainButton.Classes.Set("on", Mode == ToolMode.Mountain);
        _scriptButton.Classes.Set("on", Mode == ToolMode.Script);
        _dungeonButton.Classes.Set("on", Mode == ToolMode.Dungeon);
        BlueprintsButton.Classes.Set("on", Mode == ToolMode.Blueprints);
        _caveButton.Classes.Set("on", false);
        if (Mode != ToolMode.Mod)
        {
            ModTool = null;
        }
        MarkModTool();
        foreach (var (k, b) in _buttons)
        {
            b.Classes.Set("on", k == t);
        }
        Options.IsVisible = t != null;
        if (t is BrushTool tool)
        {
            _title.Text = Brush.Label(tool);
            _help.Text = Lang.T("brush.helpHold", Brush.Help(tool));
            _flattenRows.IsVisible = tool == BrushTool.Flatten;
            _naturalRows.IsVisible = tool == BrushTool.Natural;
            _erodeRows.IsVisible = tool == BrushTool.Erode;
            SyncStamp();
        }
        ToolChanged?.Invoke(t);
    }
}
