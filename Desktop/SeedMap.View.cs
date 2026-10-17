using Avalonia;
using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The view: where the middle is, the zoom, keeping the world in view, and the size.
public sealed partial class SeedMap : Control, IDisposable
{
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
        if (!_disposed)
        {
            _closeWait.Start();
        }
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
}
