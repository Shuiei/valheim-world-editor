
namespace TerrainEditor.Desktop;

public sealed partial class AreaTool
{
    /// <summary>Enter on an open polygon closes it (true); otherwise Enter applies the action.</summary>
    public bool CloseWithEnter()
    {
        if (!Box && !Closed && Points.Count >= 3)
        {
            Closed = true;
            Notify();
            return true;
        }
        return false;
    }

    /// <summary>Backspace: removes the polygon's last point.</summary>
    public void RemoveLast()
    {
        if (!Box && Points.Count > 0)
        {
            lock (Points)
            {
                Points.RemoveAt(Points.Count - 1);
            }
            Notify();
        }
    }
}
