using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The mouse wheel: Ctrl and Alt turn the tool in use when it takes the turn, otherwise the view zooms.
public sealed partial class GlView
{
    /// <summary>
    /// The wheel: Ctrl or Alt turns the tool in use when it takes the turn; otherwise, and when nothing takes it,
    /// the view zooms (orbit eye only).
    /// </summary>
    /// <param name="e">The wheel.</param>
    private void OnPointerWheel(PointerWheelEventArgs e)
    {
        // Ctrl or Alt + wheel: one step per notch. Smooth-scrolling wheels and touchpads send parts of a
        // notch (and some systems send Shift's as sideways): added up until a whole one.
        bool ctrlW = Ctrl(e.KeyModifiers), altW = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (WheelWanted(ctrlW, altW) && TurnTool(e.Delta, e.KeyModifiers.HasFlag(KeyModifiers.Shift), ctrlW))
        {
            e.Handled = true;
            Wake();
            return;
        }
        _wheelSum = 0;
        lock (_camLock)
        {
            if (_eye == EyeMode.Orbit)
            {
                _distance = Math.Clamp(_distance * MathF.Pow(0.88f, (float)e.Delta.Y), 3, 3000);
            }
        }
        Wake();
    }

    /// <summary>
    /// Whether the wheel with these modifiers goes to a tool: Ctrl to the building or paste tools, Alt to the tools
    /// that turn things.
    /// </summary>
    /// <param name="ctrlW">Ctrl is held.</param>
    /// <param name="altW">Alt is held.</param>
    /// <returns>True when a tool may take the wheel.</returns>
    internal bool WheelWanted(bool ctrlW, bool altW)
    {
        // Ctrl: building lifts the piece; Alt: the tools that turn things turn them. Else the view zooms.
        return ctrlW
            ? (Mode == ToolMode.Place && Place?.Tool.Building == true || Mode == ToolMode.Paste) && CtrlWheel != null
            : altW && AltWheel != null && Mode is ToolMode.Place or ToolMode.Paste or ToolMode.Select;
    }

    /// <summary>
    /// Adds the wheel's movement to the sum and gives each whole notch to the tool. Nothing to turn (or lift): the
    /// wheel zooms as usual.
    /// </summary>
    /// <param name="movement">The wheel's movement (Y, or X when Y is 0: some systems send Shift's as sideways).</param>
    /// <param name="shiftW">Shift is held.</param>
    /// <param name="ctrlW">Ctrl is held: the lift wheel, else the turn wheel.</param>
    /// <returns>True when the wheel was used up (no zoom); false when the view zooms.</returns>
    internal bool TurnTool(Vector movement, bool shiftW, bool ctrlW)
    {
        double delta = movement.Y != 0 ? movement.Y : movement.X;
        _wheelSum += delta;
        bool tried = false, used = false;
        while (Math.Abs(_wheelSum) >= 0.999)
        {
            int dir = _wheelSum > 0 ? 1 : -1;
            _wheelSum -= dir;
            tried = true;
            used |= ctrlW ? CtrlWheel!(dir, shiftW) : AltWheel!(dir > 0 ? Key.OemComma : Key.OemPeriod, shiftW);
        }
        return !tried || used;
    }
}
