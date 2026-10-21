using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Unity's own primitives made for the view (BundleModels): Cube, Quad and Plane, and the names not made here.
/// </summary>
public class BundlePrimitivesTests
{
    [Fact]
    public void UnitysCubeIsABoxSeenFromBothSides()
    {
        var cube = BundleModels.PrimitiveMesh("Cube")!;

        Assert.Equal(("Cube", 24), (cube.Name, cube.VertexCount));
        int[] tris = Assert.Single(cube.Submeshes);
        Assert.Equal(72, tris.Length);
        for (int v = 0; v < 24; v++)
        {
            var p = new Vector3(cube.Positions[v * 3], cube.Positions[v * 3 + 1], cube.Positions[v * 3 + 2]);
            var n = new Vector3(cube.Normals![v * 3], cube.Normals[v * 3 + 1], cube.Normals[v * 3 + 2]);
            // On the box's surface, its normal one of the axes, outward.
            Assert.Equal(0.5f, MathF.Max(MathF.Abs(p.X), MathF.Max(MathF.Abs(p.Y), MathF.Abs(p.Z))), 5);
            Assert.Equal(1, n.Length(), 5);
            Assert.Equal(1, MathF.Abs(n.X) + MathF.Abs(n.Y) + MathF.Abs(n.Z), 5);
            Assert.Equal(0.5f, Vector3.Dot(n, p), 5);
            Assert.InRange(cube.Uv![v * 2], 0, 1);
            Assert.InRange(cube.Uv[v * 2 + 1], 0, 1);
        }
        // Each triangle wound both ways.
        var both = Enumerable.Range(0, tris.Length / 3).Select(t => (tris[t * 3], tris[t * 3 + 1], tris[t * 3 + 2])).ToList();
        Assert.All(both, t => Assert.Contains((t.Item1, t.Item3, t.Item2), both));
    }

    [Theory]
    [InlineData("Quad", 2, 0, 0, -1)]
    [InlineData("Plane", 1, 0, 1, 0)]
    public void UnitysQuadAndPlaneAreFlatSquares(string name, int flat, float nx, float ny, float nz)
    {
        var mesh = BundleModels.PrimitiveMesh(name)!;

        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(12, Assert.Single(mesh.Submeshes).Length);
        float size = name == "Quad" ? 0.5f : 5;
        for (int v = 0; v < 4; v++)
        {
            Assert.Equal(0, mesh.Positions[v * 3 + flat]);
            Assert.All(Enumerable.Range(0, 3).Where(a => a != flat), a => Assert.Equal(size, MathF.Abs(mesh.Positions[v * 3 + a])));
            Assert.Equal(new Vector3(nx, ny, nz), new Vector3(mesh.Normals![v * 3], mesh.Normals[v * 3 + 1], mesh.Normals[v * 3 + 2]));
        }
        Assert.Equal([0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f], mesh.Uv!);
    }

    [Fact]
    public void AnyOtherPrimitiveIsNotMade()
    {
        Assert.Null(BundleModels.PrimitiveMesh("Torus"));
    }
}
