using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>Switches the rail to the Workshop's (Build, Select and View) or back; switching on, another tool in use goes back to View.</summary>
    public void SetWorkshop(bool on)
    {
        Workshop = on;
        foreach (var c in _rail.Children)
        {
            c.IsVisible = !on || c == _buildButton || c == SelectButton || c == _viewButton;
        }
        if (on && (Tool != null || Mode is not (ToolMode.View or ToolMode.Place or ToolMode.Select)))
        {
            Choose(null);
        }
        else if (Mode == ToolMode.Place)
        {
            // The plot's placing tool is Build; a world's comes back as Place (the window shows the panels).
            BuildMode = on;
            _placeButton.Classes.Set("on", !on);
            _buildButton.Classes.Set("on", on);
        }
    }

    /// <summary>
    /// A brush (a paint: the Paint brush laying it), or (null) the mode's tool. In the Workshop only View, Build and
    /// Select can be picked.
    /// </summary>
    /// <param name="t">The brush, or null for the mode's tool.</param>
    /// <param name="mode">The tool, when no brush is given.</param>
    /// <param name="build">With Place: the Build tool (the Build dock) instead of the Place panel. Always so in the Workshop.</param>
    public void Choose(BrushTool? t, ToolMode mode = ToolMode.View, bool build = false)
    {
        if (Workshop && (t != null || mode is not (ToolMode.View or ToolMode.Place or ToolMode.Select)))
        {
            return;
        }
        Tool = t;
        Mode = t != null ? ToolMode.Brush : mode;
        BuildMode = Mode == ToolMode.Place && (build || Workshop);
        _viewButton.Classes.Set("on", Mode == ToolMode.View);
        SelectButton.Classes.Set("on", Mode == ToolMode.Select);
        _measureButton.Classes.Set("on", Mode == ToolMode.Measure);
        _landformButton.Classes.Set("on", Mode == ToolMode.Landform);
        _pathButton.Classes.Set("on", Mode == ToolMode.Path);
        _areaButton.Classes.Set("on", Mode is ToolMode.Area or ToolMode.Paste);
        _placeButton.Classes.Set("on", Mode == ToolMode.Place && !BuildMode);
        _buildButton.Classes.Set("on", BuildMode);
        _scriptButton.Classes.Set("on", Mode == ToolMode.Script);
        _dungeonButton.Classes.Set("on", Mode == ToolMode.Dungeon);
        BlueprintsButton.Classes.Set("on", Mode == ToolMode.Blueprints);
        if (Mode != ToolMode.Mod)
        {
            ModTool = null;
        }
        MarkModTool();
        foreach (var (k, b) in _buttons)
        {
            b.Classes.Set("on", k == t);
        }
        bool paint = t is BrushTool p && IsPaint(p);
        if (paint)
        {
            // The Paint brush keeps the paint: its button brings it back, in its colour.
            Paint = t!.Value;
            _paintSwatch.Background = PaintColour(Paint);
        }
        _paintButton.Classes.Set("on", paint);
        foreach (var (k, b) in PaintButtons)
        {
            b.Classes.Set("on", k == t);
        }
        Options.IsVisible = t != null;
        if (t is BrushTool tool)
        {
            _title.Text = paint ? Lang.T("rail.label.paint") : Brush.Label(tool);
            _help.Text = Lang.T("brush.helpHold", Brush.Help(tool));
            _paintRows.IsVisible = paint;
            _flattenRows.IsVisible = tool == BrushTool.Flatten;
            _naturalRows.IsVisible = tool == BrushTool.Natural;
            _erodeRows.IsVisible = tool == BrushTool.Erode;
            SyncStamp();
        }
        ToolChanged?.Invoke(t);
        if (BuildMode)
        {
            // The Build dock takes the bottom of the window and the rail scrolls: its Build button stays in sight.
            Avalonia.Threading.Dispatcher.UIThread.Post(_buildButton.BringIntoView, Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}
