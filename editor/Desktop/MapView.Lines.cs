using System.Numerics;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace TerrainEditor.Desktop;

/// <summary>
/// The lines over the map in screen pixels: the zone grid, edited zones, resets and the chosen area.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    // ---- Lines over the map (in screen pixels): the chosen area, edited zones, resets, the grid.
    /// <summary>
    /// Draws the lines: the zone grid, edited zones (thin yellow; thick orange where changed since the world was loaded),
    /// resets and the chosen area.
    /// </summary>
    /// <param name="w">The view's width, in device pixels.</param>
    /// <param name="h">The view's height, in device pixels.</param>
    /// <param name="mpp">Metres per device pixel.</param>
    private unsafe void DrawLines(int w, int h, float mpp)
    {
        var s = _session;
        if (s == null)
        {
            return;
        }
        // Made again only when what they show or where the map looks changed: the same while the map idles.
        var key = (s, s.Edits.Version, Center, mpp, w, h, Chosen, ShowGrid, ShowEdited, MetersPerPixel);
        if (_linesKey != key)
        {
            (_linesKey, _lines) = (key, Lines(s, w, h, mpp));
        }
        var id = Matrix4x4.Identity;
        _gl.UseProgram(_lineProg);
        _gl.UniformMatrix4(_u.At(_lineProg, "uViewProj"), 1, false, (float*)&id);
        GlBlend.Over(_gl);
        _gl.BindVertexArray(_lineVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
        foreach (var (data, col) in _lines)
        {
            fixed (float* p = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4), p, BufferUsageARB.DynamicDraw);
            }
            _gl.Uniform4(_u.At(_lineProg, "uColor"), col.X, col.Y, col.Z, col.W);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(data.Length / 3));
        }
        _gl.Disable(EnableCap.Blend);
    }

    /// <summary>What the lines were last made for (<see cref="DrawLines"/>).</summary>
    private (WorldSession?, int, Vector2, float, int, int, (int X, int Z, int Size)?, bool, bool, float) _linesKey;

    /// <summary>The lines last made, by colour, those with any line only.</summary>
    private List<(float[] Data, Vector4 Color)> _lines = [];

    /// <summary>The lines of <see cref="DrawLines"/>, in clip space, by colour (groups without a line left out).</summary>
    private List<(float[] Data, Vector4 Color)> Lines(WorldSession s, int w, int h, float mpp)
    {
        var groups = new List<(float[] Data, Vector4 Color)>();
        var frame = new LineFrame(Center, w, h, mpp);
        AddGrid(groups, frame);
        if (ShowEdited)
        {
            AddEdited(groups, s, frame);
        }
        AddResets(groups, s, frame);
        AddArea(groups, frame);
        return [.. groups.Where(gr => gr.Data.Length > 0)];
    }
}
