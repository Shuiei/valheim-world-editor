// The look's texture reading: a texture's top level as pixels, a texture array as one picture, and the flip of a map (see GameLookData).

namespace TerrainEditor.App;

/// <summary>
/// The game's look besides models: this part reads the textures the terrain and the world map are made of.
/// </summary>
public static partial class GameLookData
{
    /// <summary>
    /// A texture's top mip level as RGBA, rows turned top first with <c>flip</c>; null when it cannot be read or
    /// converted.
    /// </summary>
    private static Picture? Texture(GameBundles game, GameBundles.Where at, bool flip)
    {
        if (game.LoadTexture(at, int.MaxValue) is not { } t || GameTextures.Rgba(t) is not { } rgba)
        {
            return null;
        }
        if (flip)
        {
            Flip(rgba, t.Width, t.Height);
        }
        return new Picture(t.Width, t.Height, rgba);
    }

    /// <summary>
    /// A texture array's slices as one picture, one below the other (rows bottom first). Throws InvalidDataException
    /// when it cannot be read.
    /// </summary>
    private static Picture Array(GameBundles game, string file)
    {
        var slices = (game.Asset(file) is { } at ? game.LoadTextureArray(at) : null) ?? throw new InvalidDataException($"The terrain's texture array {file} could not be read.");
        int w = slices[0].Width, h = slices[0].Height;
        // In long: 4096 × 4096 × 32 slices passes int's range.
        long size = (long)w * h * 4 * slices.Count;
        if (size > System.Array.MaxLength)
        {
            throw new InvalidDataException($"The terrain's texture array {file} is too large to read here ({w} × {h} × {slices.Count}).");
        }
        byte[] all = new byte[size];
        for (int i = 0; i < slices.Count; i++)
        {
            byte[] rgba = GameTextures.Rgba(slices[i]) ?? throw new InvalidDataException($"The terrain's texture array {file} is in a format not read here ({slices[i].Format}).");
            Buffer.BlockCopy(rgba, 0, all, i * w * h * 4, rgba.Length);
        }
        return new Picture(w, h * slices.Count, all);
    }

    /// <summary>
    /// Turns an RGBA picture upside down, in place.
    /// </summary>
    private static void Flip(byte[] rgba, int w, int h)
    {
        int row = w * 4;
        byte[] tmp = new byte[row];
        for (int y = 0; y < h / 2; y++)
        {
            Buffer.BlockCopy(rgba, y * row, tmp, 0, row);
            Buffer.BlockCopy(rgba, (h - 1 - y) * row, rgba, y * row, row);
            Buffer.BlockCopy(tmp, 0, rgba, (h - 1 - y) * row, row);
        }
    }
}
