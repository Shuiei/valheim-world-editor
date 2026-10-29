using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The batch of one kind of shape: its own vertex and index buffers, one instance where it is (the vertices are in view
// space already).
public sealed partial class GlView
{
    /// <summary>
    /// Makes the batch of the triangles gathered for one kind, colour and sidedness: its own buffers, drawn once in
    /// view space.
    /// </summary>
    /// <param name="kind">The View kind of the shapes.</param>
    /// <param name="color">The colour of the shapes.</param>
    /// <param name="both">Whether the shapes are drawn both sides.</param>
    /// <param name="v">The vertices (eight floats each).</param>
    /// <param name="idx">The indices of the triangles.</param>
    private unsafe void MakeShapeBatch(ObjectKind kind, Vector4 color, bool both, List<float> v, List<uint> idx)
    {
        var b = new Batch
        {
            Kind = kind,
            Material = new ModelStore.MaterialData(color, null, 0, both, new Vector4(1, 1, 0, 0), Transparent: color.W < 1),
            IndexCount = idx.Count,
            Instances = 1,
            // A cell past the area's: never culled.
            Cells = [(int.MaxValue, 0, 1)],
            Size = 1000,
            OwnVbo = _own.Buffer(),
            OwnEbo = _own.Buffer(),
            Vao = _own.VertexArray(),
            InstanceVbo = _own.Buffer(),
        };
        _gl.BindVertexArray(b.Vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.OwnVbo);
        float[] verts = [.. v];
        fixed (float* p = verts)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        SetUpVertexLayout();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, b.OwnEbo);
        uint[] ix = [.. idx];
        fixed (uint* p = ix)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(ix.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        // One instance where it is: the vertices are in view space already (no tint).
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
        var one = Matrix4x4.Identity;
        _gl.BufferData(BufferTargetARB.ArrayBuffer, 64, &one, BufferUsageARB.StaticDraw);
        SetUpInstances();
        _gl.BindVertexArray(0);
        _batches.Add(b);
        _shapeBatches.Add(b);
    }
}
