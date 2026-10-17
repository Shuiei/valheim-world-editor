using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

// Colours: the cells' pictures from the biome and the height.
public sealed partial class SeedMap : Control, IDisposable
{
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
