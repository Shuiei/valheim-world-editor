using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Drawing: the world, the close look over it, and the dots.
public sealed partial class SeedMap : Control, IDisposable
{
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
}
