using TerrainEditor.Save;

namespace TerrainEditor.App;

public sealed partial class MasterMap
{
    /// <summary>How far (in RGB, straight-line) a pixel's colour may be from the legend's to count as it.</summary>
    public const int Tolerance = 48;

    /// <summary>A picture read as a master map, with what the reading noticed.</summary>
    /// <param name="Map">The map.</param>
    /// <param name="Unknown">Pixels of a colour too far from every colour of the legend (each taken as the nearest).</param>
    /// <param name="FirstUnknown">The first such pixel (column, row of the picture read), or null.</param>
    /// <param name="Starts">How many starts were drawn far apart when more than one was (only the first is kept), else 0.</param>
    public sealed record Reading(MasterMap Map, int Unknown, (int Column, int Row)? FirstUnknown, int Starts);

    /// <summary>
    /// The master map a PNG picture draws: square, any size (scaled to <see cref="Size"/> by the nearest pixel), each
    /// pixel the legend's kind nearest its colour, the start colour's pixels the start point (the ground under it the
    /// kind most of the pixels round it are).
    /// </summary>
    /// <param name="png">The PNG file's bytes.</param>
    /// <exception cref="InvalidDataException">Not a PNG the reader takes (see <see cref="Png.ReadRgb"/>), or not square.</exception>
    public static Reading Read(byte[] png)
    {
        var img = Png.ReadRgb(png);
        if (img.Width != img.Height)
        {
            throw new InvalidDataException(Lang.T("masterMap.error.notSquare", img.Width, img.Height));
        }
        // Each pixel of the picture snapped once (start = 255).
        const byte startMark = 255;
        int n = img.Width;
        byte[] snapped = new byte[n * n];
        int unknown = 0;
        (int, int)? firstUnknown = null;
        for (int i = 0; i < n * n; i++)
        {
            byte r = img.Rgb[i * 3], g = img.Rgb[i * 3 + 1], b = img.Rgb[i * 3 + 2];
            if (Distance2(r, g, b, StartColour.R, StartColour.G, StartColour.B) <= Tolerance * Tolerance)
            {
                snapped[i] = startMark;
                continue;
            }
            var (kind, d2) = Nearest(r, g, b);
            snapped[i] = (byte)kind;
            if (d2 > Tolerance * Tolerance)
            {
                unknown++;
                firstUnknown ??= (i % n, i / n);
            }
        }
        byte[] cells = new byte[Size * Size];
        for (int row = 0; row < Size; row++)
        {
            int sy = (int)((long)row * n / Size);
            for (int col = 0; col < Size; col++)
            {
                int sx = (int)((long)col * n / Size);
                byte c = snapped[sy * n + sx];
                cells[row * Size + col] = c == startMark ? Around(snapped, n, sx, sy, startMark) : c;
            }
        }
        // Start pixels that touch are one start (drawn bigger than a pixel); groups apart are several.
        var (groups, middle) = StartGroups(snapped, n, startMark);
        (int, int)? start = middle is var (mx, my) ? ((int)((mx + 0.5) * Size / n), (int)((my + 0.5) * Size / n)) : null;
        return new Reading(new MasterMap(cells, start), unknown, firstUnknown, groups > 1 ? groups : 0);
    }

    /// <summary>
    /// The groups of touching start pixels (sideways, up and down or across a corner), counted up to 100 (plenty to say
    /// there are too many), and the middle of the first one (in the picture's pixels), or null when there is none.
    /// </summary>
    private static (int Groups, (double X, double Y)? First) StartGroups(byte[] snapped, int n, byte startMark)
    {
        bool[] seen = new bool[snapped.Length];
        int groups = 0;
        (double, double)? first = null;
        var todo = new Stack<int>();
        for (int i = 0; i < snapped.Length && groups < 100; i++)
        {
            if (snapped[i] != startMark || seen[i])
            {
                continue;
            }
            groups++;
            long sumX = 0, sumY = 0, count = 0;
            seen[i] = true;
            todo.Push(i);
            while (todo.Count > 0)
            {
                int p = todo.Pop();
                int x = p % n, y = p / n;
                (sumX, sumY, count) = (sumX + x, sumY + y, count + 1);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < n && ny < n && snapped[ny * n + nx] == startMark && !seen[ny * n + nx])
                        {
                            seen[ny * n + nx] = true;
                            todo.Push(ny * n + nx);
                        }
                    }
                }
            }
            first ??= ((double)sumX / count, (double)sumY / count);
        }
        return (groups, first);
    }

    /// <summary>The kind most of the pixels round a start pixel are (the Meadows when all are start pixels).</summary>
    private static byte Around(byte[] snapped, int n, int x, int y, byte startMark)
    {
        Span<int> votes = stackalloc int[Legend.Count];
        for (int r = 1; r <= 4; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int px = x + dx, py = y + dy;
                    if (px >= 0 && py >= 0 && px < n && py < n && snapped[py * n + px] is var c && c != startMark)
                    {
                        votes[c]++;
                    }
                }
            }
            int best = -1;
            for (int k = 0; k < votes.Length; k++)
            {
                if (votes[k] > 0 && (best < 0 || votes[k] > votes[best]))
                {
                    best = k;
                }
            }
            if (best >= 0)
            {
                return (byte)best;
            }
        }
        return (byte)Kind.Meadows;
    }

    /// <summary>The legend's kind nearest a colour, and how far (squared) it is.</summary>
    public static (Kind Kind, int Distance2) Nearest(byte r, byte g, byte b)
    {
        Kind best = Kind.Meadows;
        int bestD = int.MaxValue;
        foreach (var e in Legend)
        {
            int d = Distance2(r, g, b, e.R, e.G, e.B);
            if (d < bestD)
            {
                (best, bestD) = (e.Kind, d);
            }
        }
        return (best, bestD);
    }

    /// <summary>The squared straight-line distance between two colours.</summary>
    private static int Distance2(int r, int g, int b, int r2, int g2, int b2) => (r - r2) * (r - r2) + (g - g2) * (g - g2) + (b - b2) * (b - b2);

    /// <summary>The map as an RGBA picture, each cell its kind's colour, the start cell the start colour.</summary>
    public byte[] Rgba()
    {
        byte[] rgba = new byte[Size * Size * 4];
        for (int i = 0; i < Cells.Length; i++)
        {
            var e = Legend[Cells[i]];
            rgba[i * 4] = e.R;
            rgba[i * 4 + 1] = e.G;
            rgba[i * 4 + 2] = e.B;
            rgba[i * 4 + 3] = 255;
        }
        if (Start is { } s)
        {
            int i = s.Row * Size + s.Column;
            rgba[i * 4] = StartColour.R;
            rgba[i * 4 + 1] = StartColour.G;
            rgba[i * 4 + 2] = StartColour.B;
        }
        return rgba;
    }

    /// <summary>The map as a PNG picture (<see cref="Read"/> reads it back the same).</summary>
    public byte[] ToPng() => Png.WriteRgba8(Size, Size, Rgba());
}
