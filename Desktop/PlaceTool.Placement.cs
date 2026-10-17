using System.Numerics;

namespace TerrainEditor.Desktop;

// A placement's random draws and the placed object, its height, clumping, water and elevation.
public sealed partial class PlaceTool
{
    /// <summary>
    /// One object's random draws, each from 0 to 1 unless said, kept so the preview stays put while the cursor moves.
    /// </summary>
    /// <param name="T">Picks the kind, by weight.</param>
    /// <param name="K">Clumping: kept where it is under the noise pattern's density.</param>
    /// <param name="Rx">Tilt about x, from -1 to 1 (times Tilt).</param>
    /// <param name="Ry">Facing, as a fraction of a turn (Random facing).</param>
    /// <param name="Rz">Tilt about z, from -1 to 1 (times Tilt).</param>
    /// <param name="S">Size between SizeMin and SizeMax.</param>
    /// <param name="W">Line: the sideways wiggle.</param>
    public sealed record Draw(float T, float K, float Rx, float Ry, float Rz, float S, float W);

    /// <summary>A fresh set of random draws.</summary>
    private Draw NewDraw() => new(_rand.NextSingle(), _rand.NextSingle(), _rand.NextSingle() * 2 - 1, _rand.NextSingle(), _rand.NextSingle() * 2 - 1, _rand.NextSingle(), _rand.NextSingle());

    /// <summary>A placement: the kind, where (world position, and the grid point), turned and scaled.</summary>
    /// <param name="Name">The kind, by prefab name.</param>
    /// <param name="Position">World position.</param>
    /// <param name="Rotation">Unity Euler rotation, in degrees.</param>
    /// <param name="Scale">Uniform scale (0: as the template object, as for pieces).</param>
    /// <param name="G">The grid point of the block it stands on.</param>
    public sealed record Placement(string Name, Vector3 Position, Vector3 Rotation, float Scale, Vector2 G);

    /// <summary>The ground's height at a grid point of the block.</summary>
    private static float HeightAt(WorldScene s, Vector2 g) => Picking.HeightAt(s, g.X - (s.W - 1) / 2f, -(g.Y - (s.H - 1) / 2f));

    /// <summary>
    /// Clumping (Brush and Zone): the density follows a noise pattern in world coordinates, so objects gather in groves
    /// and leave clearings. 0: even.
    /// </summary>
    private bool ClumpKeeps(WorldScene s, Vector2 g, float k)
    {
        float c = Clump / 100;
        if (c <= 0)
        {
            return true;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        float n = Math.Clamp(0.5f + 0.5f * Brush.Noise.Fbm((ox + g.X) / Patch + _clumpSeed, (oz + g.Y) / Patch - _clumpSeed), 0, 1);
        float lo = c * 0.75f - 0.1f, t = Math.Clamp((n - lo) / 0.25f, 0, 1);
        return k < t * t * (3 - 2 * t);
    }

    /// <summary>
    /// Kelp and seaweed may go under water; other kinds on the ground are left out below the water line.
    /// </summary>
    private static bool UnderwaterOk(IEnumerable<string> names) => names.Any(n => n.Contains("kelp", StringComparison.OrdinalIgnoreCase) || n.Contains("seaweed", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A piece's bottom below its origin (its lowest snap point: walls have their origin in the middle).
    /// </summary>
    private static float? BottomOf(string name) => SnapsOf(name) is { Length: > 0 } sp ? sp.Min(p => p[1]) : null;

    /// <summary>
    /// The height an object's origin goes at for the Elevation: on the ground, a height above it, or one height for
    /// all; a piece's bottom (its lowest snap point) is what stands there.
    /// </summary>
    /// <param name="name">The kind, by prefab name.</param>
    /// <param name="groundY">The ground's height under it.</param>
    public float Elevated(string name, float groundY)
    {
        float? b = BottomOf(name);
        return Elevation == Elevations.At ? Elev - (b ?? 0) : groundY - (b ?? 0.05f) + (Elevation == Elevations.Above ? Elev : 0);
    }
}
