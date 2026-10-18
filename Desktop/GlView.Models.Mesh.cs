using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// Meshes on the graphics card (one vertex buffer and an index buffer per part), the stand-in box, and the batches a
// part is drawn with.
public sealed partial class GlView
{
    /// <summary>
    /// A mesh on the graphics card: its vertex buffer and one index buffer per part, uploaded the first time
    /// (<paramref name="md"/> may be null only when it is already there). The model store then forgets the mesh.
    /// </summary>
    private unsafe (uint Vbo, uint[] Ebos, int[] Counts) MeshGl(string id, ModelStore.MeshData? md)
    {
        if (_meshGl.TryGetValue(id, out var m))
        {
            return m;
        }
        // No vertex array bound: binding the index buffers below would rewire whichever one is (another
        // mesh's batch then draws with these triangles: shards everywhere).
        _gl.BindVertexArray(0);
        uint vbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = md!.Vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(md.Vertices.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        uint[] ebos = new uint[md.Submeshes.Length];
        int[] counts = new int[md.Submeshes.Length];
        for (int s = 0; s < ebos.Length; s++)
        {
            ebos[s] = _own.Buffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebos[s]);
            fixed (uint* p = md.Submeshes[s])
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(md.Submeshes[s].Length * 4), p, BufferUsageARB.StaticDraw);
            }
            counts[s] = md.Submeshes[s].Length;
        }
        _gpuBytes += md.Vertices.Length * 4L + md.Submeshes.Sum(x => x.Length * 4L);
        _onGpu[id] = true;
        if (md.BothWays)
        {
            _bothWays.Add(id);
        }
        float[] positions = new float[md.Vertices.Length / 8 * 3];
        for (int v = 0; v < positions.Length / 3; v++)
        {
            positions[v * 3] = md.Vertices[v * 8];
            positions[v * 3 + 1] = md.Vertices[v * 8 + 1];
            positions[v * 3 + 2] = md.Vertices[v * 8 + 2];
        }
        _pickMeshes[id] = new PickMesh(positions, md.Submeshes, md.Bounds);
        Models?.Forget(id);
        return _meshGl[id] = (vbo, ebos, counts);
    }

    /// <summary>The stand-in box's mesh: a unit cube centred on 0, flat normals per face.</summary>
    private (uint, uint[], int[]) BoxMesh()
    {
        const string Id = "\0box";
        if (_meshGl.TryGetValue(Id, out var m))
        {
            return m;
        }
        var verts = new List<float>();
        var idx = new List<uint>();
        // A unit cube centred on 0, flat normals per face.
        Vector3[] n = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        foreach (var f in n)
        {
            var u = MathF.Abs(f.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(f, u);
            uint b = (uint)(verts.Count / 8);
            foreach (var (a, c) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var p = (f + u * a + v * c) * 0.5f;
                verts.AddRange([p.X, p.Y, p.Z, f.X, f.Y, f.Z, 0f, 0f]);
            }
            idx.AddRange([b, b + 1, b + 2, b, b + 2, b + 3]);
        }
        return MeshGl(Id, new ModelStore.MeshData([.. verts], [[.. idx]]));
    }

    /// <summary>The vertex layout of the models' meshes: position, normal and texture coordinates, 32 bytes a vertex.</summary>
    private unsafe void SetUpVertexLayout()
    {
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 32, (void*)12);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 32, (void*)24);
    }

    /// <summary>
    /// A batch for one part of a group's model: the mesh part, its material, and the part's matrix within the model;
    /// <paramref name="bothWays"/>: the mesh has every triangle wound both ways (see <see cref="Batch.BothWays"/>).
    /// </summary>
    private unsafe void AddBatch(Group g, (uint Vbo, uint[] Ebos, int[] Counts) mesh, int sub, ModelStore.MaterialData mat, Matrix4x4 pre, bool bothWays = false, string meshId = "")
    {
        var b = new Batch { Kind = g.Kind, Material = mat, IndexCount = mesh.Counts[sub], Instances = 0, BothWays = bothWays, MeshId = meshId, Sub = sub };
        lock (_textures)
        {
            b.Texture = mat.Map != null && _textures.TryGetValue(mat.Map, out uint t) ? t : 0;
        }
        b.Vao = _own.VertexArray();
        _gl.BindVertexArray(b.Vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, mesh.Vbo);
        SetUpVertexLayout();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, mesh.Ebos[sub]);
        b.InstanceVbo = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
        SetUpInstances();
        _gl.BindVertexArray(0);
        _batches.Add(b);
        lock (_groups)
        {
            g.Batches.Add((b, pre));
        }
    }
}
