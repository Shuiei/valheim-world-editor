namespace TerrainEditor.App;

/// <summary>
/// Cut-out bleeding of transparent texture pixels (see <see cref="Bleed"/>), kept apart from the RGBA conversion.
/// </summary>
public static partial class GameTextures
{
    /// <summary>
    /// The pixel at index <paramref name="i"/> and its eight neighbours, wrapping around the edges (the texture tiles),
    /// as indices into the pixels, the pixel itself among them.
    /// </summary>
    /// <param name="i">The pixel's index (row times width, plus column).</param>
    /// <param name="w">The texture's width in pixels.</param>
    /// <param name="h">The texture's height in pixels.</param>
    /// <param name="into">Nine entries to fill, the rows top to bottom and each row left to right.</param>
    private static void Neighbourhood(int i, int w, int h, Span<int> into)
    {
        int x = i % w, y = i / w;
        int k = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                into[k++] = ((y + dy + h) % h) * w + (x + dx + w) % w;
            }
        }
    }

    /// <summary>
    /// Cut-out textures: transparent pixels take the colour of nearby opaque ones (up to 16 pixels away,
    /// wrapping around: textures tile), the rest the average opaque colour, so that filtering and mipmaps
    /// do not bleed the hidden colour (often beige) into leaf edges. Alpha is kept. Opaque means alpha of
    /// 128 or more; fully opaque or fully transparent textures are left alone. Works in place.
    /// </summary>
    public static void Bleed(byte[] rgba, int w, int h)
    {
        int n = w * h;
        bool[] filled = new bool[n];
        int opaque = 0;
        long sr = 0, sg = 0, sb = 0;
        for (int i = 0; i < n; i++)
        {
            if (rgba[i * 4 + 3] >= 128)
            {
                filled[i] = true;
                opaque++;
                sr += rgba[i * 4];
                sg += rgba[i * 4 + 1];
                sb += rgba[i * 4 + 2];
            }
        }
        if (opaque == 0 || opaque == n)
        {
            return;
        }
        var next = new List<(int I, byte R, byte G, byte B)>();
        // Each pass looks only at the frontier: the pixels not filled yet next to one the last pass filled (the
        // first pass, at every one not filled). Any other has no filled neighbour, or got its colour already.
        List<int> frontier = [.. Enumerable.Range(0, n).Where(i => !filled[i])];
        int[] queued = new int[n];
        Span<int> around = stackalloc int[9];
        for (int it = 0; it < 16 && frontier.Count > 0; it++)
        {
            next.Clear();
            foreach (int i in frontier)
            {
                int c = 0, r = 0, g = 0, b = 0;
                Neighbourhood(i, w, h, around);
                foreach (int j in around)
                {
                    if (filled[j])
                    {
                        c++;
                        r += rgba[j * 4];
                        g += rgba[j * 4 + 1];
                        b += rgba[j * 4 + 2];
                    }
                }
                if (c > 0)
                {
                    next.Add((i, (byte)((r + c / 2) / c), (byte)((g + c / 2) / c), (byte)((b + c / 2) / c)));
                }
            }
            if (next.Count == 0)
            {
                break;
            }
            foreach (var (i, r, g, b) in next)
            {
                rgba[i * 4] = r;
                rgba[i * 4 + 1] = g;
                rgba[i * 4 + 2] = b;
                filled[i] = true;
            }
            // The next frontier, each pixel once (the order does not matter: a pass reads only what was filled before it).
            frontier.Clear();
            foreach (var (i, _, _, _) in next)
            {
                Neighbourhood(i, w, h, around);
                foreach (int j in around)
                {
                    if (!filled[j] && queued[j] != it + 1)
                    {
                        queued[j] = it + 1;
                        frontier.Add(j);
                    }
                }
            }
        }
        byte mr = (byte)(sr / opaque), mg = (byte)(sg / opaque), mb = (byte)(sb / opaque);
        for (int i = 0; i < n; i++)
        {
            if (!filled[i])
            {
                rgba[i * 4] = mr;
                rgba[i * 4 + 1] = mg;
                rgba[i * 4 + 2] = mb;
            }
        }
    }
}
