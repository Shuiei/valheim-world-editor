using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>Enter: places what the line, grid or zone shows.</summary>
    public void PlaceShape()
    {
        if (_preview.Count == 0 || Session() is not { } session)
        {
            Message?.Invoke(Tool.Mode == PlaceTool.Modes.Line ? Said.Of("place.msg.drawLine") : Tool.Mode == PlaceTool.Modes.Zone ? Said.Of("place.msg.drawZone") : Said.Of("place.msg.dragBox"));
            return;
        }
        var list = _preview.ToList();
        string label = Tool.Mode == PlaceTool.Modes.Line ? Lang.T("place.undo.placedLine", list.Count)
            : Tool.Mode == PlaceTool.Modes.Zone ? Lang.T("place.undo.placedZone", list.Count) : Lang.T("place.undo.placedGrid", list.Count);
        Commit(session, list, label);
        Placed?.Invoke([.. list.Select(o => o.Name).Distinct()]);
        Message?.Invoke(Said.Of("place.msg.placed", list.Count));
        Refresh();
    }

    /// <summary>Removes the line's or zone's last point (Backspace).</summary>
    public void RemoveLastPoint()
    {
        if (Pointed && Tool.Points.Count > 0)
        {
            lock (Tool.Points) { Tool.Points.RemoveAt(Tool.Points.Count - 1); }
            Tool.Notify();
        }
    }
}
