using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The master map being drawn (the Draw a world page), north up: the left button paints with the tool chosen (a brush
/// of the kind chosen, a fill, or the start point), the right or middle button drags the view, the wheel zooms where
/// the pointer is, a double click with the right button shows all of it. The world's edge (its 10 km circle) and the
/// start are drawn over the cells.
/// </summary>
public sealed class MasterMapCanvas : Control, IDisposable
{
    /// <summary>What the left button does.</summary>
    public enum Tool
    {
        /// <summary>Paints discs of the kind chosen along the drag.</summary>
        Brush,
        /// <summary>Fills the connected cells of the kind clicked with the kind chosen.</summary>
        Fill,
        /// <summary>Puts the start point where clicked.</summary>
        Start,
    }

    /// <summary>The map drawn on.</summary>
    public MasterMap Map { get; private set; } = new();

    /// <summary>What the left button does.</summary>
    public Tool Using { get; set; } = Tool.Brush;

    /// <summary>The kind the brush and the fill paint.</summary>
    public MasterMap.Kind Paint { get; set; } = MasterMap.Kind.Meadows;

    /// <summary>The brush's radius, metres.</summary>
    public float BrushMetres
    {
        get;
        set
        {
            field = Math.Clamp(value, MasterMap.CellMetres / 2, 3000f);
            InvalidateVisual();
        }
    } = 400f;

    /// <summary>The world x at the middle of the view.</summary>
    public float CenterX { get; private set; }

    /// <summary>The world z at the middle of the view.</summary>
    public float CenterZ { get; private set; }

    /// <summary>Metres a screen pixel: the zoom.</summary>
    public float MetersPerPixel { get; private set; } = 40;

    /// <summary>A change is about to be made (a stroke, a fill, the start moved): the page keeps the map as it was, to undo.</summary>
    public event Action? Changing;

    /// <summary>A stroke, a fill or the start's move is done.</summary>
    public event Action? Changed;

    /// <summary>The pointer is over this world point (x, z).</summary>
    public event Action<float, float>? Hovered;

    /// <summary>The cells as a picture, one pixel a cell.</summary>
    private readonly WriteableBitmap _picture = new(new PixelSize(MasterMap.Size, MasterMap.Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

    /// <summary>The legend's colours, as the picture's pixels (blue, green, red, alpha).</summary>
    private static readonly int[] Pixels = [.. MasterMap.Legend.Select(e => unchecked((int)(0xFF000000u | ((uint)e.R << 16) | ((uint)e.G << 8) | e.B)))];

    /// <summary>All of the map is in view, and stays so when the canvas is resized.</summary>
    private bool _fit = true;

    /// <summary>Where the view's drag last was, while dragging it.</summary>
    private Point? _drag;

    /// <summary>The last cell painted in the stroke under way, or null when none is.</summary>
    private (int Column, int Row)? _stroke;

    /// <summary>The pointer's place on the canvas (for the brush's outline), or null when it is not over it.</summary>
    private Point? _pointer;

    private static readonly IBrush Outside = new SolidColorBrush(Color.FromRgb(18, 22, 28));
    private static readonly Pen Edge = new(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1, new DashStyle([6, 4], 0));
    private static readonly Pen BrushLine = new(Brushes.White, 1.2);
    private static readonly Pen BrushShadow = new(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 3);
    private static readonly IBrush StartFill = new SolidColorBrush(Color.FromRgb(MasterMap.StartColour.R, MasterMap.StartColour.G, MasterMap.StartColour.B));
    private static readonly Pen StartRing = new(Brushes.White, 2);
    private static readonly Pen MiddleRing = new(new SolidColorBrush(Color.FromArgb(200, 255, 0, 255)), 2, new DashStyle([3, 3], 0));

    /// <summary>An empty sea to draw on.</summary>
    public MasterMapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        Show(Map);
    }

    /// <summary>Frees the picture.</summary>
    public void Dispose() => _picture.Dispose();

    /// <summary>Draws on another map (opened, made new, or an undo).</summary>
    public void Show(MasterMap map)
    {
        Map = map;
        Repaint(0, 0, MasterMap.Size, MasterMap.Size);
    }

    /// <summary>Copies a rectangle of cells into the picture and redraws.</summary>
    private void Repaint(int x, int y, int w, int h)
    {
        using (var fb = _picture.Lock())
        {
            int[] row = new int[w];
            for (int r = y; r < y + h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    int i = r * MasterMap.Size + x + c;
                    row[c] = Map.Heights is { } heights ? Shaded(Pixels[Map.Cells[i]], heights, i) : Pixels[Map.Cells[i]];
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + r * fb.RowBytes + x * 4, w);
            }
        }
        InvalidateVisual();
    }

    /// <summary>
    /// A cell's colour with the relief of the map's heights: lighter higher up, and lit from the north-west (brighter
    /// on slopes facing it), so the land's shape shows under the kinds' colours.
    /// </summary>
    private static int Shaded(int pixel, ushort[] heights, int i)
    {
        double h = DrawnWorlds.DrawnLand.ContinentOf(heights[i]);
        int back = i - MasterMap.Size - 1;
        double nw = back >= 0 ? DrawnWorlds.DrawnLand.ContinentOf(heights[back]) : h;
        double f = Math.Clamp(0.85 + (h - 0.15) * 0.4 + (h - nw) * 40.0, 0.5, 1.35);
        int r = Math.Min(255, (int)(((pixel >> 16) & 0xFF) * f)), g = Math.Min(255, (int)(((pixel >> 8) & 0xFF) * f)), b = Math.Min(255, (int)((pixel & 0xFF) * f));
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }

    // ---- The view.

    /// <summary>All of the map in view.</summary>
    public void Fit()
    {
        _fit = true;
        CenterX = CenterZ = 0;
        double side = Math.Min(Bounds.Width, Bounds.Height);
        MetersPerPixel = side > 0 ? (float)(2 * MasterMap.Half / side) : 40;
        InvalidateVisual();
    }

    /// <summary>Closer (factor &lt; 1) or farther around a screen point (the middle when null).</summary>
    public void Zoom(double factor, Point? at = null)
    {
        var p = at ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var (bx, bz) = WorldAt(p);
        float most = (float)(2.5 * MasterMap.Half / Math.Max(1, Math.Min(Bounds.Width, Bounds.Height)));
        MetersPerPixel = Math.Clamp((float)(MetersPerPixel * factor), 0.5f, most);
        var (ax, az) = WorldAt(p);
        CenterX = Math.Clamp(CenterX + bx - ax, -MasterMap.Half, MasterMap.Half);
        CenterZ = Math.Clamp(CenterZ + bz - az, -MasterMap.Half, MasterMap.Half);
        _fit = false;
        InvalidateVisual();
    }

    /// <summary>Where a world point (x, z) is on the canvas, in pixels.</summary>
    public Point ScreenOf(float x, float z) => new(Bounds.Width / 2 + (x - CenterX) / MetersPerPixel, Bounds.Height / 2 - (z - CenterZ) / MetersPerPixel);

    /// <summary>The world point under a point of the canvas.</summary>
    public (float X, float Z) WorldAt(Point p) => (CenterX + (float)(p.X - Bounds.Width / 2) * MetersPerPixel, CenterZ - (float)(p.Y - Bounds.Height / 2) * MetersPerPixel);

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_fit)
        {
            Fit();
        }
    }

    // ---- Drawing.

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Outside, new Rect(Bounds.Size));
        var topLeft = ScreenOf(-MasterMap.Half, MasterMap.Half);
        var bottomRight = ScreenOf(MasterMap.Half, -MasterMap.Half);
        // Cells stay sharp squares close up, and are averaged far out.
        var mode = MetersPerPixel < MasterMap.CellMetres ? BitmapInterpolationMode.None : BitmapInterpolationMode.MediumQuality;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = mode }))
        {
            context.DrawImage(_picture, new Rect(0, 0, MasterMap.Size, MasterMap.Size), new Rect(topLeft, bottomRight));
        }
        // The world's edge: past it the game sinks everything into the sea.
        var middle = ScreenOf(0, 0);
        double radius = MasterMap.Half / MetersPerPixel;
        context.DrawEllipse(null, Edge, middle, radius, radius);
        // The start: where drawn, or the middle when none is (dashed).
        if (Map.Start is { } s)
        {
            var (x, z) = MasterMap.CenterOf(s.Column, s.Row);
            context.DrawEllipse(StartFill, StartRing, ScreenOf(x, z), 6, 6);
        }
        else
        {
            context.DrawEllipse(null, MiddleRing, middle, 6, 6);
        }
        if (_pointer is { } p && Using == Tool.Brush)
        {
            double r = Math.Max(2, BrushMetres / MetersPerPixel);
            context.DrawEllipse(null, BrushShadow, p, r, r);
            context.DrawEllipse(null, BrushLine, p, r, r);
        }
    }

    // ---- The pointer.

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoom(Math.Pow(1.25, -e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        var at = e.GetPosition(this);
        Focus();
        if (point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                Fit();
                return;
            }
            _drag = at;
            e.Pointer.Capture(this);
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        var (x, z) = WorldAt(at);
        var cell = MasterMap.CellOf(x, z);
        switch (Using)
        {
            case Tool.Brush:
                Changing?.Invoke();
                _stroke = cell;
                PaintDisc(cell);
                e.Pointer.Capture(this);
                break;
            case Tool.Fill:
                if (Map.At(cell.Column, cell.Row) != Paint)
                {
                    Changing?.Invoke();
                    if (Map.Fill(cell.Column, cell.Row, Paint) is { } box)
                    {
                        Repaint(box.X, box.Y, box.W, box.H);
                    }
                    Changed?.Invoke();
                }
                break;
            case Tool.Start:
                Changing?.Invoke();
                Map.Start = cell;
                InvalidateVisual();
                Changed?.Invoke();
                break;
            default:
                throw new InvalidOperationException($"No such tool: {Using}.");
        }
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = e.GetPosition(this);
        _pointer = at;
        if (_drag is { } from)
        {
            CenterX = Math.Clamp(CenterX - (float)(at.X - from.X) * MetersPerPixel, -MasterMap.Half, MasterMap.Half);
            CenterZ = Math.Clamp(CenterZ + (float)(at.Y - from.Y) * MetersPerPixel, -MasterMap.Half, MasterMap.Half);
            _drag = at;
            _fit = false;
        }
        var (x, z) = WorldAt(at);
        if (_stroke is { } last)
        {
            // Discs along the way, close enough to make a line however fast the pointer moves.
            var cell = MasterMap.CellOf(x, z);
            int dx = cell.Column - last.Column, dy = cell.Row - last.Row;
            float step = Math.Max(1f, BrushMetres / MasterMap.CellMetres / 2);
            int steps = (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / step);
            for (int i = 1; i <= steps; i++)
            {
                PaintDisc((last.Column + dx * i / steps, last.Row + dy * i / steps));
            }
            _stroke = cell;
        }
        Hovered?.Invoke(x, z);
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drag = null;
        if (_stroke != null)
        {
            _stroke = null;
            Changed?.Invoke();
        }
        e.Pointer.Capture(null);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        InvalidateVisual();
    }

    /// <summary>Paints one disc of the brush at a cell.</summary>
    private void PaintDisc((int Column, int Row) cell)
    {
        if (Map.Disc(cell.Column, cell.Row, BrushMetres / MasterMap.CellMetres, Paint) is { } box)
        {
            Repaint(box.X, box.Y, box.W, box.H);
        }
    }
}
