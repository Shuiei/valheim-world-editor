using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Unity's round primitives made for the view (BundleModels): Cylinder, Sphere and Capsule, closed and facing out.
/// </summary>
public class BundleRoundPrimitivesTests
{
    [Theory]
    [InlineData("Cylinder", 150, 864, 1f)]
    [InlineData("Sphere", 425, 4608, 0.5f)]
    [InlineData("Capsule", 450, 4896, 1f)]
    public void UnitysRoundPrimitivesAreClosedFacingOutAndSeenFromBothSides(string name, int vertices, int indices, float half)
    {
        var mesh = BundleModels.PrimitiveMesh(name)!;

        Assert.Equal((name, vertices), (mesh.Name, mesh.VertexCount));
        int[] tris = Assert.Single(mesh.Submeshes);
        Assert.Equal(indices, tris.Length);
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        for (int v = 0; v < vertices; v++)
        {
            var p = new Vector3(mesh.Positions[v * 3], mesh.Positions[v * 3 + 1], mesh.Positions[v * 3 + 2]);
            var n = new Vector3(mesh.Normals![v * 3], mesh.Normals[v * 3 + 1], mesh.Normals[v * 3 + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            // On the surface: half a metre from the axis (or inside an end's disc), from the centre (the sphere), or from
            // the segment between the ends' centres (the capsule).
            float r = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
            switch (name)
            {
                case "Cylinder":
                    Assert.True(MathF.Abs(r - 0.5f) < 1e-5f || MathF.Abs(MathF.Abs(p.Y) - 1) < 1e-5f && r <= 0.5f + 1e-5f, $"{p} is not on the cylinder");
                    break;
                case "Sphere":
                    Assert.Equal(0.5f, p.Length(), 5);
                    break;
                default:
                    Assert.Equal(0.5f, (p - new Vector3(0, Math.Clamp(p.Y, -0.5f, 0.5f), 0)).Length(), 5);
                    break;
            }
            // Outward (each is convex around its centre), of unit length; uv from 0 to 1.
            Assert.Equal(1, n.Length(), 5);
            Assert.True(Vector3.Dot(n, p) > 0, $"the normal at {p} points in");
            Assert.InRange(mesh.Uv![v * 2], 0, 1);
            Assert.InRange(mesh.Uv[v * 2 + 1], 0, 1);
        }
        // Unity's bounds: half a metre across, as high as told.
        Assert.True(Vector3.Distance(new Vector3(-0.5f, -half, -0.5f), min) < 1e-5f, $"lowest corner {min}");
        Assert.True(Vector3.Distance(new Vector3(0.5f, half, 0.5f), max) < 1e-5f, $"highest corner {max}");
        var both = Enumerable.Range(0, tris.Length / 3).Select(t => (tris[t * 3], tris[t * 3 + 1], tris[t * 3 + 2])).ToHashSet();
        Assert.All(both, t => Assert.Contains((t.Item1, t.Item3, t.Item2), both));
    }
}
