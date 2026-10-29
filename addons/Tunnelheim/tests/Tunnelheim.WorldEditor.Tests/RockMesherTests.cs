using System.Numerics;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The rock's surface: nothing for a chunk that is not there, flat ground meshed flat with normals up, and a dug hole
/// meshed with its vertices marked dug and its triangles facing the air.
/// </summary>
public class RockMesherTests
{
    [Fact]
    public void AChunkThatIsNotThereHasNoSurface() => Assert.Same(RockMesh.Empty, RockMesher.Build(new RockGrid((_, _) => 40f), 0, 2, 0));

    [Fact]
    public void FlatGroundIsMeshedFlatFacingUp()
    {
        var grid = new RockGrid((_, _) => 40.25f);
        grid.EnsureColumn(0, 0);
        var m = RockMesher.Build(grid, 0, 2, 0);
        Assert.NotEmpty(m.Triangles);
        Assert.All(m.Positions, p => Assert.Equal(40.25f - 32f, p.Y, 3));
        Assert.All(m.Normals, n => Assert.Equal((0f, 1f, 0f), (Math.Abs(n.X), n.Y, Math.Abs(n.Z))));
        Assert.DoesNotContain(true, m.Dug);
    }

    [Fact]
    public void ADugHoleIsMarkedDug()
    {
        var grid = new RockGrid((_, _) => 40.25f);
        grid.Carve(new DigOp(DigKind.Sphere, new Vector3(8f, 40f, 8f), 2f));
        var m = RockMesher.Build(grid, 0, 2, 0);
        Assert.Contains(true, m.Dug);
        for (int t = 0; t < m.Triangles.Length; t += 3)
        {
            var (a, b, c) = (m.Positions[m.Triangles[t]], m.Positions[m.Triangles[t + 1]], m.Positions[m.Triangles[t + 2]]);
            // Clockwise seen from the front in Unity's left-handed frame: the cross product of the edges points to the front, into the air.
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            Assert.True(Vector3.Dot(n, m.Normals[m.Triangles[t]]) > 0f);
        }
    }
}
