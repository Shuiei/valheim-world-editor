using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// A seed's world seen from above (the New world page), north up: the wheel zooms where the pointer is, dragging moves,
/// a double click shows all of it. The whole world is drawn from the preview; zoomed in, the part in view is drawn
/// again from the game's generator, one sample a screen pixel, so it stays sharp.
/// </summary>
public sealed partial class SeedMap : Control, IDisposable
{
    /// <summary>The seed's preview shown, or null.</summary>
    private SeedPreview.Preview? _preview;

    /// <summary>The world's radius, metres: the preview's (its world's rules'), the game's 10 km when none is shown.</summary>
    internal float Radius => _preview?.Radius ?? SeedPreview.Radius;
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
    private static readonly Pen DotLine = new(new SolidColorBrush(Color.FromRgb(20, 20, 24)), 1);

    /// <summary>The view moved or zoomed.</summary>
    public event Action? ViewChanged;
    /// <summary>The pointer is over this world point (x, z).</summary>
    public event Action<float, float>? Hovered;

    /// <summary>The background, outside the world.</summary>
    private static readonly IBrush Outside = new SolidColorBrush(Color.FromRgb(18, 22, 28));

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

    /// <summary>Set once disposed: a later resize or move samples and keeps no close look.</summary>
    private bool _disposed;

    /// <summary>Stops any close look being sampled and frees the pictures.</summary>
    public void Dispose()
    {
        _disposed = true;
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
}
