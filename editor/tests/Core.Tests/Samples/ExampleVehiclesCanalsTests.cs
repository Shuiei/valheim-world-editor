using System.Numerics;
using ExampleVehicles.WorldEditor;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The SDK sample's canals (sdk/samples/ExampleVehicles.WorldEditor/Canals.cs), a scene layer: a path kept the mod's way
/// and read back; a canal dug as a trench a depth below the ground, its bed painted dirt and its banks left as the
/// ground, the ground left undrawn over it cell by cell; nothing for an area without canals or with a damaged path.
/// </summary>
public class ExampleVehiclesCanalsTests
{
    /// <summary>An area of flat ground at 40 m in the Meadows holding the canals given.</summary>
    private static PlainArea Area(params IObjectView[] canals) =>
        new(-32, -32, 32, 32, 40) { Objects = new Dictionary<string, IReadOnlyList<IObjectView>> { [Keys.Canal] = canals }, BiomeValue = 1, Colour = 0x01020304 };

    /// <summary>A canal object keeping a path (and a width when given).</summary>
    private static ObjectView Canal(byte[] path, float? width = null)
    {
        var z = new ZdoData { Prefab = StableHash.Of(Keys.Canal) };
        z.ByteList.Add((StableHash.Of(Keys.Path), path));
        if (width is float w)
        {
            z.FloatList.Add((StableHash.Of(Keys.Width), w));
        }
        return new ObjectView(Keys.Canal, Vector3.Zero, Vector3.Zero, z);
    }

    [Fact]
    public void APathIsKeptTheModsWay()
    {
        Vector2[] points = [new(-3.5f, 2), new(10, 2.25f)];
        byte[] bytes = Canals.WritePath(points);
        Assert.Equal(17, bytes.Length);
        Assert.Equal(points, Canals.ReadPath(bytes)!);
        Assert.Null(Canals.ReadPath([2, 0, 0, 0, 0, 0, 0, 0, 0]));
        Assert.Null(Canals.ReadPath([1, 0, 0]));
        Assert.Null(Canals.ReadPath([]));
    }

    [Fact]
    public void ACanalIsDugBelowTheGroundWhichIsLeftUndrawnOverIt()
    {
        var layer = new Canals().Build(Area(Canal(Canals.WritePath([new(0, 0), new(10, 0)]))));
        var mesh = Assert.Single(layer!.Meshes);
        // The bed (within 1.5 m of the path, the default width's half) 1.5 m down, painted dirt; the banks at the ground.
        for (int i = 0; i < mesh.Positions.Length; i++)
        {
            var p = mesh.Positions[i];
            // Its distance from the path, from (0, 0) to (10, 0).
            float d = Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(Math.Clamp(p.X, 0, 10), 0));
            bool bed = d <= 1.5f;
            Assert.Equal(bed ? 38.5f : 40f, p.Y);
            Assert.Equal(bed ? 0.75f : 0.25f, mesh.MaskUvs![i].X);
            Assert.Equal(0x01020304u, mesh.BiomeColors![i]);
        }
        Assert.Equal(2, mesh.Mask!.Width);
        // Every metre square holed is a pair of triangles of the mesh, and the path's own squares are holed.
        Assert.Equal(layer.Holes.Count * 6, mesh.Triangles.Length);
        Assert.Contains(layer.Holes, h => h.X0 == 5 && h.Z0 == 0);
        Assert.Contains(layer.Holes, h => h.X0 == 5 && h.Z0 == -1);
        Assert.DoesNotContain(layer.Holes, h => h.Z0 is >= 4 or < -4);
        // Faces up are wound clockwise seen from above (Unity's front): their normals point up.
        Assert.All(mesh.Normals, n => Assert.True(n.Y > 0));
    }

    [Fact]
    public void AWiderCanalHolesMore()
    {
        static int Holes(float? width) => new Canals().Build(Area(Canal(Canals.WritePath([new(0, 0), new(10, 0)]), width)))!.Holes.Count;
        Assert.True(Holes(6) > Holes(null));
    }

    [Fact]
    public void WithoutACanalOrWithADamagedPathNothingIsDrawn()
    {
        Assert.Null(new Canals().Build(Area()));
        Assert.Null(new Canals().Build(Area(Canal([9, 9]))));
        Assert.Null(new Canals().Build(Area(Canal(Canals.WritePath([new(1, 1)])))));
    }
}
