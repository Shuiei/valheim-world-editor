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

// A seed's world seen from above (the New world page), north up: the wheel zooms where the pointer is,
// dragging moves, a double click shows all of it. The whole world is drawn from the preview; zoomed in,
// the part in view is drawn again from the game's generator, one sample a screen pixel, so it stays
// sharp.
public sealed class SeedMap : Control, IDisposable
{
	private SeedPreview.Preview? _preview;
	private WriteableBitmap? _world;
	private (WriteableBitmap Picture, float X0, float Z0, float X1, float Z1)? _close;
	private CancellationTokenSource? _closeWork;
	private readonly DispatcherTimer _closeWait = new() { Interval = TimeSpan.FromMilliseconds(180) };
	private bool _fit = true;
	private Point? _drag;

	// The view: the world point at the middle, metres a screen pixel.
	public float CenterX { get; private set; }
	public float CenterZ { get; private set; }
	public float MetersPerPixel { get; private set; } = 40;

	public event Action? ViewChanged;
	public event Action<float, float>? Hovered;

	private static readonly IBrush Outside = new SolidColorBrush(Avalonia.Media.Color.FromRgb(18, 22, 28));

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

	public void Dispose()
	{
		_closeWait.Stop();
		_closeWork?.Cancel();
		_closeWork?.Dispose();
		_closeWork = null;
	}

	// A new preview; keepView: stay where the view is (the same seed again).
	public void Show(SeedPreview.Preview p, bool keepView = false)
	{
		_preview = p;
		_world = Picture(p.Biome, p.Height, p.Size, p.Size);
		_close = null;
		if (!keepView)
		{
			Fit();
		}
		Changed();
	}

	public void Clear()
	{
		_preview = null;
		_world = null;
		_close = null;
		InvalidateVisual();
	}

	// All of the world in view.
	public void Fit()
	{
		_fit = true;
		CenterX = CenterZ = 0;
		double side = Math.Min(Bounds.Width, Bounds.Height);
		MetersPerPixel = side > 0 ? (float)(2 * SeedPreview.Radius / side) : 40;
		Changed();
	}

	// A closer look at a point (metres a pixel: how close).
	public void LookAt(float x, float z, float metersPerPixel = 4)
	{
		_fit = false;
		CenterX = x;
		CenterZ = z;
		MetersPerPixel = Math.Clamp(metersPerPixel, MinMpp, MaxMpp);
		Changed();
	}

	// Closer (factor < 1) or farther around a screen point (the middle when null).
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

	private const float MinMpp = 0.5f;

	private float MaxMpp => (float)(2.5 * SeedPreview.Radius / Math.Max(1, Math.Min(Bounds.Width, Bounds.Height)));

	public Point ScreenOf(float x, float z) => new(Bounds.Width / 2 + (x - CenterX) / MetersPerPixel, Bounds.Height / 2 - (z - CenterZ) / MetersPerPixel);

	public (float X, float Z) WorldAt(Point p) => (CenterX + (float)(p.X - Bounds.Width / 2) * MetersPerPixel, CenterZ - (float)(p.Y - Bounds.Height / 2) * MetersPerPixel);

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
	}

	private void Draw(DrawingContext context, WriteableBitmap picture, float x0, float z0, float x1, float z1)
	{
		var topLeft = ScreenOf(x0, z1);
		var bottomRight = ScreenOf(x1, z0);
		context.DrawImage(picture, new Rect(0, 0, picture.PixelSize.Width, picture.PixelSize.Height), new Rect(topLeft, bottomRight));
	}

	// Zoomed in past the preview's cells: the part in view sampled again, a cell a screen pixel.
	private void DrawClose()
	{
		_closeWork?.Cancel();
		_closeWork?.Dispose();
		_closeWork = null;
		if (_preview?.Generator is not { } gen || MetersPerPixel >= _preview.Cell * 0.8f || Bounds.Width < 2 || Bounds.Height < 2)
		{
			if (_close != null)
			{
				_close = null;
				InvalidateVisual();
			}
			return;
		}
		var (vx0, vz1) = WorldAt(new Point(0, 0));
		var (vx1, vz0) = WorldAt(new Point(Bounds.Width, Bounds.Height));
		float r = SeedPreview.Radius, cell = MetersPerPixel;
		float x0 = Math.Max(vx0, -r), z0 = Math.Max(vz0, -r), x1 = Math.Min(vx1, r), z1 = Math.Min(vz1, r);
		int w = (int)Math.Ceiling((x1 - x0) / cell), h = (int)Math.Ceiling((z1 - z0) / cell);
		if (w < 1 || h < 1)
		{
			return;
		}
		var preview = _preview;
		var work = _closeWork = new CancellationTokenSource();
		Task.Run(() => SeedPreview.Sample(gen, x0, z0, cell, w, h, parallel: true, work.Token), work.Token).ContinueWith(t =>
		{
			if (work.IsCancellationRequested || t.Status != TaskStatus.RanToCompletion || preview != _preview)
			{
				return;
			}
			_close = (Picture(t.Result.Biome, t.Result.Height, w, h), x0, z0, x0 + w * cell, z0 + h * cell);
			InvalidateVisual();
		}, TaskScheduler.FromCurrentSynchronizationContext());
	}

	protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
	{
		base.OnPointerWheelChanged(e);
		Zoom(Math.Pow(1.25, -e.Delta.Y), e.GetPosition(this));
		e.Handled = true;
	}

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

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);
		_drag = null;
		e.Pointer.Capture(null);
	}

	// Cells as a picture (row 0 the south, so drawn last): the game map's biome colours, shaded by
	// height, water on land tinted, the sea shaded by depth.
	public static WriteableBitmap Picture(byte[] biome, float[] height, int w, int h)
	{
		var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
		using var fb = bmp.Lock();
		var row = new byte[w * 4];
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

	// One cell's colour (outside the world: the background).
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
