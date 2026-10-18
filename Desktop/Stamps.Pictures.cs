using SkiaSharp;

namespace TerrainEditor.Desktop;

public static partial class Stamps
{
    /// <summary>
    /// A picture to Size × Size weights: brightness times opacity. Null when it cannot be read or would take more than
    /// <see cref="MostPictureBytes"/> decoded.
    /// </summary>
    /// <param name="bytes">The picture file's contents, in any format Skia decodes.</param>
    public static float[]? FromPicture(byte[] bytes)
    {
        // Not a picture: no codec (Decode would throw).
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        if (codec == null || codec.Info.BytesSize64 > MostPictureBytes)
        {
            return null;
        }
        using var src = Decoded(codec);
        if (src == null)
        {
            return null;
        }
        using var small = src.Resize(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (small == null)
        {
            return null;
        }
        float[] data = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var c = small.GetPixel(x, y);
                data[y * Size + x] = (0.299f * c.Red + 0.587f * c.Green + 0.114f * c.Blue) / 255f * (c.Alpha / 255f);
            }
        }
        return data;
    }

    /// <summary>
    /// The codec's picture as SKBitmap.Decode makes it (premultiplied, no colour space), or null when its pixels cannot be
    /// allocated or decoded: Decode throws when they cannot be allocated.
    /// </summary>
    /// <param name="codec">The picture's codec.</param>
    private static SKBitmap? Decoded(SKCodec codec)
    {
        var info = codec.Info;
        info = info.WithAlphaType(info.AlphaType == SKAlphaType.Unpremul ? SKAlphaType.Premul : info.AlphaType).WithColorSpace(null);
        var bitmap = new SKBitmap();
        if (bitmap.TryAllocPixels(info) && codec.GetPixels(info, bitmap.GetPixels()) is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bitmap;
        }
        bitmap.Dispose();
        return null;
    }
}
