using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TerrainEditor.Desktop;

// The mouse and the keys: the surface over the view hands its pointer events here (Attach), the window its
// keys; the wheel, and what each tool does with a click or a drag. The handlers live in the GlView.Input.*.cs files.
public sealed partial class GlView
{
    /// <summary>The keys held now (moving the camera with them).</summary>
    private readonly HashSet<Key> _keys = [];
    /// <summary>Where a mouse drag started, null when none is on.</summary>
    private Point? _dragFrom;
    /// <summary>The mouse button the drag is made with.</summary>
    private PointerUpdateKind _dragButton;

    /// <summary>
    /// Alt + wheel: the tools that turn take it (the window decides: true when taken); otherwise it zooms.
    /// The direction is that of the , and . keys: towards you (down) as . and away (up) as ,.
    /// </summary>
    internal Func<Key, bool, bool>? AltWheel { get; set; }
    /// <summary>
    /// Ctrl + wheel (up: 1, down: -1; with Shift): what the tool does with it (true), else the view zooms.
    /// </summary>
    internal Func<int, bool, bool>? CtrlWheel { get; set; }
    /// <summary>The wheel's turns not used yet (a touchpad gives fractions of a notch).</summary>
    private double _wheelSum;
    /// <summary>For tests: the keys held.</summary>
    internal Key[] KeysHeld { get { lock (_keys) { return [.. _keys]; } } }

    /// <summary>Where the mouse button went down, to tell a click from a drag.</summary>
    private Point? _pressAt;
    /// <summary>The press's click count (2: a double click).</summary>
    private int _pressClicks;

    /// <summary>The drag slides the view (Space + left, Shift + right).</summary>
    internal bool Panning { get; private set; }

    /// <summary>
    /// Mouse and keys. The OpenGL picture is not something Avalonia can hit-test (pointer events go
    /// through it), so a transparent surface laid over the view takes them and hands them here; keys
    /// come from the window, whatever has the focus.
    /// </summary>
    public void Attach(Control surface, Window window)
    {
        // A click on the view takes the keyboard from a box typed in (its keys are the box's while it has the focus),
        // so the camera keys work again; not a Tab stop.
        surface.Focusable = true;
        KeyboardNavigation.SetIsTabStop(surface, false);
        surface.PointerPressed += (_, e) => OnPointerPressed(surface, e);
        surface.PointerReleased += (_, e) => OnPointerReleased(surface, e);
        // The pointer taken away mid-drag (the system's capture ended: a dialog, another window): what was held ends
        // where the pointer last was, as a release there (no click), so no stroke or drag keeps going until the next
        // click.
        surface.PointerCaptureLost += (_, _) => Up(_heldAt, surface.Bounds.Size, KeyModifiers.None, click: false);
        surface.PointerMoved += (_, e) => OnPointerMoved(surface, e);
        surface.PointerExited += (_, _) => OnPointerExited();
        surface.PointerWheelChanged += (_, e) => OnPointerWheel(e);
        // Space is the view's (held: Space + left drag slides, flying goes up), not the focused
        // button's, which it would press; typing keeps it.
        window.AddHandler(KeyDownEvent, (_, e) => OnSpaceKey(surface, e, down: true), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        window.AddHandler(KeyUpEvent, (_, e) => OnSpaceKey(surface, e, down: false), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        window.KeyDown += (_, e) => OnWindowKeyDown(surface, e);
        window.KeyUp += (_, e) => OnWindowKeyUp(e);
        // Keys held when the window loses the focus would keep moving the camera; so would a key held while a box
        // takes the focus (its release goes to the box).
        window.Deactivated += (_, _) => ClearKeys();
        // So would one held while the editor is hidden: its release, on the start page or the map, is not the view's.
        surface.DetachedFromVisualTree += (_, _) => ClearKeys();
        // Handled ones too: a box (or a control around it) marking the focus event handled still took the focus.
        window.AddHandler(GotFocusEvent, (_, e) => ClearKeysForTextBox(e.Source), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// Whether a key pressed in the window is the view's: not while typing in a box (its keys are its own: an F
    /// there is a letter, not a change of eye), and only while the 3D editor is shown (the <paramref name="surface"/>
    /// is in the window), not on the start page or the map, where W would move a camera nobody sees.
    /// </summary>
    private static bool TakesKey(Control surface, KeyEventArgs e) => e.Source is not TextBox && surface.IsAttachedToVisualTree();

    /// <summary>Whether Ctrl is held for a pointer action (add to the selection, remove a point…), or Cmd, the Mac's Ctrl (where
    /// Ctrl + click is a right-click); as for the keyboard shortcuts.</summary>
    private static bool Ctrl(KeyModifiers m) => m.HasFlag(KeyModifiers.Control) || m.HasFlag(KeyModifiers.Meta);

    /// <summary>
    /// The button let go on the surface: a release while the eyedropper swallowed its press does nothing else;
    /// otherwise it ends the drags.
    /// </summary>
    /// <param name="surface">The surface the release is on.</param>
    /// <param name="e">The release.</param>
    private void OnPointerReleased(Control surface, PointerReleasedEventArgs e)
    {
        if (_pickSwallow)
        {
            _pickSwallow = false;
            _pressAt = null;
            _dragFrom = null;
            e.Pointer.Capture(null);
            return;
        }
        Up(e.GetPosition(surface), surface.Bounds.Size, e.KeyModifiers, click: true);
        e.Pointer.Capture(null);
    }
}
