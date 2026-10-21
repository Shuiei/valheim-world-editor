using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// The vertex layout the ground's programs read (GlView.GroundLayout.cs), shared by the ground and the mods' layers
/// drawn like it.
/// </summary>
public sealed partial class GlView
{
    /// <summary>
    /// Fills the bound vertex array with a mesh in the ground's layout: position, normal and plain colour (nine floats a
    /// corner, attributes 0 to 2), the game's biome colour (four bytes a corner, attribute 3), then the mask uv, ocean
    /// depth and limit tint (four floats a corner, attributes 4 to 6), and its triangles.
    /// </summary>
    /// <param name="v">The corners' nine floats each.</param>
    /// <param name="idx">The triangles' corners.</param>
    /// <param name="biomes">The corners' biome colours, four bytes each.</param>
    /// <param name="extra">The corners' four extra floats each.</param>
    /// <returns>The buffers made: corners, triangles, biome colours, extras.</returns>
    private unsafe (uint Vbo, uint Ebo, uint Biomes, uint Extra) UploadGroundLayout(float[] v, uint[] idx, ReadOnlySpan<byte> biomes, float[] extra)
    {
        uint vbo = _own.Buffer(), ebo = _own.Buffer(), bc = _own.Buffer(), ex = _own.Buffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        _own.Upload(BufferTargetARB.ArrayBuffer, v);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        _own.Upload(BufferTargetARB.ElementArrayBuffer, idx);
        for (uint a = 0; a < 3; a++)
        {
            _gl.EnableVertexAttribArray(a);
            _gl.VertexAttribPointer(a, 3, VertexAttribPointerType.Float, false, 36, (void*)(a * 12));
        }
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, bc);
        fixed (byte* p = biomes)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)biomes.Length, p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.UnsignedByte, true, 4, (void*)0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, ex);
        _own.Upload(BufferTargetARB.ArrayBuffer, extra);
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, 16, (void*)0);
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, 16, (void*)8);
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribPointer(6, 1, VertexAttribPointerType.Float, false, 16, (void*)12);
        return (vbo, ebo, bc, ex);
    }

    /// <summary>A mesh's biome colours (red in the lowest byte) as the bytes the layout reads.</summary>
    private static ReadOnlySpan<byte> ColourBytes(uint[] colours) => MemoryMarshal.AsBytes(colours.AsSpan());
}
