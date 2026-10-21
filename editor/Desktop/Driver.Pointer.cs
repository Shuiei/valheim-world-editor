
namespace TerrainEditor.Desktop;

/// <summary>
/// The commands that move the mouse and the wheel over the 3D view, with real pointer events.
/// </summary>
public static partial class Driver
{
    /// <summary>Key modifiers from their names (shift, ctrl, alt; others ignored).</summary>
    internal static Avalonia.Input.KeyModifiers Mods(IEnumerable<string> names)
    {
        var m = Avalonia.Input.KeyModifiers.None;
        foreach (string n in names)
        {
            m |= n switch { "shift" => Avalonia.Input.KeyModifiers.Shift, "ctrl" => Avalonia.Input.KeyModifiers.Control, "alt" => Avalonia.Input.KeyModifiers.Alt, _ => Avalonia.Input.KeyModifiers.None };
        }
        return m;
    }

    /// <summary>Where world x, z (on the ground) is over the 3D view; throws when it is not in view.</summary>
    private static Avalonia.Point ScreenAt(MainWindow w, float x, float z)
    {
        var s = w.View.Scene ?? throw new InvalidOperationException("no area open");
        var g = new System.Numerics.Vector2(x - s.OriginX, z - s.OriginZ);
        return w.View.ScreenOfGrid(g, 0) ?? throw new InvalidOperationException($"{x}, {z} is not in view (camera: {w.View.CameraSays()})");
    }

    /// <summary>The one mouse pointer the driver's events come from.</summary>
    private static readonly Avalonia.Input.Pointer TheMouse = new(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);

    /// <summary>A real pointer event over the 3D view: down, move (with the button held) or up.</summary>
    private static void Mouse(MainWindow w, string what, Avalonia.Point p, string button, Avalonia.Input.KeyModifiers mods)
    {
        var raw = (Avalonia.Input.RawInputModifiers)mods;
        var (kind, down) = button switch
        {
            "right" => (what == "up" ? Avalonia.Input.PointerUpdateKind.RightButtonReleased : Avalonia.Input.PointerUpdateKind.RightButtonPressed, Avalonia.Input.RawInputModifiers.RightMouseButton),
            "middle" => (what == "up" ? Avalonia.Input.PointerUpdateKind.MiddleButtonReleased : Avalonia.Input.PointerUpdateKind.MiddleButtonPressed, Avalonia.Input.RawInputModifiers.MiddleMouseButton),
            _ => (what == "up" ? Avalonia.Input.PointerUpdateKind.LeftButtonReleased : Avalonia.Input.PointerUpdateKind.LeftButtonPressed, Avalonia.Input.RawInputModifiers.LeftMouseButton),
        };
        var surface = w.Surface;
        switch (what)
        {
            case "down":
                _held = down;
                surface.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw | down, kind), mods, 1));
                break;
            case "move":
                surface.RaiseEvent(new Avalonia.Input.PointerEventArgs(Avalonia.Input.InputElement.PointerMovedEvent, surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw | _held, Avalonia.Input.PointerUpdateKind.Other), mods));
                break;
            default:
                _held = Avalonia.Input.RawInputModifiers.None;
                surface.RaiseEvent(new Avalonia.Input.PointerReleasedEventArgs(surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw, kind), mods, kind == Avalonia.Input.PointerUpdateKind.RightButtonReleased ? Avalonia.Input.MouseButton.Right : kind == Avalonia.Input.PointerUpdateKind.MiddleButtonReleased ? Avalonia.Input.MouseButton.Middle : Avalonia.Input.MouseButton.Left));
                break;
        }
    }

    /// <summary>The mouse button held since the last down, for the moves.</summary>
    private static Avalonia.Input.RawInputModifiers _held;

    /// <summary>The 'mouse' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMouse(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        var p = ScreenAt(w, call.F(1), call.F(2));
        string button = args.Length > 3 ? args[3] : "left";
        Mouse(w, args[0], p, button, Mods(args.Skip(4)));
        return Task.FromResult(State(w));
    }

    /// <summary>The 'mouse3' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMouse3(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        // A point in the air: world x, height above the ground, z (pieces, for the Workshop).
        var sc = w.View.Scene ?? throw new InvalidOperationException("no area open");
        var g = new System.Numerics.Vector2(call.F(1) - sc.OriginX, call.F(3) - sc.OriginZ);
        var p = w.View.ScreenOfGrid(g, call.F(2)) ?? throw new InvalidOperationException($"not in view (camera: {w.View.CameraSays()})");
        Mouse(w, args[0], p, args.Length > 4 ? args[4] : "left", Mods(args.Skip(5)));
        return Task.FromResult(State(w));
    }

    /// <summary>The 'wheel' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdWheel(Call call)
    {
        MainWindow w = call.W;
        var p = ScreenAt(w, call.F(0), call.F(1));
        var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);
        w.Surface.RaiseEvent(new Avalonia.Input.PointerWheelEventArgs(w.Surface, pointer, w.Surface, p, 0, new Avalonia.Input.PointerPointProperties(), Avalonia.Input.KeyModifiers.None, new Avalonia.Vector(0, call.F(2))));
        return Task.FromResult(State(w));
    }
}
