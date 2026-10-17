#nullable disable

// Minimal stand-ins for the Unity types the world generator works with. Arithmetic matches Unity's
// managed implementations (single-precision float math).
namespace ValheimGen;

/// <summary>
/// Unity's Vector2: a 2D point or direction in single precision.
/// </summary>
public struct Vector2
{
    /// <summary>
    /// The x component (world x, for map points).
    /// </summary>
    public float x;

    /// <summary>
    /// The y component (world z, for map points).
    /// </summary>
    public float y;

    /// <summary>
    /// The vector (x, y).
    /// </summary>
    public Vector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>
    /// (0, 0).
    /// </summary>
    public static Vector2 zero => new(0f, 0f);

    /// <summary>
    /// (0, 1).
    /// </summary>
    public static Vector2 up => new(0f, 1f);

    /// <summary>
    /// (0, -1).
    /// </summary>
    public static Vector2 down => new(0f, -1f);

    /// <summary>
    /// (-1, 0).
    /// </summary>
    public static Vector2 left => new(-1f, 0f);

    /// <summary>
    /// (1, 0).
    /// </summary>
    public static Vector2 right => new(1f, 0f);

    /// <summary>
    /// The length. Squares and sums in double, as the game's Mono runtime does with these floats (the float
    /// result differed now and then: rivers and streams a hair off).
    /// </summary>
    public readonly float magnitude => (float)Math.Sqrt((double)x * x + (double)y * y);

    /// <summary>
    /// The squared length, summed in double like <see cref="magnitude"/>.
    /// </summary>
    public readonly float sqrMagnitude => (float)((double)x * x + (double)y * y);

    /// <summary>
    /// The same direction at length 1; zero for vectors shorter than 1e-5, as Unity's.
    /// </summary>
    public readonly Vector2 normalized
    {
        get
        {
            float m = magnitude;
            return m > 1E-05f ? this / m : zero;
        }
    }

    /// <summary>
    /// The distance between two points, summed in double like <see cref="magnitude"/>.
    /// </summary>
    public static float Distance(Vector2 a, Vector2 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
    }

    /// <summary>
    /// The squared length of a, summed in double.
    /// </summary>
    public static float SqrMagnitude(Vector2 a) => (float)((double)a.x * a.x + (double)a.y * a.y);

    /// <summary>
    /// The sum.
    /// </summary>
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);

    /// <summary>
    /// The difference.
    /// </summary>
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);

    /// <summary>
    /// The opposite direction.
    /// </summary>
    public static Vector2 operator -(Vector2 a) => new(0f - a.x, 0f - a.y);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector2 operator *(float d, Vector2 a) => new(a.x * d, a.y * d);

    /// <summary>
    /// Divided by d.
    /// </summary>
    public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);

    /// <summary>
    /// Unity's approximate equality: the points are less than 1e-5 apart. (Equals stays exact.)
    /// </summary>
    public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 9.99999944E-11f;

    /// <summary>
    /// Not approximately equal (see ==).
    /// </summary>
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    /// <summary>
    /// Exact equality of both components, unlike ==.
    /// </summary>
    public override readonly bool Equals(object obj) => obj is Vector2 v && x.Equals(v.x) && y.Equals(v.y);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
}

/// <summary>
/// Unity's Vector3: a 3D point or direction in single precision (y up).
/// </summary>
public struct Vector3
{
    /// <summary>
    /// The x component (east).
    /// </summary>
    public float x;

    /// <summary>
    /// The y component (height).
    /// </summary>
    public float y;

    /// <summary>
    /// The z component (north).
    /// </summary>
    public float z;

    /// <summary>
    /// The vector (x, y, z).
    /// </summary>
    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    /// <summary>
    /// (0, 0, 0).
    /// </summary>
    public static Vector3 zero => new(0f, 0f, 0f);

    /// <summary>
    /// The length (summed in float).
    /// </summary>
    public readonly float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);

    /// <summary>
    /// The same direction at length 1; zero for vectors shorter than 1e-5.
    /// </summary>
    public readonly Vector3 normalized => Normalize(this);

    /// <summary>
    /// v at length 1; zero for vectors shorter than 1e-5, as Unity's.
    /// </summary>
    public static Vector3 Normalize(Vector3 v)
    {
        float m = v.magnitude;
        return m > 1E-05f ? v / m : zero;
    }

    /// <summary>
    /// The sum.
    /// </summary>
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);

    /// <summary>
    /// The difference.
    /// </summary>
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);

    /// <summary>
    /// Scaled by d.
    /// </summary>
    public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);

    /// <summary>
    /// Divided by d.
    /// </summary>
    public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);
}

/// <summary>
/// Valheim's Vector2i: an integer grid cell (the river cells' dictionary key).
/// </summary>
public struct Vector2i : IEquatable<Vector2i>
{
    /// <summary>
    /// The cell's x.
    /// </summary>
    public int x;

    /// <summary>
    /// The cell's y (world z).
    /// </summary>
    public int y;

    /// <summary>
    /// The cell (x, y).
    /// </summary>
    public Vector2i(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    /// <inheritdoc/>
    public readonly bool Equals(Vector2i other) => x == other.x && y == other.y;

    /// <inheritdoc/>
    public override readonly bool Equals(object obj) => obj is Vector2i v && Equals(v);

    /// <summary>
    /// Spread out (x ^ y gave the world's grid cells only about 512 hashes: long chains in a dictionary).
    /// </summary>
    public override readonly int GetHashCode() => unchecked(x * 73856093 ^ y * 19349663);

    /// <summary>
    /// The same cell.
    /// </summary>
    public static bool operator ==(Vector2i a, Vector2i b) => a.Equals(b);

    /// <summary>
    /// Different cells.
    /// </summary>
    public static bool operator !=(Vector2i a, Vector2i b) => !a.Equals(b);
}

/// <summary>
/// Valheim's Vector2s: a pair of shorts (zone corners, as the biome caches key them).
/// </summary>
public struct Vector2s : IEquatable<Vector2s>
{
    /// <summary>
    /// The x.
    /// </summary>
    public short x;

    /// <summary>
    /// The y (world z).
    /// </summary>
    public short y;

    /// <summary>
    /// The pair (x, y), each cut to a short.
    /// </summary>
    public Vector2s(int x, int y)
    {
        this.x = (short)x;
        this.y = (short)y;
    }

    /// <summary>
    /// The difference, cut to shorts.
    /// </summary>
    public static Vector2s operator -(Vector2s a, Vector2s b) => new(a.x - b.x, a.y - b.y);

    /// <inheritdoc/>
    public readonly bool Equals(Vector2s other) => x == other.x && y == other.y;

    /// <inheritdoc/>
    public override readonly bool Equals(object obj) => obj is Vector2s v && Equals(v);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => (ushort)x | (y << 16);
}

/// <summary>
/// Unity's Color: four 0..1 floats; the generator uses it for the base paint mask.
/// </summary>
public struct Color
{
    /// <summary>
    /// Red.
    /// </summary>
    public float r;

    /// <summary>
    /// Green.
    /// </summary>
    public float g;

    /// <summary>
    /// Blue.
    /// </summary>
    public float b;

    /// <summary>
    /// Alpha.
    /// </summary>
    public float a;

    /// <summary>
    /// The colour (r, g, b, a).
    /// </summary>
    public Color(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    /// <summary>
    /// Opaque black (0, 0, 0, 1): no base paint.
    /// </summary>
    public static Color black => new(0f, 0f, 0f, 1f);
}

/// <summary>
/// Unity's Mathf: the few helpers the generator calls, with sine and cosine worked out in double.
/// </summary>
public static class Mathf
{
    /// <summary>
    /// |f|.
    /// </summary>
    public static float Abs(float f) => Math.Abs(f);

    /// <summary>
    /// The sine of f (radians), worked out in double.
    /// </summary>
    public static float Sin(float f) => (float)Math.Sin(f);

    /// <summary>
    /// The cosine of f (radians), worked out in double.
    /// </summary>
    public static float Cos(float f) => (float)Math.Cos(f);

    /// <summary>
    /// The smaller of a and b.
    /// </summary>
    public static float Min(float a, float b) => a < b ? a : b;

    /// <summary>
    /// The smaller of a and b.
    /// </summary>
    public static int Min(int a, int b) => a < b ? a : b;

    /// <summary>
    /// The larger of a and b.
    /// </summary>
    public static float Max(float a, float b) => a > b ? a : b;

    /// <summary>
    /// The larger of a and b.
    /// </summary>
    public static int Max(int a, int b) => a > b ? a : b;

    /// <summary>
    /// f rounded up.
    /// </summary>
    public static float Ceil(float f) => (float)Math.Ceiling(f);

    /// <summary>
    /// f rounded up, as an int.
    /// </summary>
    public static int CeilToInt(float f) => (int)Math.Ceiling(f);

    /// <summary>
    /// f rounded down, as an int.
    /// </summary>
    public static int FloorToInt(float f) => (int)Math.Floor(f);

    /// <summary>
    /// value held to min..max.
    /// </summary>
    public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    /// <summary>
    /// value held to min..max.
    /// </summary>
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}

/// <summary>
/// Valheim's Utils: the one helper the generator calls.
/// </summary>
public static class Utils
{
    /// <summary>
    /// Where v lies from l (0) to h (1), held to 0..1.
    /// </summary>
    public static float LerpStep(float l, float h, float v) => Mathf.Clamp((v - l) / (h - l), 0f, 1f);
}

/// <summary>
/// Valheim's ZLog: logging is dropped offline.
/// </summary>
public static class ZLog
{
    /// <summary>
    /// Does nothing.
    /// </summary>
    public static void Log(object _)
    {
    }
}

/// <summary>
/// Valheim's Heightmap: only its biome enums.
/// </summary>
public static class Heightmap
{
    /// <summary>
    /// The game's biomes, as flags, with the game's own numbers.
    /// </summary>
    public enum Biome
    {
        /// <summary>No biome.</summary>
        None = 0,
        /// <summary>Meadows.</summary>
        Meadows = 1,
        /// <summary>Swamp.</summary>
        Swamp = 2,
        /// <summary>Mountains.</summary>
        Mountain = 4,
        /// <summary>Black Forest.</summary>
        BlackForest = 8,
        /// <summary>Plains.</summary>
        Plains = 0x10,
        /// <summary>Ashlands.</summary>
        AshLands = 0x20,
        /// <summary>Deep North.</summary>
        DeepNorth = 0x40,
        /// <summary>Ocean.</summary>
        Ocean = 0x100,
        /// <summary>Mistlands.</summary>
        Mistlands = 0x200,
        /// <summary>
        /// Every biome.
        /// </summary>
        All = 0x37F,
        /// <summary>
        /// Every biome but Ocean.
        /// </summary>
        Land = 0x27F
    }

    /// <summary>
    /// Where in a biome a zone or point lies, for location placement (flags).
    /// </summary>
    public enum BiomeArea
    {
        /// <summary>
        /// On a biome's edge: the zone's corner and the corners round it are not all in one biome.
        /// </summary>
        Edge = 1,
        /// <summary>
        /// In a biome's middle.
        /// </summary>
        Median = 2,
        /// <summary>
        /// Both.
        /// </summary>
        Everything = 3
    }
}

/// <summary>
/// Biome sectors only drive textures and vegetation, never height; the generator gets the
/// "no biome data" answer, the same as for a world without Deep North data.
/// </summary>
public sealed class BiomeSector
{
    /// <summary>
    /// The sector's biome.
    /// </summary>
    public Heightmap.Biome Biome;

    /// <summary>
    /// An empty Black Forest sector.
    /// </summary>
    public static readonly BiomeSector EmptyBlackForest = new() { Biome = Heightmap.Biome.BlackForest };

    /// <summary>
    /// An empty Meadows sector.
    /// </summary>
    public static readonly BiomeSector EmptyMeadows = new() { Biome = Heightmap.Biome.Meadows };
}

/// <summary>
/// Stand-in for the game's biome sector data: never ready, so the generator takes the "no biome data" path.
/// </summary>
public sealed class AltBiomeWorldData
{
    /// <summary>
    /// Always false: there is no sector data offline.
    /// </summary>
    public bool IsReady => false;

    /// <summary>
    /// Always null.
    /// </summary>
    public BiomeSector[,] PointSectors => null;

    /// <summary>
    /// Always 0.
    /// </summary>
    public static int WorldSpaceToMapSpace(float _) => 0;
}

/// <summary>
/// Valheim's World: what the generator needs to know about the world it makes.
/// </summary>
public sealed class World
{
    /// <summary>
    /// The seed (the seed name's stable hash).
    /// </summary>
    public int m_seed;

    /// <summary>
    /// The seed as players type it.
    /// </summary>
    public string m_seedName = "";

    /// <summary>
    /// The world generator version the world was made with (older worlds keep older rules).
    /// </summary>
    public int m_worldGenVersion = 2;

    /// <summary>
    /// The main menu's background world: simpler terrain and no rivers.
    /// </summary>
    public bool m_menu;

    /// <summary>
    /// Biome sector data; never set offline.
    /// </summary>
    public AltBiomeWorldData m_biomeData;
}
