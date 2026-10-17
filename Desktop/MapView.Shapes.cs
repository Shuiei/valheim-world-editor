using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

/// <summary>
/// Shapes over the map in world metres: fills, quads, corners and discs, made of triangles.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    // ---- Shapes over the map in world metres (one colour per corner, drawn in order).
    /// <summary>
    /// Draws shapes from <paramref name="data"/>: x, z then RGBA, 6 floats per corner.
    /// </summary>
    private unsafe void Fill(float[] data, PrimitiveType type)
    {
        if (data.Length == 0)
        {
            return;
        }
        _gl.BindVertexArray(_fillVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _fillVbo);
        fixed (float* p = data)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.StreamDraw);
        }
        _gl.DrawArrays(type, 0, (uint)(data.Length / 6));
    }

    /// <summary>
    /// Adds a quad (two triangles a-b-c, a-c-d) of one colour.
    /// </summary>
    private static void Quad(List<float> to, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 col)
    {
        Corner(to, a.X, a.Y, col);
        Corner(to, b.X, b.Y, col);
        Corner(to, c.X, c.Y, col);
        Corner(to, a.X, a.Y, col);
        Corner(to, c.X, c.Y, col);
        Corner(to, d.X, d.Y, col);
    }

    /// <summary>Adds one corner: x, z then its colour (no array made for it).</summary>
    private static void Corner(List<float> to, float x, float z, Vector4 col)
    {
        to.Add(x);
        to.Add(z);
        to.Add(col.X);
        to.Add(col.Y);
        to.Add(col.Z);
        to.Add(col.W);
    }

    /// <summary>
    /// Adds a disc of one colour, as a fan of <paramref name="sides"/> triangles.
    /// </summary>
    /// <param name="to">Where the corners go.</param>
    /// <param name="x">Middle, east.</param>
    /// <param name="z">Middle, north.</param>
    /// <param name="r">Radius in metres.</param>
    /// <param name="col">Its colour.</param>
    /// <param name="sides">How many triangles.</param>
    private static void Disc(List<float> to, float x, float z, float r, Vector4 col, int sides = 12)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * MathF.Tau / sides, a1 = (i + 1) * MathF.Tau / sides;
            Corner(to, x, z, col);
            Corner(to, x + r * MathF.Cos(a0), z + r * MathF.Sin(a0), col);
            Corner(to, x + r * MathF.Cos(a1), z + r * MathF.Sin(a1), col);
        }
    }
}
