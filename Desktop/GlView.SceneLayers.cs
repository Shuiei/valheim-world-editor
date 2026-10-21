using System.Numerics;
using Silk.NET.OpenGL;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

// Mods' extensions' layers (SceneLayers) on the graphics card: their meshes, drawn with the ground's programs, and the
// cells of ground they leave undrawn.
public sealed partial class GlView
{
    /// <summary>One layer mesh on the graphics card.</summary>
    /// <param name="Vao">Its vertex array (the ground's layout: position, normal, colour, biome colour, mask uv).</param>
    /// <param name="Buffers">Its buffers.</param>
    /// <param name="Count">Its index count.</param>
    /// <param name="Mask">Its paint mask texture (0: the plain one).</param>
    /// <param name="Look">Drawn with the game's ground look (it has biome colours).</param>
    private sealed record LayerMesh(uint Vao, uint[] Buffers, uint Count, uint Mask, bool Look);

    /// <summary>What is on the graphics card, and for which result.</summary>
    private SceneLayers.Built? _layersUp;

    /// <summary>The layers' meshes on the graphics card.</summary>
    private readonly List<LayerMesh> _layerMeshes = [];

    /// <summary>The ground cells left undrawn, one byte a cell (0: none uploaded), and a plain paint mask for meshes without one.</summary>
    private uint _holeTex, _plainMask;

    /// <summary>Whether ground cells are left undrawn now.</summary>
    private bool _holesOn;

    /// <summary>The layers worked out last for the area: sent to the graphics card when they are new.</summary>
    private void PrepareLayers(WorldScene s)
    {
        var built = SceneLayers.Shown(s, Wake);
        if (ReferenceEquals(built, _layersUp))
        {
            return;
        }
        DropLayers();
        _layersUp = built;
        foreach (var m in built.Layers.SelectMany(l => l.Meshes).Where(m => m.Triangles.Length > 0))
        {
            _layerMeshes.Add(UploadLayerMesh(s, m));
        }
        UploadHoles(s, built.Holes);
    }

    /// <summary>The layers' meshes and holes off the graphics card.</summary>
    private void DropLayers()
    {
        foreach (var m in _layerMeshes)
        {
            _own.DeleteVertexArray(m.Vao);
            foreach (uint b in m.Buffers)
            {
                _own.DeleteBuffer(b);
            }
            if (m.Mask != 0)
            {
                _own.DeleteTexture(m.Mask);
            }
        }
        _layerMeshes.Clear();
        _own.DeleteTexture(_holeTex);
        _holeTex = 0;
        _holesOn = false;
        _layersUp = null;
    }

    /// <summary>A mesh in the view's frame with the ground's attributes; its triangles wound for OpenGL (z is mirrored).</summary>
    private unsafe LayerMesh UploadLayerMesh(WorldScene s, SceneMesh m)
    {
        int n = m.Positions.Length;
        float[] v = new float[n * 9];
        var c = new Vector3(MathF.Pow(m.Color.X, 2.2f), MathF.Pow(m.Color.Y, 2.2f), MathF.Pow(m.Color.Z, 2.2f));
        float[] extra = new float[n * 4];
        for (int i = 0; i < n; i++)
        {
            var p = SceneLayers.ToView(s, m.Positions[i]);
            var nv = m.Normals[i];
            (v[i * 9], v[i * 9 + 1], v[i * 9 + 2]) = (p.X, p.Y, p.Z);
            (v[i * 9 + 3], v[i * 9 + 4], v[i * 9 + 5]) = (nv.X, nv.Y, -nv.Z);
            (v[i * 9 + 6], v[i * 9 + 7], v[i * 9 + 8]) = (c.X, c.Y, c.Z);
            var uv = m.Mask != null && m.MaskUvs != null ? m.MaskUvs[i] : new Vector2(0.5f, 0.5f);
            (extra[i * 4], extra[i * 4 + 1]) = (uv.X, uv.Y);
        }
        uint[] idx = new uint[m.Triangles.Length];
        for (int t = 0; t < idx.Length; t += 3)
        {
            (idx[t], idx[t + 1], idx[t + 2]) = ((uint)m.Triangles[t], (uint)m.Triangles[t + 2], (uint)m.Triangles[t + 1]);
        }
        uint vao = _own.VertexArray();
        _gl.BindVertexArray(vao);
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
        uint[] biomes = m.BiomeColors ?? new uint[n];
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, bc);
        fixed (uint* p = biomes)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(biomes.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.UnsignedByte, true, 4, (void*)0);
        // Mask uv, then no ocean depth and no limit tint (the game's rock material has none).
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, ex);
        fixed (float* p = extra)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(extra.Length * 4), p, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, 16, (void*)0);
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, 16, (void*)8);
        _gl.EnableVertexAttribArray(6);
        _gl.VertexAttribPointer(6, 1, VertexAttribPointerType.Float, false, 16, (void*)12);
        _gl.BindVertexArray(0);
        uint mask = m.Mask is { } t2 && m.MaskUvs != null ? MaskTexture(t2.Width, t2.Height, t2.Texels) : 0;
        return new LayerMesh(vao, [vbo, ebo, bc, ex], (uint)idx.Length, mask, m.BiomeColors != null);
    }

    /// <summary>A paint mask texture: RGBA texels from the bottom row, filtered between them, clamped.</summary>
    private unsafe uint MaskTexture(int w, int h, uint[] texels)
    {
        uint tex = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (uint* p = texels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        return tex;
    }

    /// <summary>The ground cells left undrawn, one byte a cell, read exactly (no filtering).</summary>
    private unsafe void UploadHoles(WorldScene s, byte[] cells)
    {
        _holesOn = cells.Length == (s.W - 1) * (s.H - 1) && cells.Length > 0;
        if (!_holesOn)
        {
            return;
        }
        _holeTex = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture2D, _holeTex);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (byte* p = cells)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, (uint)(s.W - 1), (uint)(s.H - 1), 0, PixelFormat.Red, PixelType.UnsignedByte, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    /// <summary>The plain paint mask (the game's unpainted ground) for meshes with the look but no mask of their own.</summary>
    private unsafe uint PlainMask()
    {
        if (_plainMask == 0)
        {
            _plainMask = MaskTexture(1, 1, [0xFF000000u]);
        }
        return _plainMask;
    }

    /// <summary>Tells the bound ground program which cells to leave undrawn (none for the layers' own meshes).</summary>
    /// <param name="s">The area.</param>
    /// <param name="program">The program in use.</param>
    /// <param name="holes">Whether to leave the cells undrawn.</param>
    /// <param name="unit">The texture unit the cells go on.</param>
    private void SetHoles(WorldScene s, uint program, bool holes, int unit)
    {
        bool on = holes && _holesOn;
        _gl.Uniform1(_u.At(program, "uHolesOn"), on ? 1f : 0f);
        // Always on a unit of its own: two samplers of different kinds on one unit fail the draw, used or not.
        _gl.Uniform1(_u.At(program, "uHoles"), unit);
        if (!on)
        {
            return;
        }
        _gl.ActiveTexture(TextureUnit.Texture0 + unit);
        _gl.BindTexture(TextureTarget.Texture2D, _holeTex);
        _gl.Uniform4(_u.At(program, "uHoleGrid"), (s.W - 1) / 2f, (s.H - 1) / 2f, s.W - 1, s.H - 1);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>Some of the layers' meshes with a ground program in use (the look's: its paint mask swapped for each).</summary>
    private unsafe void DrawLayerMeshes(WorldScene s, uint program, GameLookGl? look, Func<LayerMesh, bool> which)
    {
        SetHoles(s, program, false, look?.FreeUnit ?? 0);
        foreach (var m in _layerMeshes.Where(which))
        {
            look?.BindMask(m.Mask != 0 ? m.Mask : PlainMask());
            _gl.BindVertexArray(m.Vao);
            _gl.DrawElements(PrimitiveType.Triangles, m.Count, DrawElementsType.UnsignedInt, (void*)0);
        }
        look?.BindMask(null);
    }
}
