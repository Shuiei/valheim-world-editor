using Avalonia;
using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The close look: the part in view sampled again from the generator in the background.
public sealed partial class SeedMap : Control, IDisposable
{
    /// <summary>
    /// Zoomed in past the preview's cells: the part in view sampled again from the generator in the background, a cell
    /// a screen pixel. Zoomed out, the close look is dropped.
    /// </summary>
    private void DrawClose()
    {
        _closeWork?.Cancel();
        _closeWork?.Dispose();
        _closeWork = null;
        if (_disposed)
        {
            return;
        }
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
            if (_disposed || work.IsCancellationRequested || t.Status != TaskStatus.RanToCompletion || preview != _preview)
            {
                return;
            }
            DropClose();
            _close = (Picture(t.Result.Biome, t.Result.Height, w, h), x0, z0, x0 + w * cell, z0 + h * cell);
            InvalidateVisual();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
