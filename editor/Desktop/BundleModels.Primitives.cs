using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>Unity's own primitive meshes (BundleModels.Primitives.cs), made here since no bundle holds them.</summary>
internal sealed partial class BundleModels
{
    /// <summary>What marks a mesh id as one of Unity's own primitives, made here (no bundle holds them).</summary>
    private const char Primitive = '~';

    /// <summary>
    /// Unity's own meshes (GameObject.CreatePrimitive, and the built-in meshes a prefab can use), by name, with their
    /// bounds (centre and extent): mods use them, and no bundle holds them.
    /// </summary>
    private static readonly Dictionary<string, float[]> Primitives = new(StringComparer.Ordinal)
    {
        ["Cube"] = [0, 0, 0, 0.5f, 0.5f, 0.5f],
        ["Quad"] = [0, 0, 0, 0.5f, 0.5f, 0],
        ["Plane"] = [0, 0, 0, 5, 0, 5],
        ["Cylinder"] = [0, 0, 0, 0.5f, 1, 0.5f],
        ["Sphere"] = [0, 0, 0, 0.5f, 0.5f, 0.5f],
        ["Capsule"] = [0, 0, 0, 0.5f, 1, 0.5f],
    };

    /// <summary>How many sides the round primitives (cylinder, sphere, capsule) are made with around their axis.</summary>
    private const int RoundSides = 24;

    /// <summary>How many rings of quads make a hemisphere of the sphere or the capsule, pole to equator.</summary>
    private const int HalfRings = 8;

    /// <summary>
    /// One of Unity's own primitives (see <see cref="Primitives"/>) as GameBundles reads meshes: a box or a flat square,
    /// each face with its own vertices, normal and uv (0 to 1), or a round one (see <see cref="RoundMesh"/>); seen from
    /// both sides (its material may cull either).
    /// </summary>
    internal static GameBundles.Mesh? PrimitiveMesh(string name)
    {
        if (!Primitives.TryGetValue(name, out float[]? b))
        {
            return null;
        }
        if (name is "Cylinder" or "Sphere" or "Capsule")
        {
            return RoundMesh(name);
        }
        var pos = new List<float>();
        var nor = new List<float>();
        var uv = new List<float>();
        var idx = new List<int>();
        void Face(Vector3 n, Vector3 u, Vector3 v)
        {
            Vector3 c = new Vector3(b[0], b[1], b[2]) + n;
            int first = pos.Count / 3;
            foreach (var (du, dv) in ((float, float)[])[(-1, -1), (1, -1), (1, 1), (-1, 1)])
            {
                Vector3 q = c + du * u + dv * v;
                pos.AddRange([q.X, q.Y, q.Z]);
                var dir = Vector3.Normalize(n == Vector3.Zero ? Vector3.Cross(v, u) : n);
                nor.AddRange([dir.X, dir.Y, dir.Z]);
                uv.AddRange([(du + 1) / 2, (dv + 1) / 2]);
            }
            idx.AddRange([first, first + 1, first + 2, first, first + 2, first + 3, first, first + 2, first + 1, first, first + 3, first + 2]);
        }
        var e = new Vector3(b[3], b[4], b[5]);
        if (e.X > 0 && e.Y > 0 && e.Z > 0)
        {
            Face(new(e.X, 0, 0), new(0, 0, e.Z), new(0, e.Y, 0));
            Face(new(-e.X, 0, 0), new(0, 0, -e.Z), new(0, e.Y, 0));
            Face(new(0, e.Y, 0), new(e.X, 0, 0), new(0, 0, e.Z));
            Face(new(0, -e.Y, 0), new(e.X, 0, 0), new(0, 0, -e.Z));
            Face(new(0, 0, e.Z), new(-e.X, 0, 0), new(0, e.Y, 0));
            Face(new(0, 0, -e.Z), new(e.X, 0, 0), new(0, e.Y, 0));
        }
        else
        {
            // Flat: a quad (x, y) or a plane (x, z).
            Face(Vector3.Zero, new(e.X, 0, 0), e.Y > 0 ? new(0, e.Y, 0) : new(0, 0, e.Z));
        }
        return new GameBundles.Mesh(name, pos.Count / 3, [.. pos], [.. nor], [.. uv], [[.. idx]]);
    }

    /// <summary>
    /// Unity's cylinder (radius 0.5, 2 high, with flat ends), sphere (radius 0.5) or capsule (radius 0.5, 2 high, round
    /// ends), centred on the origin, upright: a profile (distance from the axis, height, and the normal's two parts)
    /// turned about y in <see cref="RoundSides"/> steps, normals outward, u around (0 to 1) and v along the profile (0 to
    /// 1, the ends' discs each 0 to 1 from rim to centre), each triangle wound both ways.
    /// </summary>
    private static GameBundles.Mesh RoundMesh(string name)
    {
        var pos = new List<float>();
        var nor = new List<float>();
        var uv = new List<float>();
        var idx = new List<int>();
        void Turn(IReadOnlyList<(float R, float Y, float Nr, float Ny)> profile)
        {
            int first = pos.Count / 3, rows = profile.Count;
            for (int j = 0; j <= RoundSides; j++)
            {
                float a = 2 * MathF.PI * j / RoundSides, cos = MathF.Cos(a), sin = MathF.Sin(a);
                for (int i = 0; i < rows; i++)
                {
                    var (r, y, nr, ny) = profile[i];
                    pos.AddRange([r * cos, y, r * sin]);
                    var n = Vector3.Normalize(new Vector3(nr * cos, ny, nr * sin));
                    nor.AddRange([n.X, n.Y, n.Z]);
                    uv.AddRange([(float)j / RoundSides, (float)i / (rows - 1)]);
                }
            }
            for (int j = 0; j < RoundSides; j++)
            {
                for (int i = 0; i + 1 < rows; i++)
                {
                    int a = first + j * rows + i, b = a + rows;
                    idx.AddRange([a, b, b + 1, a, b + 1, a + 1, a, b + 1, b, a, a + 1, b + 1]);
                }
            }
        }
        // A hemisphere's rings, from the equator (latitude 0) to the pole (90°) of the side the sign gives, the centre at
        // that height.
        IEnumerable<(float, float, float, float)> Half(float centre, int sign) => Enumerable.Range(0, HalfRings + 1).Select(k =>
        {
            float lat = sign * MathF.PI / 2 * k / HalfRings;
            return (0.5f * MathF.Cos(lat), centre + 0.5f * MathF.Sin(lat), MathF.Cos(lat), MathF.Sin(lat));
        });
        switch (name)
        {
            case "Cylinder":
                Turn([(0.5f, -1, 1, 0), (0.5f, 1, 1, 0)]);
                Turn([(0.5f, 1, 0, 1), (0, 1, 0, 1)]);
                Turn([(0.5f, -1, 0, -1), (0, -1, 0, -1)]);
                break;
            case "Sphere":
                Turn([.. Half(0, -1).Reverse(), .. Half(0, 1).Skip(1)]);
                break;
            default:
                Turn([.. Half(-0.5f, -1).Reverse(), .. Half(0.5f, 1)]);
                break;
        }
        return new GameBundles.Mesh(name, pos.Count / 3, [.. pos], [.. nor], [.. uv], [[.. idx]]);
    }
}
