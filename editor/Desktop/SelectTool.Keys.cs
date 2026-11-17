
namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The tool's keys (Esc, Del; with a selection: , . turn by <see cref="TurnStep"/>, R turns a quarter, PgUp PgDn
    /// lift by <see cref="LiftStep"/>, End drops; Shift for finer steps, as in Build and Paste); true when the key was one.
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
        // The building bar's steps; Shift, finer: 1° at most, a fifth of the lift.
        float step = shift ? MathF.Min(BuildingBar.FineTurn, TurnStep) : TurnStep;
        float lift = shift ? MathF.Round(LiftStep / 5, 2) : LiftStep;
        switch (key)
        {
            case Avalonia.Input.Key.OemComma:
                Turn(-step);
                return true;
            case Avalonia.Input.Key.OemPeriod:
                Turn(step);
                return true;
            // A quarter turn, as R turns the paste and the Place preview.
            case Avalonia.Input.Key.R:
                Turn(90);
                return true;
            case Avalonia.Input.Key.PageUp:
                Lift(lift);
                return true;
            case Avalonia.Input.Key.PageDown:
                Lift(-lift);
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
