namespace TerrainEditor.Desktop;

// Smooth and Naturalize: one kernel, the average of the ground round a point as it was before, and the aim built on it.
public static partial class GroundOps
{
    /// <summary>
    /// How far round a point Smooth reads when it is applied in one go (the Area and Path tools), in grid points: each
    /// point moves towards the average of the ground within 3 m.
    /// </summary>
    public const int SmoothReach = 3;

    /// <summary>
    /// How far round a point the Smooth brush reads each frame, in grid points. A stroke applies the kernel a little
    /// every frame, so a small reach spreads as the brush is held: about a second and a half at the default strength
    /// evens the ground out as one Apply of the Area or Path tool does.
    /// </summary>
    public const int BrushSmoothReach = 1;

    /// <summary>
    /// The one kernel of Smooth and Naturalize: the average of the heights within r grid points of a point (the square
    /// round it, the part inside the ground).
    /// </summary>
    /// <param name="w">The ground's width in grid points.</param>
    /// <param name="h">The ground's depth in grid points.</param>
    /// <param name="p">The grid point (index z * width + x).</param>
    /// <param name="r">The reach, in grid points (0: the point alone).</param>
    /// <param name="heightOf">A grid point's height as it was before the action, by its index.</param>
    /// <returns>The average, in metres.</returns>
    public static float Average(int w, int h, int p, int r, Func<int, float> heightOf)
    {
        int gx = p % w, gz = p / w, n = 0;
        float sum = 0;
        for (int dz = -r; dz <= r; dz++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                // Inside the ground on both axes (a flat index would wrap into the next row).
                int x = gx + dx, z = gz + dz;
                if (x >= 0 && z >= 0 && x < w && z < h)
                {
                    sum += heightOf(z * w + x);
                    n++;
                }
            }
        }
        return sum / n;
    }

    /// <summary>Smooth: moves a point a share of the way towards the average of the ground round it, as it was before.</summary>
    /// <param name="g">The ground.</param>
    /// <param name="before">The heights before the action (or the frame), reaching far enough round the point.</param>
    /// <param name="p">The grid point.</param>
    /// <param name="k">The share of the way, 0 to 1.</param>
    /// <param name="reach">How far round the point the average reads, in grid points.</param>
    /// <returns>Whether the point reached the game's ±8 m limit.</returns>
    public static bool Smooth(Ground g, HeightsBefore before, int p, float k, int reach = SmoothReach) => Toward(g, p, before.Average(p, reach), k);

    /// <summary>
    /// How far round a point Naturalize reads the ground's broad shape, in grid points: a third of the bump size,
    /// between 2 and 8.
    /// </summary>
    /// <param name="b">The brush: its bump size.</param>
    /// <returns>The reach.</returns>
    public static int NaturalReach(Brush b) => Math.Max(2, Math.Min(8, (int)MathF.Round(b.NoiseSize / 3)));

    /// <summary>
    /// Naturalize's aim for a point: the ground's broad shape there plus the brush's bumps, placed by world
    /// coordinates so the pattern is continuous across areas and tools.
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="b">The brush: its bumps, bump size and pattern.</param>
    /// <param name="p">The grid point.</param>
    /// <param name="broad">The broad shape there: the average of the ground within <see cref="NaturalReach"/>, as it was before.</param>
    /// <returns>The height aimed at, in metres.</returns>
    public static float NaturalTarget(Ground g, Brush b, int p, float broad) =>
        broad + b.NoiseAmp * b.Noise.Fbm((g.OriginX + p % g.W) / b.NoiseSize, (g.OriginZ + p / g.W) / b.NoiseSize);

    /// <summary>Naturalize: moves a point a share of the way towards the ground's broad shape plus the brush's bumps.</summary>
    /// <param name="g">The ground.</param>
    /// <param name="b">The brush: its bumps, bump size and pattern.</param>
    /// <param name="before">The heights before the action, reaching <see cref="NaturalReach"/> round the point.</param>
    /// <param name="p">The grid point.</param>
    /// <param name="k">The share of the way, 0 to 1.</param>
    /// <returns>Whether the point reached the game's ±8 m limit.</returns>
    public static bool Naturalize(Ground g, Brush b, HeightsBefore before, int p, float k) =>
        Toward(g, p, NaturalTarget(g, b, p, before.Average(p, NaturalReach(b))), k);
}
