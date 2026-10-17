using BCnEncoder.Decoder;
using BCnEncoder.Shared;

namespace TerrainEditor.App;

/// <summary>
/// The game's textures as pixels: one mip level (GameBundles.LoadTexture) made RGBA, rows bottom first
/// as Unity keeps them. Block-compressed ones (DXT1, DXT5, BC7...) are decoded with BCnEncoder.Net.
/// </summary>
public static partial class GameTextures
{
    /// <summary>
    /// RGBA bytes of a level, or null for a format not read here. Read: Unity's Alpha8, RGB24, RGBA32, ARGB32,
    /// BGRA32, R8, DXT1, DXT5, BC4, BC5 and BC7.
    /// </summary>
    public static byte[]? Rgba(GameBundles.TextureLevel t)
    {
        int n = t.Width * t.Height;
        byte[] rgba = new byte[n * 4];
        switch (t.Format)
        {
            case 10 or 12 or 25 or 26 or 27:
                var format = t.Format switch
                {
                    10 => CompressionFormat.Bc1,
                    12 => CompressionFormat.Bc3,
                    25 => CompressionFormat.Bc7,
                    26 => CompressionFormat.Bc4,
                    _ => CompressionFormat.Bc5,
                };
                var px = new BcDecoder().DecodeRaw(t.Data, t.Width, t.Height, format);
                for (int i = 0; i < n; i++)
                {
                    // BC4 (one channel) reads as grey, as the copy had it.
                    rgba[i * 4] = px[i].r;
                    rgba[i * 4 + 1] = t.Format == 26 ? px[i].r : px[i].g;
                    rgba[i * 4 + 2] = t.Format == 26 ? px[i].r : px[i].b;
                    rgba[i * 4 + 3] = t.Format is 10 or 26 ? (byte)255 : px[i].a;
                }
                return rgba;
            case 4:
                Buffer.BlockCopy(t.Data, 0, rgba, 0, n * 4);
                return rgba;
            case 5:
                for (int i = 0; i < n; i++)
                {
                    rgba[i * 4] = t.Data[i * 4 + 1];
                    rgba[i * 4 + 1] = t.Data[i * 4 + 2];
                    rgba[i * 4 + 2] = t.Data[i * 4 + 3];
                    rgba[i * 4 + 3] = t.Data[i * 4];
                }
                return rgba;
            case 14:
                for (int i = 0; i < n; i++)
                {
                    rgba[i * 4] = t.Data[i * 4 + 2];
                    rgba[i * 4 + 1] = t.Data[i * 4 + 1];
                    rgba[i * 4 + 2] = t.Data[i * 4];
                    rgba[i * 4 + 3] = t.Data[i * 4 + 3];
                }
                return rgba;
            case 3:
                for (int i = 0; i < n; i++)
                {
                    rgba[i * 4] = t.Data[i * 3];
                    rgba[i * 4 + 1] = t.Data[i * 3 + 1];
                    rgba[i * 4 + 2] = t.Data[i * 3 + 2];
                    rgba[i * 4 + 3] = 255;
                }
                return rgba;
            case 1 or 63:
                // Alpha8: black with that alpha; R8: grey.
                for (int i = 0; i < n; i++)
                {
                    byte v = t.Data[i];
                    rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = t.Format == 1 ? (byte)0 : v;
                    rgba[i * 4 + 3] = t.Format == 1 ? v : (byte)255;
                }
                return rgba;
            default:
                return null;
        }
    }
}
