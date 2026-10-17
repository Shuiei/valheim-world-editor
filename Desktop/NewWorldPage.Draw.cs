using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// A seed's preview drawn as a small picture for a search result.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The preview as a small picture of px pixels (a search's results): the map's colours (SeedMap.Shade), the start
    /// in red, north up.
    /// </summary>
    /// <param name="p">The preview.</param>
    /// <param name="px">The picture's side, in pixels.</param>
    /// <param name="startDot">Mark the start with a red square.</param>
    internal static WriteableBitmap Draw(SeedPreview.Preview p, int px, bool startDot = true)
    {
        var bmp = new WriteableBitmap(new PixelSize(px, px), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        byte[] row = new byte[px * 4];
        int startI = (int)((p.Stats.Start.X + SeedPreview.Radius) / p.Cell), startJ = (int)((p.Stats.Start.Z + SeedPreview.Radius) / p.Cell);
        for (int y = 0; y < px; y++)
        {
            int j = p.Size - 1 - y * p.Size / px;
            for (int x = 0; x < px; x++)
            {
                int i = x * p.Size / px, k = j * p.Size + i;
                var (r, g, b) = SeedMap.Shade(p.Biome[k], p.Height[k]);
                if (startDot && Math.Abs(i - startI) <= Math.Max(1, p.Size / 100) && Math.Abs(j - startJ) <= Math.Max(1, p.Size / 100))
                {
                    (r, g, b) = (230, 40, 40);
                }
                row[x * 4] = b;
                row[x * 4 + 1] = g;
                row[x * 4 + 2] = r;
                row[x * 4 + 3] = 255;
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
        }
        return bmp;
    }
}
