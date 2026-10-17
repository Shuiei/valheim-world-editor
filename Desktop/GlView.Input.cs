using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The mouse and the keys: the surface over the view hands its pointer events here (Attach), the window its
// keys; the wheel, and what each tool does with a click or a drag.
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

    /// <summary>
    /// The button let go (or the pointer lost): ends what the press started (a path, area, place, select drag or a
    /// brush stroke) and the view's sliding; a click (not a drag) in the View tool picks the object under the pointer.
    /// Nothing when nothing is held.
    /// </summary>
    /// <param name="at">Where the pointer is, on the surface.</param>
    /// <param name="size">The surface's size.</param>
    /// <param name="mods">The keys held.</param>
    /// <param name="click">Whether it is a real release, which may pick (not a lost pointer).</param>
    private void Up(Point at, Size size, KeyModifiers mods, bool click)
    {
        if (_pathDown)
        {
            _pathDown = false;
            Path.Up();
        }
        if (_areaDown)
        {
            _areaDown = false;
            Area.Up();
        }
        if (_placeDown)
        {
            _placeDown = false;
            Place?.Up(at, size);
        }
        if (_selectDown)
        {
            _selectDown = false;
            SelectTool.Up(at, size, mods.HasFlag(KeyModifiers.Shift) || Ctrl(mods));
            // A look queued by a move just before the press was dropped while the button was down. The handle
            // under the pointer is found again first: the drag's moves did not look for it, and over a handle no
            // object is outlined.
            SelectTool.Hover(at, size);
            QueueHoverPick();
        }
        else if (_brushDown)
        {
            _brushDown = false;
            var message = Scene?.Session?.EndStroke() ?? Said.Empty;
            StrokeEnded?.Invoke(message);
        }
        else if (click && Mode == ToolMode.View && !Panning && _dragButton == PointerUpdateKind.LeftButtonPressed && _pressAt is Point from && Point.Distance(from, at) < 5)
        {
            bool add = mods.HasFlag(KeyModifiers.Shift) || Ctrl(mods);
            // A plain double click on an object: the Select tool, to move it with the handles.
            if (Pick(at, size, add, mods.HasFlag(KeyModifiers.Shift) && !Ctrl(mods)) != null && _pressClicks == 2 && !add)
            {
                SelectToolAsked?.Invoke();
            }
        }
        _pressAt = null;
        _dragFrom = null;
        _pickSwallow = false;
        Panning = false;
        Wake();
    }

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
        surface.PointerPressed += (_, e) =>
        {
            surface.Focus(NavigationMethod.Pointer);
            var p = e.GetCurrentPoint(surface);
            _pressAt = p.Position;
            _pressClicks = e.ClickCount;
            _dragFrom = p.Position;
            _dragButton = p.Properties.PointerUpdateKind;
            _pointer = _heldAt = p.Position;
            _surfaceSize = surface.Bounds.Size;
            e.Pointer.Capture(surface);
            // The eyedropper waits: this click is its, not the tool's (nor is the release).
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && PickObjectOnce is { } pickOnce)
            {
                PickObjectOnce = null;
                _dragFrom = null;
                _pickSwallow = true;
                pickOnce(ObjectAt(p.Position, _surfaceSize));
                Wake();
                return;
            }
            // Space + left drag and Shift + right drag slide the view, whatever the tool (the web
            // editor's; handy without a middle button).
            bool space;
            lock (_keys)
            {
                space = _keys.Contains(Key.Space);
            }
            if (_eye == EyeMode.Orbit && (_dragButton == PointerUpdateKind.LeftButtonPressed && space || _dragButton == PointerUpdateKind.RightButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
            {
                Panning = true;
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Place && Place != null)
            {
                _dragFrom = null;
                _placeDown = true;
                var m = e.KeyModifiers;
                Place.Down(p.Position, _surfaceSize, m.HasFlag(KeyModifiers.Shift), Ctrl(m), m.HasFlag(KeyModifiers.Alt), e.ClickCount);
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Paste)
            {
                _dragFrom = null;
                if (GridAt(p.Position, _surfaceSize) is { } at)
                {
                    PasteClicked?.Invoke(at);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Area)
            {
                _dragFrom = null;
                _areaDown = true;
                Area.Down(GridAt(p.Position, _surfaceSize), e.ClickCount);
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Path)
            {
                _dragFrom = null;
                _pathDown = true;
                var mods = e.KeyModifiers;
                Path.Down(GridAt(p.Position, _surfaceSize), p.Position, g => ScreenOfGrid(g), Ctrl(mods), mods.HasFlag(KeyModifiers.Alt),
                    mods.HasFlag(KeyModifiers.Shift), GridHeight);
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode is ToolMode.Shape or ToolMode.Mountain)
            {
                _dragFrom = null;
                if (Scene is { } sc && GroundAt(sc, _lastViewProj, p.Position, _surfaceSize) is { } g)
                {
                    ShapeClicked?.Invoke(g.X, g.Z);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Dungeon)
            {
                _dragFrom = null;
                if (WorldRay(p.Position, _surfaceSize) is { } ray)
                {
                    Dungeon.Hover(ray.O, ray.D);
                }
                Dungeon.Click();
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Mode == ToolMode.Measure)
            {
                _dragFrom = null;
                if (WorldAt(p.Position, _surfaceSize) is { } w)
                {
                    Tape.Down(w);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool == null && SelectMode)
            {
                _dragFrom = null;
                _selectDown = true;
                // Ctrl + click (or Shift, Ctrl + zone): added to what is selected; Shift + click: the row
                // from the last object clicked.
                SelectTool.Range = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !Ctrl(e.KeyModifiers);
                SelectTool.Down(p.Position, _surfaceSize, e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Ctrl(e.KeyModifiers), e.KeyModifiers.HasFlag(KeyModifiers.Alt), e.ClickCount);
                if (e.ClickCount == 2)
                {
                    // A double click on an object: its inspector (a chest's contents, a sign's text...).
                    Dispatcher.UIThread.Post(() => InspectAsked?.Invoke());
                }
                Wake();
                return;
            }
            // With a brush, the left button paints (the middle one still slides the view); Alt + click picks
            // the ground's height (Alt + Shift: for the Mask).
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool is BrushTool && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                _dragFrom = null;
                if (WorldAt(p.Position, _surfaceSize) is { } w)
                {
                    BrushAltClick?.Invoke(w.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                }
                return;
            }
            // Stamp once: a click of Raise or Lower puts the whole stamp in.
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool is BrushTool.Raise or BrushTool.Lower && Scene is { Session.Brush: { StampOnce: true, Shape: BrushShape.Stamp } } ss)
            {
                _dragFrom = null;
                if (GroundAt(ss, _lastViewProj, p.Position, _surfaceSize) is { } at)
                {
                    StampClicked?.Invoke(at.X, at.Z);
                }
                Wake();
                return;
            }
            if (_dragButton == PointerUpdateKind.LeftButtonPressed && Tool is BrushTool tool && Scene is { Session: { } session } s)
            {
                _dragFrom = null;
                if (GroundAt(s, _lastViewProj, p.Position, _surfaceSize) is { } at)
                {
                    session.BeginStroke(tool, at.X, at.Z);
                    _brushDown = true;
                }
            }
            Wake();
        };
        surface.PointerReleased += (_, e) =>
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
        };
        // The pointer taken away mid-drag (the system's capture ended: a dialog, another window): what was held ends
        // where the pointer last was, as a release there (no click), so no stroke or drag keeps going until the next
        // click.
        surface.PointerCaptureLost += (_, _) => Up(_heldAt, surface.Bounds.Size, KeyModifiers.None, click: false);
        surface.PointerMoved += (_, e) =>
        {
            _pointer = _heldAt = e.GetPosition(surface);
            _surfaceSize = surface.Bounds.Size;
            if (_pathDown)
            {
                Path.Moved(GridAt(_pointer.Value, _surfaceSize));
            }
            if (_areaDown)
            {
                Area.Moved(GridAt(_pointer.Value, _surfaceSize));
            }
            if (Mode == ToolMode.Paste)
            {
                Paste.At = GridAt(_pointer.Value, _surfaceSize);
            }
            if (Mode == ToolMode.Place)
            {
                Place?.Moved(_pointer.Value, _surfaceSize);
            }
            if (_selectDown)
            {
                SelectTool.Moved(_pointer.Value, _surfaceSize, Ctrl(e.KeyModifiers));
            }
            else if (SelectMode && Tool == null)
            {
                SelectTool.Hover(_pointer.Value, _surfaceSize);
                // The object a click would pick (none over a move handle), looked for once the moves waiting are
                // taken: a fast mouse sends many a frame, and each look tries every object's box.
                QueueHoverPick();
            }
            else if (Mode == ToolMode.Dungeon && WorldRay(_pointer.Value, _surfaceSize) is { } ray)
            {
                Dungeon.Hover(ray.O, ray.D);
            }
            else if (Mode == ToolMode.Measure && WorldAt(_pointer.Value, _surfaceSize) is { } w)
            {
                Tape.Move(w);
            }
            Drag(_pointer.Value);
        };
        surface.PointerExited += (_, _) =>
        {
            _pointer = null;
            SetHoverObject(null);
            Wake();
        };
        surface.PointerWheelChanged += (_, e) =>
        {
            // Ctrl or Alt + wheel: one step per notch. Smooth-scrolling wheels and touchpads send parts of a
            // notch (and some systems send Shift's as sideways): added up until a whole one.
            bool ctrlW = Ctrl(e.KeyModifiers), altW = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
            // Ctrl: building lifts the piece; Alt: the tools that turn things turn them. Else the view zooms.
            bool wants = ctrlW ? (Mode == ToolMode.Place && Place?.Tool.Building == true || Mode == ToolMode.Paste) && CtrlWheel != null
                : altW && AltWheel != null && Mode is ToolMode.Place or ToolMode.Paste or ToolMode.Select;
            if (wants)
            {
                double delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
                bool shiftW = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                _wheelSum += delta;
                bool tried = false, used = false;
                while (Math.Abs(_wheelSum) >= 0.999)
                {
                    int dir = _wheelSum > 0 ? 1 : -1;
                    _wheelSum -= dir;
                    tried = true;
                    used |= ctrlW ? CtrlWheel!(dir, shiftW) : AltWheel!(dir > 0 ? Key.OemComma : Key.OemPeriod, shiftW);
                }
                // Nothing to turn (or lift): the wheel zooms as usual.
                if (!tried || used)
                {
                    e.Handled = true;
                    Wake();
                    return;
                }
                _wheelSum = 0;
            }
            else
            {
                _wheelSum = 0;
            }
            lock (_camLock)
            {
                if (_eye == EyeMode.Orbit)
                {
                    _distance = Math.Clamp(_distance * MathF.Pow(0.88f, (float)e.Delta.Y), 3, 3000);
                }
            }
            Wake();
        };
        // Space is the view's (held: Space + left drag slides, flying goes up), not the focused
        // button's, which it would press; typing keeps it.
        window.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Space && TakesKey(surface, e))
            {
                lock (_keys)
                {
                    _keys.Add(Key.Space);
                }
                e.Handled = true;
                Wake();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        window.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Space && TakesKey(surface, e))
            {
                lock (_keys)
                {
                    _keys.Remove(Key.Space);
                }
                e.Handled = true;
                Wake();
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        window.KeyDown += (_, e) =>
        {
            if (!TakesKey(surface, e))
            {
                return;
            }
            if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)
            {
                CycleEye();
                return;
            }
            lock (_keys)
            {
                _keys.Add(e.Key);
            }
            Wake();
        };
        window.KeyUp += (_, e) =>
        {
            lock (_keys)
            {
                _keys.Remove(e.Key);
            }
            Wake();
        };
        // Keys held when the window loses the focus would keep moving the camera; so would a key held while a box
        // takes the focus (its release goes to the box).
        window.Deactivated += (_, _) => { lock (_keys) { _keys.Clear(); } };
        // So would one held while the editor is hidden: its release, on the start page or the map, is not the view's.
        surface.DetachedFromVisualTree += (_, _) => { lock (_keys) { _keys.Clear(); } };
        // Handled ones too: a box (or a control around it) marking the focus event handled still took the focus.
        window.AddHandler(GotFocusEvent, (_, e) =>
        {
            if (e.Source is TextBox)
            {
                lock (_keys)
                {
                    _keys.Clear();
                }
            }
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>
    /// Whether a key pressed in the window is the view's: not while typing in a box (its keys are its own: an F
    /// there is a letter, not a change of eye), and only while the 3D editor is shown (the <paramref name="surface"/>
    /// is in the window), not on the start page or the map, where W would move a camera nobody sees.
    /// </summary>
    private static bool TakesKey(Control surface, KeyEventArgs e) => e.Source is not TextBox && surface.IsAttachedToVisualTree();

    /// <summary>Where the mouse button went down, to tell a click from a drag.</summary>
    private Point? _pressAt;
    /// <summary>The press's click count (2: a double click).</summary>
    private int _pressClicks;

    /// <summary>The drag slides the view (Space + left, Shift + right).</summary>
    internal bool Panning { get; private set; }
    /// <summary>
    /// Whether Ctrl is held for a pointer action (add to the selection, remove a point…), or Cmd, the Mac's Ctrl (where
    /// Ctrl + click is a right-click); as for the keyboard shortcuts.
    /// </summary>
    private static bool Ctrl(KeyModifiers m) => m.HasFlag(KeyModifiers.Control) || m.HasFlag(KeyModifiers.Meta);
}
