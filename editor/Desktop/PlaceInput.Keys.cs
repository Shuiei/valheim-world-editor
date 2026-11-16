
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
        // Building turns pieces by the building bar's step (Shift, finer: by 1° at most).
        float step = TurnStep is float ts ? shift ? MathF.Min(BuildingBar.FineTurn, ts) : ts : shift ? 1 : 15;
        bool shape = Tool.Mode != PlaceTool.Modes.Brush;
        switch (key)
        {
            case Avalonia.Input.Key.PageUp:
            case Avalonia.Input.Key.PageDown:
                Raise(key == Avalonia.Input.Key.PageUp ? 1 : -1, shift);
                return true;
            // Snapping by hand: Q and E choose the piece's snap point (E is Select otherwise).
            case Avalonia.Input.Key.Q or Avalonia.Input.Key.E when Tool.Hammering && Tool.SnapMode == PlaceTool.SnapModes.Manual:
                CycleSnap(key == Avalonia.Input.Key.E ? 1 : -1);
                return true;
            case Avalonia.Input.Key.N:
                Tool.NewLayout();
                return true;
            // R turns a quarter wherever something turns: the preview here, the paste, the selection.
            case Avalonia.Input.Key.R:
                TurnPlacement(90);
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

    /// <summary>
    /// PgUp, PgDn or a notch of Ctrl + wheel: building, the piece is lifted (or lowered) from where the cursor points;
    /// otherwise the elevation the objects stand at goes up or down. Shift: finer.
    /// </summary>
    /// <param name="dir">1 up, -1 down.</param>
    /// <param name="shift">Shift is held: finer.</param>
    public void Raise(int dir, bool shift)
    {
        if (Tool.Building)
        {
            Lift(dir * LiftBy(shift));
        }
        else
        {
            Message?.Invoke(Tool.NudgeElevation(dir * LiftBy(shift)));
        }
    }

    /// <summary>
    /// Manual snapping: the piece's next (1) or previous (-1) snap point is the one that meets the others; the preview
    /// follows.
    /// </summary>
    public void CycleSnap(int step)
    {
        Tool.SnapPoint += step;
        Refresh();
        Tool.Notify();
    }

    /// <summary>
    /// Building: Alt pressed (true) or let go, whether the pointer moved or not; the preview follows, so the next click
    /// places what it shows.
    /// </summary>
    public void Alt(bool held)
    {
        if (!Tool.Hammering || Tool.Fine == held)
        {
            return;
        }
        Tool.Fine = held;
        Refresh();
    }
}
