using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The selection's outline, as in Blender or a map editor: the selected objects drawn in a flat colour into a picture
// of their shape (the mask), then a line round that shape over the view, the active object's brighter.
public sealed partial class GlView
{
    /// <summary>The mask's picture and its framebuffer, made again when the size changes; its size.</summary>
    private uint _outlineFbo, _outlineTex;
    /// <summary>The mask's size in pixels.</summary>
    private int _outlineW, _outlineH;
    /// <summary>The outline's shader and the empty vertex array its one triangle is drawn with.</summary>
    private uint _outlineProg, _outlineVao;
    /// <summary>The picture the frame is drawn into (the screen's, or the smaller one) and its size.</summary>
    private uint _frameFbo;
    /// <summary>The frame's picture size in pixels.</summary>
    private int _frameW, _frameH;
    /// <summary>
    /// The selected objects' model parts with their placements, made again with the selection's boxes; the active
    /// object's apart (drawn last, in the mask's green).
    /// </summary>
    private List<(Batch B, Matrix4x4[] Data, bool Active)> _outlineParts = [];
    /// <summary>The mask's picture could not be made: the selection is boxed from then on.</summary>
    private bool _outlineFailed;

    /// <summary>The outline's colour: the selection's orange.</summary>
    private static readonly Vector4 OutlineColour = new(1, 0.6f, 0.15f, 1);
    /// <summary>The active object's outline: a lighter yellow, as Blender's.</summary>
    private static readonly Vector4 ActiveColour = new(1, 0.9f, 0.45f, 1);

    /// <summary>
    /// The selected objects' model parts and placements for the mask (where a move shows them), the active one's apart;
    /// the selected objects with no model drawn yet (or none) are returned, for a box instead.
    /// </summary>
    /// <param name="s">The area shown.</param>
    private List<int> MakeOutlineParts(WorldScene s)
    {
        var parts = new List<(Batch B, Matrix4x4[] Data, bool Active)>();
        var boxed = new List<int>();
        int? active = ActiveObject;
        var previews = _previews;
        List<(int I, WorldScene.Thing T)> things;
        lock (s.Things)
        {
            things = [.. Selected.Where(i => i < s.Things.Count).Select(i => (i, previews.TryGetValue(i, out var p) ? p : s.Things[i])).Where(x => !x.Item2.Gone)];
        }
        foreach (var same in things.GroupBy(x => (Key: KeyOf(x.T), Active: x.I == active)).OrderBy(g => g.Key.Active))
        {
            Group? g;
            lock (_groups)
            {
                _groups.TryGetValue(same.Key.Key, out g);
            }
            if (g is not { Ready: true, Batches.Count: > 0 })
            {
                boxed.AddRange(same.Select(x => x.I));
                continue;
            }
            var placements = same.Select(x => Placement(s, x.T, g.RootScale)).ToList();
            foreach (var (b, pre) in g.Batches)
            {
                parts.Add((b, [.. placements.Select(m => pre * m)], same.Key.Active));
            }
        }
        _outlineParts = parts;
        return boxed;
    }

    /// <summary>
    /// The outline round the selected objects, over everything (seen through what is in front, as the boxes were):
    /// the mask drawn, then the line, two pixels wide at 1080 lines (one to four).
    /// </summary>
    private unsafe void DrawOutline()
    {
        var parts = _outlineParts;
        if (parts.Count == 0 || !OutlineTarget(_frameW, _frameH))
        {
            return;
        }
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outlineFbo);
        _gl.ClearColor(0, 0, 0, 0);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _gl.Disable(EnableCap.DepthTest);
        _gl.UseProgram(_objectProg);
        var u = MaterialUniforms.Of(_u, _objectProg);
        int uFlat = _u.At(_objectProg, "uFlat");
        if (_ghostVbo == 0)
        {
            _ghostVbo = _own.Buffer();
        }
        foreach (var (b, data, active) in parts)
        {
            _gl.Uniform4(uFlat, 1f, active ? 1f : 0f, 0f, 1f);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _ghostVbo);
            fixed (Matrix4x4* p = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), p, BufferUsageARB.DynamicDraw);
            }
            DrawFrom(b, _ghostVbo, data.Length, null, u);
        }
        _gl.Uniform4(uFlat, 0f, 0f, 0f, 0f);
        _gl.Disable(EnableCap.Blend);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _frameFbo);
        DrawOutlineLine();
    }

    /// <summary>The line round the mask's shape over the frame's picture (OutlineFs).</summary>
    private void DrawOutlineLine()
    {
        if (_outlineProg == 0)
        {
            _outlineProg = Program(Shaders.OutlineVs, Shaders.OutlineFs);
            _outlineVao = _own.VertexArray();
        }
        _gl.UseProgram(_outlineProg);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _outlineTex);
        _gl.Uniform1(_u.At(_outlineProg, "uMask"), 0);
        _gl.Uniform1(_u.At(_outlineProg, "uWidth"), Math.Clamp((int)MathF.Round(_frameH / 540f), 1, 4));
        _gl.Uniform4(_u.At(_outlineProg, "uColor"), OutlineColour.X, OutlineColour.Y, OutlineColour.Z, OutlineColour.W);
        _gl.Uniform4(_u.At(_outlineProg, "uActive"), ActiveColour.X, ActiveColour.Y, ActiveColour.Z, ActiveColour.W);
        _gl.Disable(EnableCap.CullFace);
        _gl.BindVertexArray(_outlineVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.Enable(EnableCap.DepthTest);
    }

    /// <summary>
    /// The mask's picture, made again when <paramref name="w"/> × <paramref name="h"/> is not its size.
    /// </summary>
    /// <returns>False when the graphics card does not take it (incomplete; logged): the selection is boxed instead.</returns>
    private unsafe bool OutlineTarget(int w, int h)
    {
        if (_outlineFbo != 0 && w == _outlineW && h == _outlineH)
        {
            return true;
        }
        DropOutlineTarget();
        (_outlineW, _outlineH) = (w, h);
        _outlineTex = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture2D, _outlineTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _outlineFbo = _own.Framebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outlineFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _outlineTex, 0);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _frameFbo);
        if (status != GLEnum.FramebufferComplete)
        {
            Console.Error.WriteLine($"The {w}×{h} picture for the selection's outline is incomplete ({status}): the selection is boxed.");
            DropOutlineTarget();
            _outlineFailed = true;
            _selectionDirty = true;
            return false;
        }
        return true;
    }

    /// <summary>Deletes the mask's picture, if there is one.</summary>
    private void DropOutlineTarget()
    {
        if (_outlineFbo != 0)
        {
            _own.DeleteFramebuffer(_outlineFbo);
            _own.DeleteTexture(_outlineTex);
        }
        _outlineFbo = _outlineTex = 0;
        _outlineW = _outlineH = 0;
    }
}
