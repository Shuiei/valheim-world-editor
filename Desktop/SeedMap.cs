using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// A seed's world seen from above (the New world page), north up: the wheel zooms where the pointer is, dragging moves,
/// a double click shows all of it. The whole world is drawn from the preview; zoomed in, the part in view is drawn
/// again from the game's generator, one sample a screen pixel, so it stays sharp.
/// </summary>
public sealed class SeedMap : Control, IDisposable
{
    /// <summary>The seed's preview shown, or null.</summary>
    private SeedPreview.Preview? _preview;
    /// <summary>The whole world's picture, from the preview.</summary>
    private WriteableBitmap? _world;
    /// <summary>The close look: the part in view sampled again, with the world rectangle it covers.</summary>
    private (WriteableBitmap Picture, float X0, float Z0, float X1, float Z1)? _close;
    /// <summary>Cancels the close look being sampled.</summary>
    private CancellationTokenSource? _closeWork;
    /// <summary>Starts the close look 180 ms after the view last changed.</summary>
    private readonly DispatcherTimer _closeWait = new() { Interval = TimeSpan.FromMilliseconds(180) };
    /// <summary>The whole world is in view, and stays so when the map is resized.</summary>
    private bool _fit = true;
    /// <summary>Where the drag last was, while dragging.</summary>
    private Point? _drag;

    /// <summary>Whether a close look is drawn over the world (tests).</summary>
    internal bool HasCloseLook => _close != null;

    /// <summary>The world x at the middle of the view.</summary>
    public float CenterX { get; private set; }
    /// <summary>The world z at the middle of the view.</summary>
    public float CenterZ { get; private set; }
    /// <summary>Metres a screen pixel: the zoom.</summary>
    public float MetersPerPixel { get; private set; } = 40;

    /// <summary>Small squares drawn over the map (the dungeons: many, so drawn here rather than as controls).</summary>
    public IReadOnlyList<(float X, float Z, IBrush Fill)> Dots
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = [];

    /// <summary>The dots' dark outline.</summary>
    private static readonly Pen DotLine = new(new SolidColorBrush(Avalonia.Media.Color.FromRgb(20, 20, 24)), 1);

    /// <summary>The view moved or zoomed.</summary>
    public event Action? ViewChanged;
    /// <summary>The pointer is over this world point (x, z).</summary>
    public event Action<float, float>? Hovered;

    /// <summary>The background, outside the world.</summary>
    private static readonly IBrush Outside = new SolidColorBrush(Avalonia.Media.Color.FromRgb(18, 22, 28));

    /// <summary>An empty map; Show gives it a preview.</summary>
    public SeedMap()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.MediumQuality);
        _closeWait.Tick += (_, _) =>
        {
            _closeWait.Stop();
            DrawClose();
        };
    }

    /// <summary>Stops any close look being sampled and frees the pictures.</summary>
    public void Dispose()
    {
        _closeWait.Stop();
        _closeWork?.Cancel();
        _closeWork?.Dispose();
        _closeWork = null;
        Replace(ref _world, null);
        DropClose();
    }

    /// <summary>
    /// Pictures freed as they are replaced (their pixels are not the garbage collector's to count: a close look the
    /// size of the screen is tens of MB).
    /// </summary>
    private static void Replace(ref WriteableBitmap? picture, WriteableBitmap? by)
    {
        picture?.Dispose();
        picture = by;
    }

    /// <summary>Frees the close look.</summary>
    private void DropClose()
    {
        _close?.Picture.Dispose();
        _close = null;
    }

    /// <summary>Shows a new preview, with all of the world in view.</summary>
    /// <param name="p">The preview.</param>
    /// <param name="keepView">Stay where the view is (the same seed again).</param>
    public void Show(SeedPreview.Preview p, bool keepView = false)
    {
        _preview = p;
        Replace(ref _world, Picture(p.Biome, p.Height, p.Size, p.Size));
        DropClose();
        if (!keepView)
        {
            Fit();
        }
        Changed();
    }

    /// <summary>Shows nothing (the background only).</summary>
    public void Clear()
    {
        _preview = null;
        Replace(ref _world, null);
        DropClose();
        InvalidateVisual();
    }

    /// <summary>All of the world in view.</summary>
    public void Fit()
    {
        _fit = true;
        CenterX = CenterZ = 0;
        double side = Math.Min(Bounds.Width, Bounds.Height);
        MetersPerPixel = side > 0 ? (float)(2 * SeedPreview.Radius / side) : 40;
        Changed();
    }

    /// <summary>A closer look at a world point.</summary>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="metersPerPixel">Metres a pixel: how close.</param>
    public void LookAt(float x, float z, float metersPerPixel = 4)
    {
        _fit = false;
        CenterX = x;
        CenterZ = z;
        MetersPerPixel = Math.Clamp(metersPerPixel, MinMpp, MaxMpp);
        Changed();
    }

    /// <summary>Closer (factor &lt; 1) or farther around a screen point (the middle when null).</summary>
    public void Zoom(double factor, Point? at = null)
    {
        var p = at ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
        var (bx, bz) = WorldAt(p);
        MetersPerPixel = Math.Clamp((float)(MetersPerPixel * factor), MinMpp, MaxMpp);
        var (ax, az) = WorldAt(p);
        CenterX += bx - ax;
        CenterZ += bz - az;
        _fit = false;
        Changed();
    }

    /// <summary>The closest zoom, in metres a pixel.</summary>
    private const float MinMpp = 0.5f;

    /// <summary>The farthest zoom: the world a little smaller than the view.</summary>
    private float MaxMpp => (float)(2.5 * SeedPreview.Radius / Math.Max(1, Math.Min(Bounds.Width, Bounds.Height)));

    /// <summary>Where a world point (x, z) is on the map, in pixels.</summary>
    public Point ScreenOf(float x, float z) => new(Bounds.Width / 2 + (x - CenterX) / MetersPerPixel, Bounds.Height / 2 - (z - CenterZ) / MetersPerPixel);

    /// <summary>The world point under a point of the map.</summary>
    public (float X, float Z) WorldAt(Point p) => (CenterX + (float)(p.X - Bounds.Width / 2) * MetersPerPixel, CenterZ - (float)(p.Y - Bounds.Height / 2) * MetersPerPixel);

    /// <summary>The view changed: keeps the world in view, redraws, tells, and waits to sample a close look.</summary>
    private void Changed()
    {
        // The world stays in view.
        CenterX = Math.Clamp(CenterX, -SeedPreview.Radius, SeedPreview.Radius);
        CenterZ = Math.Clamp(CenterZ, -SeedPreview.Radius, SeedPreview.Radius);
        InvalidateVisual();
        ViewChanged?.Invoke();
        _closeWait.Stop();
        _closeWait.Start();
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_fit)
        {
            Fit();
        }
        else
        {
            Changed();
        }
    }

    /// <summary>Draws the world, the close look over it and the dots.</summary>
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Outside, new Rect(Bounds.Size));
        if (_world == null || _preview == null)
        {
            return;
        }
        Draw(context, _world, -SeedPreview.Radius, -SeedPreview.Radius, SeedPreview.Radius, SeedPreview.Radius);
        if (_close is { } c)
        {
            Draw(context, c.Picture, c.X0, c.Z0, c.X1, c.Z1);
        }
        foreach (var (x, z, fill) in Dots)
        {
            var at = ScreenOf(x, z);
            if (at.X >= -4 && at.Y >= -4 && at.X <= Bounds.Width + 4 && at.Y <= Bounds.Height + 4)
            {
                context.DrawRectangle(fill, DotLine, new Rect(at.X - 3, at.Y - 3, 6, 6));
            }
        }
    }

    /// <summary>Draws a picture over the world rectangle it covers.</summary>
    private void Draw(DrawingContext context, WriteableBitmap picture, float x0, float z0, float x1, float z1)
    {
        var topLeft = ScreenOf(x0, z1);
        var bottomRight = ScreenOf(x1, z0);
        context.DrawImage(picture, new Rect(0, 0, picture.PixelSize.Width, picture.PixelSize.Height), new Rect(topLeft, bottomRight));
    }

    /// <summary>
    /// Zoomed in past the preview's cells: the part in view sampled again from the generator in the background, a cell
    /// a screen pixel. Zoomed out, the close look is dropped.
    /// </summary>
    private void DrawClose()
    {
        _closeWork?.Cancel();
        _closeWork?.Dispose();
        _closeWork = null;
        if (_preview?.Generator is not { } gen || MetersPerPixel >= _preview.Cell * 0.8f || Bounds.Width < 2 || Bounds.Height < 2)
        {
            if (_close != null)
            {
                DropClose();
                InvalidateVisual();
            }
            return;
        }
        var (vx0, vz1) = WorldAt(new Point(0, 0));
        var (vx1, vz0) = WorldAt(new Point(Bounds.Width, Bounds.Height));
        float r = SeedPreview.Radius, cell = MetersPerPixel;
        float x0 = Math.Max(vx0, -r), z0 = Math.Max(vz0, -r), x1 = Math.Min(vx1, r), z1 = Math.Min(vz1, r);
        // The middle is kept in the world (Changed) and the view is 2 pixels or more: at least a cell each way.
        int w = (int)Math.Ceiling((x1 - x0) / cell), h = (int)Math.Ceiling((z1 - z0) / cell);
        var preview = _preview;
        var work = _closeWork = new CancellationTokenSource();
        Task.Run(() => SeedPreview.Sample(gen, x0, z0, cell, w, h, parallel: true, work.Token), work.Token).ContinueWith(t =>
        {
            if (work.IsCancellationRequested || t.Status != TaskStatus.RanToCompletion || preview != _preview)
            {
                return;
            }
            DropClose();
            _close = (Picture(t.Result.Biome, t.Result.Height, w, h), x0, z0, x0 + w * cell, z0 + h * cell);
            InvalidateVisual();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>The wheel zooms where the pointer is.</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoom(Math.Pow(1.25, -e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>A double click shows all of the world; a left press starts a drag.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount == 2)
        {
            Fit();
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _drag = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    /// <summary>Drags the view, and tells where the pointer is.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_drag is { } from)
        {
            CenterX -= (float)(p.X - from.X) * MetersPerPixel;
            CenterZ += (float)(p.Y - from.Y) * MetersPerPixel;
            _drag = p;
            _fit = false;
            Changed();
        }
        var (x, z) = WorldAt(p);
        Hovered?.Invoke(x, z);
    }

    /// <summary>Ends a drag.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drag = null;
        e.Pointer.Capture(null);
    }

    /// <summary>
    /// Cells as a picture (row 0 is the south: the picture's bottom row): the game map's biome colours, shaded by
    /// height, water on land tinted, the sea shaded by depth.
    /// </summary>
    /// <param name="biome">Each cell's biome (an index into the preview's biomes).</param>
    /// <param name="height">Each cell's height (negative infinity with the ocean biome: outside the world).</param>
    /// <param name="w">Cells across.</param>
    /// <param name="h">Cells deep.</param>
    public static WriteableBitmap Picture(byte[] biome, float[] height, int w, int h)
    {
        var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        byte[] row = new byte[w * 4];
        for (int y = 0; y < h; y++)
        {
            int j = h - 1 - y;
            for (int i = 0; i < w; i++)
            {
                var (r, g, b) = Shade(biome[j * w + i], height[j * w + i]);
                row[i * 4] = b;
                row[i * 4 + 1] = g;
                row[i * 4 + 2] = r;
                row[i * 4 + 3] = 255;
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
        }
        return bmp;
    }

    /// <summary>One cell's colour (outside the world: the background).</summary>
    public static (byte R, byte G, byte B) Shade(byte biome, float height)
    {
        if (biome == SeedPreview.Ocean)
        {
            if (float.IsNegativeInfinity(height))
            {
                return (18, 22, 28);
            }
            float d = Math.Clamp((TerrainService.WaterLevel - height) / 60f, 0, 1);
            return ((byte)(48 - 28 * d), (byte)(98 - 50 * d), (byte)(150 - 50 * d));
        }
        var (br, bg, bb) = MapData.PixelColor(SeedPreview.Biomes[biome]);
        if (height < TerrainService.WaterLevel)
        {
            // Water on land (swamp pools, rivers, lakes): the biome under a blue tint.
            return ((byte)((br + 2 * 48) / 3), (byte)((bg + 2 * 98) / 3), (byte)((bb + 2 * 150) / 3));
        }
        float lift = Math.Clamp((height - TerrainService.WaterLevel) / 120f, 0, 1) * 0.35f;
        return ((byte)(br + (255 - br) * lift), (byte)(bg + (255 - bg) * lift), (byte)(bb + (255 - bb) * lift));
    }
}
