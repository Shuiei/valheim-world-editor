using WorldEditor.Tests.Unity;
using Xunit;

using static WorldEditor.Tests.GameBundlesTestAssets;

namespace WorldEditor.Tests;

/// <summary>
/// Meshes read from the game's bundles: the vertex count and bounds, every layout the game uses, and compressed meshes
/// unpacked.
/// </summary>
public class GameBundlesMeshTests
{
    private static readonly float[] Expected1 = [1f, 2f, 3f, 0.5f, 1f, 1.5f];
    [Fact]
    public void AMeshsVertexCountAndBoundsAreReadAndNothingWhenItIsNotThere()
    {
        using var game = new TestGame();
        var a = game.Bundle("a_mod");
        long quad = Quad("quad").Add(a);
        long triangle = new TestMesh { Name = "triangle", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]], Bounds = ((1, 2, 3), (0.5, 1, 1.5)) }.Add(a);
        game.Save();
        var bundles = game.Bundles();

        Assert.Equal(4, bundles.VertexCountOf(new("a_mod", quad)));
        Assert.Equal(3, bundles.VertexCountOf(new("a_mod", triangle)));
        Assert.Equal(-1, bundles.VertexCountOf(new("a_mod", 4242)));
        // A bundle read is kept open until Close; read again after that, it is opened again.
        Assert.Equal(1, bundles.OpenBundles);
        bundles.Close();
        Assert.Equal(0, bundles.OpenBundles);
        Assert.Equal(4, bundles.VertexCountOf(new("a_mod", quad)));
        Assert.Equal(1, bundles.OpenBundles);
        Assert.Equal(Expected1, bundles.BoundsOf(new("a_mod", triangle)));
        Assert.Null(bundles.BoundsOf(new("a_mod", 4242)));
    }

    [Fact]
    public void MeshesAreReadInEveryLayoutTheGameUses()
    {
        using var game = new TestGame { BlockSize = 256 };
        var b = game.Bundle("meshes");
        float[] pos = [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1];
        float[] normals = [0, 0, -1, 0, 1, 0, 1, 0, 0, -1, 0, 0];
        float[] uv = [0, 0, 1, 0, 1, 1, 0.5f, 0.25f];
        long plain = new TestMesh { Name = "plain", Positions = pos, Normals = normals, Uv = uv, Submeshes = [[0, 1, 2, 0, 2, 3]] }.Add(b);
        // Half floats, normals in signed bytes, UV in a second stream as 16-bit fractions; 32-bit indices from a base
        // vertex (a count not a multiple of 3 cut); a line list draws nothing.
        long packed = new TestMesh
        {
            Name = "packed",
            Positions = pos,
            Normals = normals,
            Uv = uv,
            PositionFormat = VertexFormat.Ieee16,
            NormalFormat = VertexFormat.SNorm8,
            UvFormat = VertexFormat.UNorm16,
            UvStream = true,
            Wide = true,
            Submeshes = [[0, 1, 2, 3], [0, 1], [0, 1, 2]],
            BaseVertex = [1, 0, 0],
            Topology = [0, 3, 0],
        }.Add(b);
        long bytes = new TestMesh { Name = "bytes", Positions = pos, Normals = normals, Uv = uv, NormalFormat = VertexFormat.SNorm16, UvFormat = VertexFormat.UNorm8, Submeshes = [[0, 1, 2]] }.Add(b);
        // Kept in the bundle's resource file.
        long streamed = new TestMesh { Name = "streamed", Positions = pos, Submeshes = [[2, 1, 0]], Streamed = true }.Add(b);
        game.Save();
        var bundles = game.Bundles();

        var m = bundles.LoadMesh(new("meshes", plain))!;
        Assert.Equal("plain", m.Name);
        Assert.Equal(4, m.VertexCount);
        Assert.Equal(pos, m.Positions);
        Assert.Equal(normals, m.Normals);
        Assert.Equal(uv, m.Uv);
        Assert.Equal([0, 1, 2, 0, 2, 3], m.Submeshes[0]);

        var p = bundles.LoadMesh(new("meshes", packed))!;
        Assert.Equal(pos, p.Positions);
        Assert.Equal(normals, p.Normals);
        for (int i = 0; i < uv.Length; i++)
        {
            Assert.Equal(uv[i], p.Uv![i], 4);
        }
        Assert.Equal([1, 2, 3], p.Submeshes[0]);
        Assert.Empty(p.Submeshes[1]);
        Assert.Equal([0, 1, 2], p.Submeshes[2]);

        var s = bundles.LoadMesh(new("meshes", bytes))!;
        for (int i = 0; i < normals.Length; i++)
        {
            Assert.Equal(normals[i], s.Normals![i], 4);
        }
        Assert.Equal(1f, s.Uv![2], 2);

        var st = bundles.LoadMesh(new("meshes", streamed))!;
        Assert.Equal(pos, st.Positions);
        Assert.Null(st.Normals);
        Assert.Null(st.Uv);
        Assert.Equal([2, 1, 0], st.Submeshes[0]);

        Assert.Null(bundles.LoadMesh(new("meshes", 12345)));
    }

    [Fact]
    public void CompressedMeshesAreUnpacked()
    {
        using var game = new TestGame();
        var b = game.Bundle("meshes");
        float[] pos = [-1, 0, 2, 3, 0.3f, 2, 3, 4, -2, -1, 4, 0.7f];
        float[] normals = [0, 0, -1, 0.6f, 0, 0.8f, 0, 0.6f, -0.8f, 0, 1, 0];
        float[] uv = [0, 0, 1, 0, 1, 1, 0.3f, 0.6f];
        // A count not a multiple of 3 (cut), from a base vertex; 32-bit indices count four bytes each.
        long narrow = new TestMesh { Name = "narrow", Positions = pos, Normals = normals, Uv = uv, Submeshes = [[0, 1, 2, 0, 2, 3], [0, 1, 2, 0]], BaseVertex = [0, 1], Compressed = true }.Add(b);
        long wide = new TestMesh { Name = "wide", Positions = pos, Submeshes = [[3, 2, 1], [0, 1, 2]], Wide = true, Compressed = true }.Add(b);
        game.Save();
        var bundles = game.Bundles();

        var m = bundles.LoadMesh(new("meshes", narrow))!;
        Assert.Equal(("narrow", 4), (m.Name, m.VertexCount));
        // Each value a step of its range (positions: -2 to 4, in 10 bits): at most half a step off.
        float step = 6f / 1023;
        for (int i = 0; i < pos.Length; i++)
        {
            Assert.Equal(pos[i], m.Positions[i], step / 2 + 1e-5f);
        }
        // Normals: x and y kept, z worked out with its sign.
        for (int i = 0; i < normals.Length; i++)
        {
            Assert.Equal(normals[i], m.Normals![i], 0.01f);
        }
        Assert.True(m.Normals![2] < 0);
        Assert.True(m.Normals[8] < 0);
        Assert.True(m.Normals[5] > 0);
        for (int i = 0; i < uv.Length; i++)
        {
            Assert.Equal(uv[i], m.Uv![i], 1f / 1023);
        }
        Assert.Equal([0, 1, 2, 0, 2, 3], m.Submeshes[0]);
        Assert.Equal([1, 2, 3], m.Submeshes[1]);

        var w = bundles.LoadMesh(new("meshes", wide))!;
        Assert.Null(w.Normals);
        Assert.Null(w.Uv);
        Assert.Equal([3, 2, 1], w.Submeshes[0]);
        Assert.Equal([0, 1, 2], w.Submeshes[1]);
    }
}
