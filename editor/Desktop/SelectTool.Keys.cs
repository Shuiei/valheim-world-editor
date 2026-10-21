
namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The tool's keys (Esc, Del; with a selection: , . turn, PgUp PgDn lift, End drops; Shift for bigger steps);
    /// true when the key was one.
    /// </summary>
    public bool Key(Avalonia.Input.Key key, bool shift)
    {
        switch (key)
        {
            case Avalonia.Input.Key.Escape when _lasso != null || _box != null:
                StopZones();
                return true;
            case Avalonia.Input.Key.Escape:
                return Cancel();
            case Avalonia.Input.Key.Delete:
                Delete();
                return true;
            default:
                break;
        }
        if (_view.Selected.Count == 0)
        {
            return false;
        }
        // 1° (Shift: 15°), like the web editor.
        float step = shift ? 15 : 1;
        switch (key)
        {
            case Avalonia.Input.Key.OemComma:
                Turn(-step);
                return true;
            case Avalonia.Input.Key.OemPeriod:
                Turn(step);
                return true;
            case Avalonia.Input.Key.PageUp:
                Lift(shift ? 1 : 0.25f);
                return true;
            case Avalonia.Input.Key.PageDown:
                Lift(shift ? -1 : -0.25f);
                return true;
            case Avalonia.Input.Key.End:
                Drop();
                return true;
            default:
                break;
        }
        return false;
    }
}
