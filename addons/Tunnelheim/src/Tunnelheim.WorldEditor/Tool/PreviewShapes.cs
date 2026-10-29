using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor;

/// <summary>The Tunnel tool's preview: see-through balls and boxes where its edits go, in the world's frame.</summary>
public static class PreviewShapes
{
    /// <summary>Where rock goes: orange; where it comes: green.</summary>
    private static readonly Vector4 Dig = new(1f, 0.55f, 0.1f, 0.35f), Add = new(0.3f, 0.9f, 0.3f, 0.35f);

    /// <summary>A ball (an octahedron twice divided, its corners pushed out to the radius).</summary>
    /// <param name="centre">Its middle.</param>
    /// <param name="radius">Its radius.</param>
    /// <param name="adds">Whether it adds rock (else it digs).</param>
    public static Shape Ball(Vector3 centre, float radius, bool adds = false)
    {
        var corners = new List<Vector3> { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
        var triangles = new List<int> { 0, 4, 2, 4, 1, 2, 1, 5, 2, 5, 0, 2, 4, 0, 3, 1, 4, 3, 5, 1, 3, 0, 5, 3 };
        for (int pass = 0; pass < 2; pass++)
        {
            (corners, triangles) = Split(corners, triangles);
        }
        Vector3[] at = [.. corners.Select(c => centre + (Vector3.Normalize(c) * radius))];
        return SeeThrough(at, [.. triangles], adds ? Add : Dig);
    }

    /// <summary>A tube between two points (a bore's length), sixteen sides round, open at its ends.</summary>
    /// <param name="from">One end's middle.</param>
    /// <param name="to">The other end's middle.</param>
    /// <param name="radius">Its radius.</param>
    public static Shape Tube(Vector3 from, Vector3 to, float radius)
    {
        const int Sides = 16;
        var along = to - from;
        var axis = along.LengthSquared() > 1e-6f ? Vector3.Normalize(along) : Vector3.UnitZ;
        var side = Vector3.Normalize(Vector3.Cross(axis, Math.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
        var up = Vector3.Cross(side, axis);
        var at = new Vector3[Sides * 2];
        int[] triangles = new int[Sides * 6];
        for (int i = 0; i < Sides; i++)
        {
            double a = i * 2 * Math.PI / Sides;
            var round = ((float)Math.Cos(a) * side + (float)Math.Sin(a) * up) * radius;
            at[i] = from + round;
            at[i + Sides] = to + round;
            int next = (i + 1) % Sides;
            new[] { i, next, i + Sides, next, next + Sides, i + Sides }.CopyTo(triangles, i * 6);
        }
        return SeeThrough(at, triangles, Dig);
    }

    /// <summary>A box, axis-aligned.</summary>
    /// <param name="centre">Its middle.</param>
    /// <param name="half">Half its size along each axis.</param>
    public static Shape Box(Vector3 centre, Vector3 half)
    {
        var at = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            // Corner i: bit 0 picks the high x side, bit 1 the high y, bit 2 the high z.
            at[i] = centre + (half * new Vector3(((i & 1) * 2) - 1, (((i >> 1) & 1) * 2) - 1, (((i >> 2) & 1) * 2) - 1));
        }
        int[] faces = [0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3];
        return SeeThrough(at, faces, Dig);
    }

    /// <summary>A preview shape: in the world's frame, seen from both sides.</summary>
    private static Shape SeeThrough(Vector3[] at, int[] triangles, Vector4 colour) => new(at, triangles, colour) { BothSides = true, WorldSpace = true };

    /// <summary>Each triangle split in four at its edges' middles.</summary>
    private static (List<Vector3>, List<int>) Split(List<Vector3> corners, List<int> triangles)
    {
        var made = new List<Vector3>(corners);
        var middles = new Dictionary<(int, int), int>();
        int Middle(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (!middles.TryGetValue(key, out int i))
            {
                middles[key] = i = made.Count;
                made.Add((made[a] + made[b]) / 2);
            }
            return i;
        }
        var split = new List<int>(triangles.Count * 4);
        for (int t = 0; t < triangles.Count; t += 3)
        {
            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
            int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
            split.AddRange([a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca]);
        }
        return (made, split);
    }
}
