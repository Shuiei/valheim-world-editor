
namespace TerrainEditor.Desktop;

public sealed partial class PlaceInput
{
    /// <summary>The tool's keys; true when the key was one.</summary>
    public bool Key(Avalonia.Input.Key key, bool shift, bool ctrl)
    {
        if (ctrl)
        {
            return false;
        }
        // The Workshop turns pieces by its own step (Shift: by 1°).
        float step = TurnStep is float ts ? shift ? 1 : ts : shift ? 15 : 1;
        bool shape = Tool.Mode != PlaceTool.Modes.Brush;
        switch (key)
        {
            case Avalonia.Input.Key.PageUp:
            case Avalonia.Input.Key.PageDown:
                Message?.Invoke(Tool.NudgeElevation((key == Avalonia.Input.Key.PageUp ? 1 : -1) * (shift ? 0.1f : 0.5f)));
                return true;
            case Avalonia.Input.Key.R:
                Tool.NewLayout();
                return true;
            case Avalonia.Input.Key.OemComma:
            case Avalonia.Input.Key.OemPeriod:
                TurnPlacement((key == Avalonia.Input.Key.OemComma ? -1 : 1) * step);
                return true;
            case Avalonia.Input.Key.Enter when shape:
                PlaceShape();
                return true;
            default:
                return EditKey(key, shape);
        }
    }
}
