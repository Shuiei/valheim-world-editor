
namespace TerrainEditor.Save;

public static partial class Png
{
    /// <summary>The fields of a PNG's header (IHDR) the reader uses.</summary>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    /// <param name="Depth">Bits per channel.</param>
    /// <param name="Color">The colour type (0 grey, 2 RGB, 4 grey with alpha, 6 RGB with alpha).</param>
    internal sealed record PngHeader(int Width, int Height, int Depth, int Color);

    /// <summary>Reads a PNG picture as brightness per pixel; colour becomes luminance and alpha is ignored.</summary>
    /// <exception cref="InvalidDataException">
    /// Not a PNG, cut short, interlaced, palette-based, not 8 or 16 bits, an unknown row filter, empty, or more than
    /// 64 Mi (2^26) pixels.
    /// </exception>
    public static Image Read(byte[] bytes)
    {
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidDataException("not a PNG picture");
        }
        var (header, text, idat) = ScanChunks(bytes);
        if (header is not { } h)
        {
            throw new InvalidDataException("the PNG picture has no header (IHDR)");
        }
        int channels = ChannelsOf(h.Color);
        if (h.Depth is not 8 and not 16)
        {
            throw new InvalidDataException($"{h.Depth}-bit PNG pictures are not supported (8 or 16 bits)");
        }
        if (h.Width <= 0 || h.Height <= 0 || (long)h.Width * h.Height > 64L << 20)
        {
            throw new InvalidDataException("the picture is empty or too large");
        }
        int bpp = channels * h.Depth / 8, stride = h.Width * bpp;
        // Only as much as the rows need is unpacked: the data may claim far more.
        int need = (stride + 1) * h.Height;
        byte[] raw = Inflate(idat, need);
        if (raw.Length < need)
        {
            throw new InvalidDataException("the PNG picture data is cut short");
        }
        return new Image(h.Width, h.Height, Brightness(raw, h, channels, bpp), text);
    }

    /// <summary>The number of channels a colour type stores; a palette picture is refused.</summary>
    /// <param name="color">The colour type.</param>
    /// <returns>The channels per pixel.</returns>
    internal static int ChannelsOf(int color) => color switch
    {
        0 => 1,
        2 => 3,
        4 => 2,
        6 => 4,
        _ => throw new InvalidDataException("palette PNG pictures are not supported; save it as grayscale or RGB"),
    };
}
