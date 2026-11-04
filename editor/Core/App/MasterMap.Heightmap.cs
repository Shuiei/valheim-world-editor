using DrawnWorlds;
using TerrainEditor.Save;
using ValheimGen;

namespace TerrainEditor.App;

public sealed partial class MasterMap
{
    /// <summary>
    /// A master map made from a heightmap (a grayscale picture of the whole world, black lowest, white highest, north
    /// up, square, any size): the heights kept (<see cref="Heights"/>), and each cell given the kind the game would give
    /// it at that height: under the sea level, the sea (the deep sea far under it); above, the game's own biome layout
    /// for the seed with that land (the mountains where it is high, the swamps, plains, mists and forests by the seed's
    /// noise and their distances from the middle, the Ashlands and the Deep North past their edges). No start, no
    /// rivers or lakes: the game adds its own on that land.
    /// </summary>
    /// <param name="png">The heightmap's PNG file (gray or colour, 8 or 16 bits).</param>
    /// <param name="seaLevel">The gray (0 black to 1 white) at the sea's surface.</param>
    /// <param name="seed">The seed whose biome layout the land gets.</param>
    /// <exception cref="InvalidDataException">Not a PNG the reader takes, or not square.</exception>
    public static MasterMap FromHeightmap(byte[] png, float seaLevel, string seed)
    {
        var img = Png.Read(png);
        if (img.Width != img.Height)
        {
            throw new InvalidDataException(Lang.T("masterMap.error.notSquare", img.Width, img.Height));
        }
        seaLevel = Math.Clamp(seaLevel, 0.001f, 0.999f);
        var game = SeedPreview.Generator(seed);
        byte[] cells = new byte[Size * Size];
        ushort[] heights = new ushort[Size * Size];
        Parallel.For(0, Size, row =>
        {
            for (int col = 0; col < Size; col++)
            {
                double land = Continent(Sample(img, col, row), seaLevel);
                int i = row * Size + col;
                heights[i] = DrawnLand.HeightOf(land);
                if (land < 0.15)
                {
                    cells[i] = (byte)(land > -0.05 ? Kind.Sea : Kind.DeepSea);
                    continue;
                }
                var (x, z) = CenterOf(col, row);
                cells[i] = (byte)KindOf(game.BiomeWithLand(x, z, (float)land));
            }
        });
        return new MasterMap(cells, null) { Heights = heights };
    }

    /// <summary>
    /// The game's continents for a heightmap's gray: under the sea level from -0.3 (the deep sea floor) up to 0.13 (just
    /// under the waterline), above it from 0.17 (just over it) up to 0.9 (high mountains).
    /// </summary>
    internal static double Continent(float gray, float seaLevel) =>
        gray <= seaLevel ? -0.3 + (0.13 - -0.3) * (gray / seaLevel) : 0.17 + (0.9 - 0.17) * ((gray - seaLevel) / (1.0 - seaLevel));

    /// <summary>The kind of a game's biome (the sea for one the legend lacks).</summary>
    private static Kind KindOf(Heightmap.Biome biome) => Legend.FirstOrDefault(e => e.Biome == biome && !IsWater(e.Kind))?.Kind ?? Kind.Sea;

    /// <summary>The heightmap's gray at a cell's middle, between the four nearest pixels.</summary>
    private static float Sample(Png.Image img, int col, int row)
    {
        int n = img.Width;
        float fx = Math.Clamp((col + 0.5f) * n / Size - 0.5f, 0, n - 1), fy = Math.Clamp((row + 0.5f) * n / Size - 0.5f, 0, n - 1);
        int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(n - 1, x0 + 1), y1 = Math.Min(n - 1, y0 + 1);
        float tx = fx - x0, ty = fy - y0;
        float a = img.Values[y0 * n + x0], b = img.Values[y0 * n + x1], c = img.Values[y1 * n + x0], d = img.Values[y1 * n + x1];
        return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    /// <summary>The heights as a 16-bit grayscale PNG (each pixel the kept height), to save beside the master map.</summary>
    /// <exception cref="InvalidOperationException">The map has no heights.</exception>
    public byte[] HeightsPng() => Png.WriteGray16(Size, Size, Heights ?? throw new InvalidOperationException("The map has no heights."),
        new Dictionary<string, string> { [HeightsKey] = "1" });

    /// <summary>The PNG note that marks a picture as a master map's kept heights (read back exactly, not as a heightmap).</summary>
    public const string HeightsKey = "vwe-master-heights";

    /// <summary>
    /// The heights a picture saved by <see cref="HeightsPng"/> keeps, or null when it is not one (another picture, or
    /// another size).
    /// </summary>
    public static ushort[]? ReadHeights(byte[] png)
    {
        var img = Png.Read(png);
        if (!img.Text.ContainsKey(HeightsKey) || img.Width != Size || img.Height != Size)
        {
            return null;
        }
        // 16 bits read as brightness 0..1: back to the 16-bit value exactly.
        return [.. img.Values.Select(v => (ushort)Math.Round(v * 65535f))];
    }
}
