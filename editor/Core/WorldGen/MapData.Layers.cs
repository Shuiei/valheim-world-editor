
namespace ValheimGen;

public sealed partial class MapData
{
    /// <summary>
    /// The map textures of one window, row by row (row is world z, column world x).
    /// </summary>
    /// <param name="width">Texels per row.</param>
    /// <param name="height">Rows.</param>
    public sealed class Layers(int width, int height)
    {
        /// <summary>
        /// Texels per row.
        /// </summary>
        public int Width { get; } = width;

        /// <summary>
        /// Number of rows.
        /// </summary>
        public int Height { get; } = height;

        /// <summary>
        /// Ground height per texel, metres.
        /// </summary>
        public Half[] Heights { get; } = new Half[width * height];

        /// <summary>
        /// Biome colour per texel (RGBA8, a always 255).
        /// </summary>
        public byte[] Biome { get; } = new byte[width * height * 4];

        /// <summary>
        /// Mask per texel (RGBA8: r forest, g mist, b ashlands lava/ocean, a always 255).
        /// </summary>
        public byte[] Mask { get; } = new byte[width * height * 4];

        /// <summary>
        /// Painted ground (r dirt, g cultivated, b paved, a = 255 where painted).
        /// </summary>
        public byte[] Paint { get; } = new byte[width * height * 4];

        /// <summary>
        /// Fills texel <paramref name="i"/>.
        /// </summary>
        /// <param name="i">The texel's index (row * width + column).</param>
        /// <param name="h">Ground height, metres.</param>
        /// <param name="biome">Biome colour.</param>
        /// <param name="mask">Mask colour.</param>
        /// <param name="paint">Painted ground; null leaves the texel unpainted. Its alpha is not used: a painted
        /// texel always gets 255.</param>
        public void Set(int i, float h, (byte R, byte G, byte B) biome, (byte R, byte G, byte B) mask, (byte R, byte G, byte B, byte A)? paint)
        {
            Heights[i] = (Half)h;
            Biome[i * 4] = biome.R;
            Biome[i * 4 + 1] = biome.G;
            Biome[i * 4 + 2] = biome.B;
            Biome[i * 4 + 3] = 255;
            Mask[i * 4] = mask.R;
            Mask[i * 4 + 1] = mask.G;
            Mask[i * 4 + 2] = mask.B;
            Mask[i * 4 + 3] = 255;
            if (paint is { } p)
            {
                Paint[i * 4] = p.R;
                Paint[i * 4 + 1] = p.G;
                Paint[i * 4 + 2] = p.B;
                Paint[i * 4 + 3] = 255;
            }
        }

        /// <summary>
        /// Binary blob for the browser: heights (float16, little-endian) then biome, mask and paint (RGBA8 each).
        /// </summary>
        public byte[] ToBytes()
        {
            int n = Width * Height;
            byte[] result = new byte[n * 2 + n * 12];
            for (int i = 0; i < n; i++)
            {
                ushort bits = BitConverter.HalfToUInt16Bits(Heights[i]);
                result[i * 2] = (byte)bits;
                result[i * 2 + 1] = (byte)(bits >> 8);
            }
            Buffer.BlockCopy(Biome, 0, result, n * 2, n * 4);
            Buffer.BlockCopy(Mask, 0, result, n * 6, n * 4);
            Buffer.BlockCopy(Paint, 0, result, n * 10, n * 4);
            return result;
        }
    }
}
