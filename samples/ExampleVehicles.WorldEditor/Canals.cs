using System;
using System.Collections.Generic;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// The canals (Keys.Canal): the mod digs a trench for its boats along a path it keeps in an object of its own, an
/// object that only holds the mod's data (its manifest's "data"). The trench is drawn as one mesh over the area with
/// the game's ground look (the biome colours of the ground there, its bed painted as dirt), and the editor's ground is
/// left undrawn over it: the scene layer hooks.
/// </summary>
public sealed class Canals : ISceneProvider
{
    /// <summary>A canal's width when it keeps none, in metres.</summary>
    private const float DefaultWidth = 3f;

    /// <summary>A canal's depth when it keeps none, in metres.</summary>
    private const float DefaultDepth = 1.5f;

    /// <summary>How far past its edge the ground is dug again (the trench's banks), in metres.</summary>
    private const float Banks = 1.5f;

    /// <summary>Its paint mask: the game's untouched ground, then dirt (red), side by side.</summary>
    private static readonly MaskTexture Paint = new(2, 1, [0xFF000000u, 0xFF0000FFu]);

    /// <inheritdoc/>
    public SceneLayer? Build(IAreaView area)
    {
        var layer = new SceneLayer();
        foreach (var canal in area.ObjectsOf(Keys.Canal))
        {
            if (canal.TryGetBytes(Keys.Path, out byte[] bytes) && ReadPath(bytes) is { Count: >= 2 } path)
            {
                float width = canal.TryGetFloat(Keys.Width, out float w) && w > 0 ? w : DefaultWidth;
                float depth = canal.TryGetFloat(Keys.Depth, out float d) && d > 0 ? d : DefaultDepth;
                Dig(area, path, width / 2, depth, layer);
            }
        }
        return layer.Meshes.Count == 0 ? null : layer;
    }

    /// <summary>
    /// A path as the mod keeps it: a version byte (1), then each point's world x and z as two floats (little-endian).
    /// Null when the bytes are not in that form.
    /// </summary>
    /// <param name="bytes">The path's bytes.</param>
    public static List<Vector2>? ReadPath(byte[] bytes)
    {
        if (bytes.Length < 1 || bytes[0] != 1 || (bytes.Length - 1) % 8 != 0)
        {
            return null;
        }
        var points = new List<Vector2>();
        for (int i = 1; i < bytes.Length; i += 8)
        {
            points.Add(new Vector2(BitConverter.ToSingle(bytes, i), BitConverter.ToSingle(bytes, i + 4)));
        }
        return points;
    }

    /// <summary>A path in the mod's form (<see cref="ReadPath"/>).</summary>
    /// <param name="points">The points, world x and z.</param>
    public static byte[] WritePath(IReadOnlyList<Vector2> points)
    {
        byte[] bytes = new byte[1 + points.Count * 8];
        bytes[0] = 1;
        for (int i = 0; i < points.Count; i++)
        {
            BitConverter.GetBytes(points[i].X).CopyTo(bytes, 1 + i * 8);
            BitConverter.GetBytes(points[i].Y).CopyTo(bytes, 5 + i * 8);
        }
        return bytes;
    }

    /// <summary>
    /// Digs one canal: every metre square of ground within its half width and banks of the path is left undrawn, and
    /// the trench drawn there instead, a metre grid whose points within the half width lie a depth below the ground.
    /// </summary>
    private static void Dig(IAreaView area, List<Vector2> path, float half, float depth, SceneLayer layer)
    {
        float reach = half + Banks;
        int x0 = (int)Math.Floor(Min(path, p => p.X) - reach), x1 = (int)Math.Ceiling(Max(path, p => p.X) + reach);
        int z0 = (int)Math.Floor(Min(path, p => p.Y) - reach), z1 = (int)Math.Ceiling(Max(path, p => p.Y) + reach);
        var corners = new Dictionary<(int, int), int>();
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var biomes = new List<uint>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        int Corner(int x, int z)
        {
            if (!corners.TryGetValue((x, z), out int i))
            {
                bool bed = Distance(path, new Vector2(x, z)) <= half;
                corners[(x, z)] = i = positions.Count;
                positions.Add(new Vector3(x, area.GroundHeight(x, z) - (bed ? depth : 0), z));
                normals.Add(Vector3.UnitY);
                biomes.Add(area.BiomeColor(x, z));
                uvs.Add(new Vector2(bed ? 0.75f : 0.25f, 0.5f));
            }
            return i;
        }
        for (int z = z0; z < z1; z++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (Distance(path, new Vector2(x + 0.5f, z + 0.5f)) > reach - 0.5f)
                {
                    continue;
                }
                layer.Holes.Add(new GroundHole(x, z, x + 1, z + 1));
                int a = Corner(x, z), b = Corner(x, z + 1), c = Corner(x + 1, z + 1), d = Corner(x + 1, z);
                // Clockwise seen from above, as Unity winds a face up.
                triangles.AddRange([a, b, c, a, c, d]);
            }
        }
        Vector3[] points = [.. positions];
        layer.Meshes.Add(new SceneMesh(points, Normals(points, triangles), [.. triangles])
        {
            BiomeColors = [.. biomes],
            Mask = Paint,
            MaskUvs = [.. uvs],
            Color = new Vector4(0.55f, 0.45f, 0.3f, 1f),
        });
    }

    /// <summary>Each corner's normal: the sum of its triangles' faces, made unit length.</summary>
    private static Vector3[] Normals(Vector3[] points, List<int> triangles)
    {
        var normals = new Vector3[points.Length];
        for (int t = 0; t < triangles.Count; t += 3)
        {
            var (a, b, c) = (points[triangles[t]], points[triangles[t + 1]], points[triangles[t + 2]]);
            var face = Vector3.Cross(b - a, c - a);
            normals[triangles[t]] += face;
            normals[triangles[t + 1]] += face;
            normals[triangles[t + 2]] += face;
        }
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].LengthSquared() > 0 ? Vector3.Normalize(normals[i]) : Vector3.UnitY;
        }
        return normals;
    }

    /// <summary>A point's distance from a path (its nearest segment).</summary>
    private static float Distance(List<Vector2> path, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            var ab = b - a;
            float t = ab.LengthSquared() > 0 ? Math.Max(0, Math.Min(1, Vector2.Dot(p - a, ab) / ab.LengthSquared())) : 0;
            best = Math.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    private static float Min(List<Vector2> path, Func<Vector2, float> of)
    {
        float m = float.MaxValue;
        foreach (var p in path)
        {
            m = Math.Min(m, of(p));
        }
        return m;
    }

    private static float Max(List<Vector2> path, Func<Vector2, float> of)
    {
        float m = float.MinValue;
        foreach (var p in path)
        {
            m = Math.Max(m, of(p));
        }
        return m;
    }
}
